using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SaveStock.Core.ControlAcceso.Entidades;
using SaveStock.Core.ControlAcceso.Seguridad;
using SaveStock.Core.ControlAcceso.Sesiones;
using SaveStock.Core.Correo;

namespace SaveStock.Tests.Integracion;

public partial class ContrasenasTests : IClassFixture<SaveStockFactory>
{
    private const string ContrasenaInicial = "clave1234";
    private const string ContrasenaNueva = "nueva5678";

    private readonly SaveStockFactory _fabrica;

    public ContrasenasTests(SaveStockFactory fabrica)
    {
        _fabrica = fabrica;
    }

    [GeneratedRegex(@"Código: ([A-Z2-9]{8})")]
    private static partial Regex PatronCodigo();

    private static async Task<string> LeerMensajeAsync(HttpResponseMessage respuesta)
    {
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        return cuerpo!["mensaje"];
    }

    private Task<Usuario> CrearUsuarioAsync(string correo, Rol rol = Rol.Estandar) =>
        _fabrica.CrearUsuarioAsync("Usuario", correo, ContrasenaInicial, rol);

    private Task<string> LeerHashAsync(int usuarioId) =>
        _fabrica.ConsultarBaseAsync(db => db.Usuarios.AsNoTracking().Where(u => u.Id == usuarioId).Select(u => u.HashContrasena).SingleAsync());

    private async Task<List<CorreoEnCola>> CorreosParaAsync(string correo) =>
        (await _fabrica.LeerCorreosEnColaAsync()).Where(c => c.Destinatario == correo).ToList();

    private async Task<string> CrearSesionAsync(Usuario usuario)
    {
        using var alcance = _fabrica.Services.CreateScope();
        return (await alcance.ServiceProvider.GetRequiredService<GestorSesiones>().CrearAsync(usuario)).Token;
    }

    private async Task<bool> SesionValidaAsync(string token)
    {
        using var alcance = _fabrica.Services.CreateScope();
        return await alcance.ServiceProvider.GetRequiredService<GestorSesiones>().ValidarAsync(token) is not null;
    }

    private HttpClient ClienteConToken(string token)
    {
        var cliente = _fabrica.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return cliente;
    }

    /// <summary>Pide la recuperación por la API y devuelve el código que quedó en el último correo encolado.</summary>
    private async Task<string> PedirCodigoAsync(string correo)
    {
        var respuesta = await _fabrica.CreateClient().PostAsJsonAsync("/api/contrasena/recuperar", new { correo });
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return PatronCodigo().Match((await CorreosParaAsync(correo)).Last().Cuerpo).Groups[1].Value;
    }

    private Task<HttpResponseMessage> RestablecerAsync(string correo, string codigo, string contrasenaNueva) =>
        _fabrica.CreateClient().PostAsJsonAsync("/api/contrasena/restablecer", new { correo, codigo, contrasenaNueva });

    [Fact]
    public async Task Recuperar_responde_identico_exista_o_no_el_correo_y_solo_encola_si_existe()
    {
        await CrearUsuarioAsync("rec.existe@negocio.com");
        var cliente = _fabrica.CreateClient();

        var existente = await cliente.PostAsJsonAsync("/api/contrasena/recuperar", new { correo = "rec.existe@negocio.com" });
        var inexistente = await cliente.PostAsJsonAsync("/api/contrasena/recuperar", new { correo = "rec.noexiste@negocio.com" });

        Assert.Equal(HttpStatusCode.OK, existente.StatusCode);
        Assert.Equal(existente.StatusCode, inexistente.StatusCode);
        Assert.Equal(await existente.Content.ReadAsStringAsync(), await inexistente.Content.ReadAsStringAsync());
        Assert.Equal("Si el correo está registrado, te enviamos un código para restablecer tu contraseña.", await LeerMensajeAsync(existente));

        var correo = Assert.Single(await CorreosParaAsync("rec.existe@negocio.com"));
        Assert.Equal("Código para restablecer tu contraseña de SaveStock", correo.Asunto);
        Assert.Matches(PatronCodigo(), correo.Cuerpo);
        Assert.Empty(await CorreosParaAsync("rec.noexiste@negocio.com"));
    }

