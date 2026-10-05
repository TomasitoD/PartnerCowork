using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SaveStock.Core.ControlAcceso.Entidades;

namespace SaveStock.Tests.Integracion;

/// <summary>
/// Registro, activación y reenvío del enlace a través de la web (RF-CA-01, RF-CA-02, RF-CA-14,
/// RF-CA-15, RF-CA-16, RF-CA-17). La base es compartida por la clase: cada prueba usa su propio correo.
/// </summary>
public partial class RegistroTests : IClassFixture<SaveStockFactory>
{
    private const string MensajeEnlaceInvalido = "El enlace no es válido, ya fue usado o venció.";
    private const string MensajeReenvio = "Si el correo está registrado y la cuenta no está activa, te enviamos un nuevo enlace.";

    private readonly SaveStockFactory _fabrica;
    private readonly HttpClient _cliente;

    public RegistroTests(SaveStockFactory fabrica)
    {
        _fabrica = fabrica;
        _cliente = fabrica.CreateClient();
    }

    [GeneratedRegex(@"http://localhost/cuentas/activar\?token=([A-Za-z0-9_-]+)")]
    private static partial Regex RegexEnlace();

    private Task<HttpResponseMessage> RegistrarAsync(string correo, string contrasena = "clave1234", string nombre = "Ana") =>
        _cliente.PostAsJsonAsync("/api/cuentas/registro", new { nombre, correo, contrasena });

    private Task<HttpResponseMessage> ReenviarAsync(string correo) =>
        _cliente.PostAsJsonAsync("/api/cuentas/reenviar-activacion", new { correo });

    private Task<HttpResponseMessage> ActivarAsync(string token) =>
        _cliente.GetAsync($"/cuentas/activar?token={Uri.EscapeDataString(token)}");

    private static async Task<string> LeerMensajeAsync(HttpResponseMessage respuesta)
    {
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        return cuerpo!["mensaje"];
    }

    private async Task<List<string>> CuerposDeCorreosParaAsync(string destinatario) =>
        (await _fabrica.LeerCorreosEnColaAsync())
            .Where(c => c.Destinatario == destinatario)
            .Select(c => c.Cuerpo)
            .ToList();

    /// <summary>El token del último correo de activación para ese destinatario, sacado del enlace.</summary>
    private async Task<string> UltimoTokenParaAsync(string destinatario)
    {
        var cuerpo = (await CuerposDeCorreosParaAsync(destinatario)).Last();
        var coincidencia = RegexEnlace().Match(cuerpo);
        Assert.True(coincidencia.Success, "El correo no contiene el enlace de activación.");
        return coincidencia.Groups[1].Value;
    }

    private Task<Usuario> UsuarioAsync(string correo) =>
        _fabrica.ConsultarBaseAsync(db => db.Usuarios.AsNoTracking().SingleAsync(u => u.Correo == correo));

    [Fact]
    public async Task El_registro_responde_201_y_crea_la_cuenta_inactiva_con_un_correo_en_la_cola()
    {
        var respuesta = await RegistrarAsync("registro.nuevo@negocio.com");

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        Assert.Equal("Te enviamos un correo para activar tu cuenta.", await LeerMensajeAsync(respuesta));

        var usuario = await UsuarioAsync("registro.nuevo@negocio.com");
        Assert.Equal(Rol.Estandar, usuario.Rol);
        Assert.False(usuario.Activo);
        Assert.Null(usuario.FechaActivacion);

        var correos = (await _fabrica.LeerCorreosEnColaAsync()).Where(c => c.Destinatario == "registro.nuevo@negocio.com").ToList();
        var correo = Assert.Single(correos);
        Assert.Equal("Activa tu cuenta de SaveStock", correo.Asunto);
        Assert.Matches(RegexEnlace(), correo.Cuerpo);
    }

    [Theory]
    [InlineData("registro.duplicado@negocio.com")]
    [InlineData("Registro.DUPLICADO@Negocio.com")]
    public async Task Un_segundo_registro_con_el_mismo_correo_responde_409(string repetido)
    {
        var correo = $"{Guid.NewGuid():N}.{repetido}";
        var primero = await RegistrarAsync(correo.ToLowerInvariant());
        Assert.Equal(HttpStatusCode.Created, primero.StatusCode);

        var segundo = await RegistrarAsync(correo, nombre: "Otra persona");

        Assert.Equal(HttpStatusCode.Conflict, segundo.StatusCode);
        Assert.Equal("Ya existe una cuenta con ese correo.", await LeerMensajeAsync(segundo));
        var cantidad = await _fabrica.ConsultarBaseAsync(db => db.Usuarios.CountAsync(u => u.Correo == correo.ToLowerInvariant()));
        Assert.Equal(1, cantidad);
    }

    [Fact]
    public async Task La_contrasena_se_guarda_con_hash_y_distinto_para_dos_usuarios_con_la_misma()
    {
        await RegistrarAsync("hash.uno@negocio.com", "misma1234");
        await RegistrarAsync("hash.dos@negocio.com", "misma1234");

        var uno = await UsuarioAsync("hash.uno@negocio.com");
        var dos = await UsuarioAsync("hash.dos@negocio.com");
        Assert.NotEqual("misma1234", uno.HashContrasena);
        Assert.DoesNotContain("misma1234", uno.HashContrasena);
        Assert.NotEqual(uno.HashContrasena, dos.HashContrasena);
    }

    [Theory]
    [InlineData("corta1")]
    [InlineData("sololetras")]
    [InlineData("12345678")]
    public async Task Una_contrasena_debil_responde_400_con_mensaje(string contrasena)
    {
        var respuesta = await RegistrarAsync($"debil.{Guid.NewGuid():N}@negocio.com", contrasena);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("La contraseña debe tener al menos 8 caracteres e incluir letras y números.", await LeerMensajeAsync(respuesta));
    }