    [Fact]
    public async Task Restablecer_con_un_codigo_valido_cambia_la_contrasena_y_la_anterior_deja_de_servir()
    {
        var usuario = await CrearUsuarioAsync("rest.valido@negocio.com");
        var codigo = await PedirCodigoAsync("rest.valido@negocio.com");

        var respuesta = await RestablecerAsync("rest.valido@negocio.com", codigo, ContrasenaNueva);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var hash = await LeerHashAsync(usuario.Id);
        Assert.True(HasherContrasenas.Verificar(ContrasenaNueva, hash));
        Assert.False(HasherContrasenas.Verificar(ContrasenaInicial, hash));
    }

    [Fact]
    public async Task Reusar_el_codigo_da_400_y_la_contrasena_no_cambia()
    {
        var usuario = await CrearUsuarioAsync("rest.reuso@negocio.com");
        var codigo = await PedirCodigoAsync("rest.reuso@negocio.com");
        await RestablecerAsync("rest.reuso@negocio.com", codigo, ContrasenaNueva);

        var segundaVez = await RestablecerAsync("rest.reuso@negocio.com", codigo, "otra9999");

        Assert.Equal(HttpStatusCode.BadRequest, segundaVez.StatusCode);
        Assert.Equal("El código no es válido o ya venció.", await LeerMensajeAsync(segundaVez));
        var hash = await LeerHashAsync(usuario.Id);
        Assert.True(HasherContrasenas.Verificar(ContrasenaNueva, hash));
        Assert.False(HasherContrasenas.Verificar("otra9999", hash));
    }

    [Fact]
    public async Task Un_codigo_vencido_da_400_y_la_contrasena_no_cambia()
    {
        var usuario = await CrearUsuarioAsync("rest.vencido@negocio.com");
        var codigo = await PedirCodigoAsync("rest.vencido@negocio.com");
        await _fabrica.ConsultarBaseAsync(db => db.CodigosRecuperacion
            .Where(c => c.UsuarioId == usuario.Id)
            .ExecuteUpdateAsync(c => c.SetProperty(x => x.FechaVencimiento, DateTime.UtcNow.AddMinutes(-1))));

        var respuesta = await RestablecerAsync("rest.vencido@negocio.com", codigo, ContrasenaNueva);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("El código no es válido o ya venció.", await LeerMensajeAsync(respuesta));
        Assert.True(HasherContrasenas.Verificar(ContrasenaInicial, await LeerHashAsync(usuario.Id)));
    }

    [Fact]
    public async Task Las_sesiones_emitidas_antes_del_restablecimiento_dejan_de_valer()
    {
        var usuario = await CrearUsuarioAsync("rest.sesion@negocio.com");
        var token = await CrearSesionAsync(usuario);
        Assert.True(await SesionValidaAsync(token));

        var codigo = await PedirCodigoAsync("rest.sesion@negocio.com");
        await RestablecerAsync("rest.sesion@negocio.com", codigo, ContrasenaNueva);

        Assert.False(await SesionValidaAsync(token));
        var conTokenViejo = await ClienteConToken(token).PutAsJsonAsync("/api/contrasena/cambiar", new { contrasenaActual = ContrasenaNueva, contrasenaNueva = "otra9999" });
        Assert.Equal(HttpStatusCode.Unauthorized, conTokenViejo.StatusCode);
    }

    [Fact]
    public async Task Un_Estandar_no_puede_forzar_el_restablecimiento()
    {
        var estandar = await CrearUsuarioAsync("forzar.estandar@negocio.com");
        var otro = await CrearUsuarioAsync("forzar.otro@negocio.com");
        var cliente = await _fabrica.ClienteConSesionAsync(estandar);

        var respuesta = await cliente.PostAsync($"/api/admin/usuarios/{otro.Id}/forzar-restablecimiento", null);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        Assert.True(HasherContrasenas.Verificar(ContrasenaInicial, await LeerHashAsync(otro.Id)));
        Assert.Empty(await CorreosParaAsync("forzar.otro@negocio.com"));
    }

    [Fact]
    public async Task El_administrador_fuerza_el_restablecimiento_y_el_usuario_recibe_un_codigo_que_sirve()
    {
        var admin = await CrearUsuarioAsync("forzar.admin@negocio.com", Rol.Administrador);
        var usuario = await CrearUsuarioAsync("forzar.usuario@negocio.com");
        var tokenUsuario = await CrearSesionAsync(usuario);
        var cliente = await _fabrica.ClienteConSesionAsync(admin);

        var respuesta = await cliente.PostAsync($"/api/admin/usuarios/{usuario.Id}/forzar-restablecimiento", null);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.False(HasherContrasenas.Verificar(ContrasenaInicial, await LeerHashAsync(usuario.Id)));
        Assert.False(await SesionValidaAsync(tokenUsuario));

        var correo = Assert.Single(await CorreosParaAsync("forzar.usuario@negocio.com"));
        Assert.Equal("Un administrador restableció tu contraseña de SaveStock", correo.Asunto);
        var codigo = PatronCodigo().Match(correo.Cuerpo).Groups[1].Value;
        var origen = await _fabrica.ConsultarBaseAsync(db => db.CodigosRecuperacion
            .Where(c => c.UsuarioId == usuario.Id && !c.Invalidado)
            .Select(c => c.Origen)
            .SingleAsync());
        Assert.Equal(OrigenCodigoRecuperacion.Administrador, origen);

        var restablecer = await RestablecerAsync("forzar.usuario@negocio.com", codigo, ContrasenaNueva);
        Assert.Equal(HttpStatusCode.OK, restablecer.StatusCode);
        Assert.True(HasherContrasenas.Verificar(ContrasenaNueva, await LeerHashAsync(usuario.Id)));
    }

    [Fact]
    public async Task Forzar_con_un_id_inexistente_da_404()
    {
        var admin = await CrearUsuarioAsync("forzar.admin404@negocio.com", Rol.Administrador);
        var cliente = await _fabrica.ClienteConSesionAsync(admin);

        var respuesta = await cliente.PostAsync("/api/admin/usuarios/999999/forzar-restablecimiento", null);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task Cambiar_con_la_contrasena_actual_incorrecta_da_400()
    {
        var usuario = await CrearUsuarioAsync("cambiar.incorrecta@negocio.com");
        var cliente = await _fabrica.ClienteConSesionAsync(usuario);

        var respuesta = await cliente.PutAsJsonAsync("/api/contrasena/cambiar", new { contrasenaActual = "equivocada1", contrasenaNueva = ContrasenaNueva });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("La contraseña actual no es correcta.", await LeerMensajeAsync(respuesta));
        Assert.True(HasherContrasenas.Verificar(ContrasenaInicial, await LeerHashAsync(usuario.Id)));
    }

    [Fact]
    public async Task Cambiar_a_una_contrasena_debil_da_400()
    {
        var usuario = await CrearUsuarioAsync("cambiar.debil@negocio.com");
        var cliente = await _fabrica.ClienteConSesionAsync(usuario);

        var respuesta = await cliente.PutAsJsonAsync("/api/contrasena/cambiar", new { contrasenaActual = ContrasenaInicial, contrasenaNueva = "12345678" });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("La contraseña debe tener al menos 8 caracteres e incluir letras y números.", await LeerMensajeAsync(respuesta));
        Assert.True(HasherContrasenas.Verificar(ContrasenaInicial, await LeerHashAsync(usuario.Id)));
    }

    [Fact]
    public async Task Cambiar_guarda_la_nueva_y_revoca_todas_las_sesiones_incluida_la_actual()
    {
        var usuario = await CrearUsuarioAsync("cambiar.ok@negocio.com");
        var tokenActual = await CrearSesionAsync(usuario);
        var otraSesion = await CrearSesionAsync(usuario);
        var cliente = ClienteConToken(tokenActual);

        var respuesta = await cliente.PutAsJsonAsync("/api/contrasena/cambiar", new { contrasenaActual = ContrasenaInicial, contrasenaNueva = ContrasenaNueva });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("Contraseña actualizada. Inicia sesión de nuevo.", await LeerMensajeAsync(respuesta));
        var hash = await LeerHashAsync(usuario.Id);
        Assert.True(HasherContrasenas.Verificar(ContrasenaNueva, hash));
        Assert.False(HasherContrasenas.Verificar(ContrasenaInicial, hash));
        Assert.False(await SesionValidaAsync(tokenActual));
        Assert.False(await SesionValidaAsync(otraSesion));

        var otraVez = await cliente.PutAsJsonAsync("/api/contrasena/cambiar", new { contrasenaActual = ContrasenaNueva, contrasenaNueva = "otra9999" });
        Assert.Equal(HttpStatusCode.Unauthorized, otraVez.StatusCode);
    }

    [Fact]
    public async Task Cambiar_sin_sesion_da_401()
    {
        var respuesta = await _fabrica.CreateClient().PutAsJsonAsync("/api/contrasena/cambiar", new { contrasenaActual = ContrasenaInicial, contrasenaNueva = ContrasenaNueva });

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        Assert.Equal("Necesitas iniciar sesión.", await LeerMensajeAsync(respuesta));
    }
}