    [Theory]
    [InlineData("", "El correo es obligatorio.")]
    [InlineData("   ", "El correo es obligatorio.")]
    [InlineData("ana-sin-arroba", "El correo no tiene un formato válido.")]
    [InlineData("ana@sindominio", "El correo no tiene un formato válido.")]
    public async Task Un_correo_vacio_o_mal_formado_responde_400_con_mensaje_y_sin_excepcion(string correo, string mensaje)
    {
        var respuesta = await RegistrarAsync(correo);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var texto = await respuesta.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Exception", texto);
        Assert.Equal(mensaje, await LeerMensajeAsync(respuesta));
    }

    [Fact]
    public async Task El_enlace_activa_la_cuenta_una_sola_vez()
    {
        await RegistrarAsync("activar.una.vez@negocio.com");
        var token = await UltimoTokenParaAsync("activar.una.vez@negocio.com");

        var primera = await ActivarAsync(token);

        Assert.Equal(HttpStatusCode.OK, primera.StatusCode);
        Assert.Equal("text/html", primera.Content.Headers.ContentType?.MediaType);
        Assert.Equal("utf-8", primera.Content.Headers.ContentType?.CharSet);
        Assert.Contains("Tu cuenta fue activada. Ya puedes iniciar sesión.", await primera.Content.ReadAsStringAsync());
        var activado = await UsuarioAsync("activar.una.vez@negocio.com");
        Assert.True(activado.Activo);
        Assert.NotNull(activado.FechaActivacion);

        var segunda = await ActivarAsync(token);

        Assert.Equal(HttpStatusCode.BadRequest, segunda.StatusCode);
        Assert.Equal("text/html", segunda.Content.Headers.ContentType?.MediaType);
        Assert.Contains(MensajeEnlaceInvalido, await segunda.Content.ReadAsStringAsync());
        var despues = await UsuarioAsync("activar.una.vez@negocio.com");
        Assert.True(despues.Activo);
        Assert.Equal(activado.FechaActivacion, despues.FechaActivacion);
    }

    [Fact]
    public async Task Un_enlace_vencido_se_rechaza_y_la_cuenta_sigue_sin_activar()
    {
        await RegistrarAsync("activar.vencido@negocio.com");
        var token = await UltimoTokenParaAsync("activar.vencido@negocio.com");
        var usuarioId = (await UsuarioAsync("activar.vencido@negocio.com")).Id;

        // Simula que pasaron más de 24 horas: el vencimiento queda en el pasado.
        await _fabrica.ConsultarBaseAsync(db => db.TokensActivacion
            .Where(t => t.UsuarioId == usuarioId)
            .ExecuteUpdateAsync(t => t.SetProperty(x => x.FechaVencimiento, DateTime.UtcNow.AddMinutes(-1))));

        var respuesta = await ActivarAsync(token);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains(MensajeEnlaceInvalido, await respuesta.Content.ReadAsStringAsync());
        var usuario = await UsuarioAsync("activar.vencido@negocio.com");
        Assert.False(usuario.Activo);
        Assert.Null(usuario.FechaActivacion);
    }

    [Fact]
    public async Task Un_enlace_inventado_o_sin_token_se_rechaza()
    {
        var inventado = await ActivarAsync("token-inventado");
        var sinToken = await _cliente.GetAsync("/cuentas/activar");

        Assert.Equal(HttpStatusCode.BadRequest, inventado.StatusCode);
        Assert.Contains(MensajeEnlaceInvalido, await inventado.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, sinToken.StatusCode);
        Assert.Contains(MensajeEnlaceInvalido, await sinToken.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task El_reenvio_responde_igual_exista_o_no_el_correo()
    {
        await RegistrarAsync("reenvio.existe@negocio.com");

        var existe = await ReenviarAsync("reenvio.existe@negocio.com");
        var noExiste = await ReenviarAsync("reenvio.no.existe@negocio.com");

        Assert.Equal(HttpStatusCode.OK, existe.StatusCode);
        Assert.Equal(existe.StatusCode, noExiste.StatusCode);
        Assert.Equal(await existe.Content.ReadAsStringAsync(), await noExiste.Content.ReadAsStringAsync());
        Assert.Equal(MensajeReenvio, await LeerMensajeAsync(existe));
        Assert.Empty(await CuerposDeCorreosParaAsync("reenvio.no.existe@negocio.com"));
    }

    [Fact]
    public async Task El_reenvio_invalida_el_enlace_anterior_y_el_nuevo_funciona()
    {
        await RegistrarAsync("reenvio.invalida@negocio.com");
        var tokenViejo = await UltimoTokenParaAsync("reenvio.invalida@negocio.com");

        await ReenviarAsync("reenvio.invalida@negocio.com");
        Assert.Equal(2, (await CuerposDeCorreosParaAsync("reenvio.invalida@negocio.com")).Count);
        var tokenNuevo = await UltimoTokenParaAsync("reenvio.invalida@negocio.com");

        var conViejo = await ActivarAsync(tokenViejo);
        Assert.Equal(HttpStatusCode.BadRequest, conViejo.StatusCode);
        Assert.Contains(MensajeEnlaceInvalido, await conViejo.Content.ReadAsStringAsync());
        Assert.Null((await UsuarioAsync("reenvio.invalida@negocio.com")).FechaActivacion);

        var conNuevo = await ActivarAsync(tokenNuevo);
        Assert.Equal(HttpStatusCode.OK, conNuevo.StatusCode);
        Assert.True((await UsuarioAsync("reenvio.invalida@negocio.com")).Activo);
    }
}
