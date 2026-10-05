using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SaveStock.Core.ControlAcceso.Entidades;

namespace SaveStock.Tests.Integracion;

public class SesionTests : IClassFixture<SaveStockFactory>
{
    private const string Contrasena = "clave1234";
    private const string ContrasenaIncorrecta = "otraClave99";

    private readonly SaveStockFactory _fabrica;

    public SesionTests(SaveStockFactory fabrica)
    {
        _fabrica = fabrica;
    }

    private record RespuestaToken(string Token, DateTime ExpiraEn);

    private static async Task<string> LeerMensajeAsync(HttpResponseMessage respuesta)
    {
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        return cuerpo!["mensaje"];
    }

    private Task<HttpResponseMessage> IniciarAsync(string correo, string contrasena) =>
        _fabrica.CreateClient().PostAsJsonAsync("/api/sesion/iniciar", new { correo, contrasena });

    private async Task<string> IniciarYObtenerTokenAsync(string correo, string contrasena)
    {
        var respuesta = await IniciarAsync(correo, contrasena);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return (await respuesta.Content.ReadFromJsonAsync<RespuestaToken>())!.Token;
    }

    private HttpClient ClienteConToken(string token)
    {
        var cliente = _fabrica.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return cliente;
    }

    private async Task FallarAsync(string correo, int veces)
    {
        for (var i = 0; i < veces; i++)
        {
            var respuesta = await IniciarAsync(correo, ContrasenaIncorrecta);
            Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        }
    }

    private Task<Usuario> LeerUsuarioAsync(string correo) =>
        _fabrica.ConsultarBaseAsync(db => db.Usuarios.AsNoTracking().SingleAsync(u => u.Correo == correo));

    // RF-CA-03

    [Fact]
    public async Task Con_credenciales_correctas_devuelve_un_token_que_sirve_en_yo()
    {
        var usuario = await _fabrica.CrearUsuarioAsync("Ana", "ana.inicio@negocio.com", Contrasena, Rol.Administrador);

        var respuesta = await IniciarAsync("ana.inicio@negocio.com", Contrasena);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<RespuestaToken>();
        Assert.False(string.IsNullOrWhiteSpace(cuerpo!.Token));
        Assert.True(cuerpo.ExpiraEn > DateTime.UtcNow);

        var yo = await ClienteConToken(cuerpo.Token).GetAsync("/api/sesion/yo");

        Assert.Equal(HttpStatusCode.OK, yo.StatusCode);
        var datos = await yo.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.Equal(usuario.Id.ToString(), datos!["id"].ToString());
        Assert.Equal("Ana", datos["nombre"].ToString());
        Assert.Equal("ana.inicio@negocio.com", datos["correo"].ToString());
        Assert.Equal("Administrador", datos["rol"].ToString());
        Assert.Equal(4, datos.Count); // Nunca expone el hash de la contraseña.
    }

    [Fact]
    public async Task Contrasena_incorrecta_y_correo_inexistente_responden_exactamente_igual()
    {
        await _fabrica.CrearUsuarioAsync("Luis", "luis.igual@negocio.com", Contrasena, Rol.Estandar);

        var contrasenaMala = await IniciarAsync("luis.igual@negocio.com", ContrasenaIncorrecta);
        var correoInexistente = await IniciarAsync("nadie.igual@negocio.com", Contrasena);

        Assert.Equal(HttpStatusCode.Unauthorized, contrasenaMala.StatusCode);
        Assert.Equal(contrasenaMala.StatusCode, correoInexistente.StatusCode);
        Assert.Equal(await contrasenaMala.Content.ReadAsStringAsync(), await correoInexistente.Content.ReadAsStringAsync());
        Assert.Equal("Correo o contraseña incorrectos.", await LeerMensajeAsync(correoInexistente));
    }

    [Theory]
    [InlineData("no-es-un-correo")]
    [InlineData("")]
    public async Task Un_correo_mal_formado_da_400_controlado(string correo)
    {
        var respuesta = await IniciarAsync(correo, Contrasena);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(await LeerMensajeAsync(respuesta)));
    }

    [Fact]
    public async Task Un_json_mal_formado_da_400_controlado()
    {
        var respuesta = await _fabrica.CreateClient().PostAsync("/api/sesion/iniciar", new StringContent("{ correo", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("La solicitud no es válida.", await LeerMensajeAsync(respuesta));
    }

    [Fact]
    public async Task Una_cuenta_sin_activar_con_la_contrasena_correcta_da_403()
    {
        await _fabrica.CrearUsuarioAsync("Pedro", "pedro.sinactivar@negocio.com", Contrasena, Rol.Estandar, activado: false);

        var respuesta = await IniciarAsync("pedro.sinactivar@negocio.com", Contrasena);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        Assert.Equal("La cuenta no está activa. Revisa tu correo para activarla.", await LeerMensajeAsync(respuesta));
    }

    [Fact]
    public async Task Una_cuenta_desactivada_con_la_contrasena_correcta_da_403()
    {
        var usuario = await _fabrica.CrearUsuarioAsync("Sofia", "sofia.desactivada@negocio.com", Contrasena, Rol.Estandar);
        await _fabrica.ConsultarBaseAsync(db =>
            db.Usuarios.Where(u => u.Id == usuario.Id).ExecuteUpdateAsync(u => u.SetProperty(x => x.Activo, false)));

        var respuesta = await IniciarAsync("sofia.desactivada@negocio.com", Contrasena);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        Assert.Equal("La cuenta está desactivada. Contacta a un administrador.", await LeerMensajeAsync(respuesta));
    }

    // RF-CA-07

    [Fact]
    public async Task Yo_sin_token_da_401()
    {
        var respuesta = await _fabrica.CreateClient().GetAsync("/api/sesion/yo");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        Assert.Equal("Necesitas iniciar sesión.", await LeerMensajeAsync(respuesta));
    }

    [Fact]
    public async Task Yo_con_un_token_inventado_da_401()
    {
        var respuesta = await ClienteConToken("esto-no-es-un-token").GetAsync("/api/sesion/yo");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    // RF-CA-18

    [Fact]
    public async Task Despues_de_cerrar_sesion_el_token_da_401()
    {
        await _fabrica.CrearUsuarioAsync("Marta", "marta.cierre@negocio.com", Contrasena, Rol.Estandar);
        var token = await IniciarYObtenerTokenAsync("marta.cierre@negocio.com", Contrasena);
        var cliente = ClienteConToken(token);
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/api/sesion/yo")).StatusCode);

        var cierre = await cliente.PostAsync("/api/sesion/cerrar", null);

        Assert.Equal(HttpStatusCode.OK, cierre.StatusCode);
        Assert.Equal("Sesión cerrada.", await LeerMensajeAsync(cierre));
        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.GetAsync("/api/sesion/yo")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.PostAsync("/api/sesion/cerrar", null)).StatusCode);
    }

    [Fact]
    public async Task Cerrar_sin_sesion_da_401()
    {
        var respuesta = await _fabrica.CreateClient().PostAsync("/api/sesion/cerrar", null);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Cerrar_una_sesion_no_cierra_las_otras_del_mismo_usuario()
    {
        await _fabrica.CrearUsuarioAsync("Raul", "raul.dos@negocio.com", Contrasena, Rol.Estandar);
        var primera = await IniciarYObtenerTokenAsync("raul.dos@negocio.com", Contrasena);
        var segunda = await IniciarYObtenerTokenAsync("raul.dos@negocio.com", Contrasena);

        await ClienteConToken(primera).PostAsync("/api/sesion/cerrar", null);

        Assert.Equal(HttpStatusCode.Unauthorized, (await ClienteConToken(primera).GetAsync("/api/sesion/yo")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ClienteConToken(segunda).GetAsync("/api/sesion/yo")).StatusCode);
    }

    // RF-CA-19

    [Fact]
    public async Task Tras_5_fallos_la_contrasena_correcta_da_423()
    {
        await _fabrica.CrearUsuarioAsync("Eva", "eva.bloqueo@negocio.com", Contrasena, Rol.Estandar);
        await FallarAsync("eva.bloqueo@negocio.com", 5);

        var respuesta = await IniciarAsync("eva.bloqueo@negocio.com", Contrasena);

        Assert.Equal((HttpStatusCode)423, respuesta.StatusCode);
        Assert.Equal("La cuenta está bloqueada temporalmente por intentos fallidos. Intenta de nuevo más tarde.", await LeerMensajeAsync(respuesta));
        var usuario = await LeerUsuarioAsync("eva.bloqueo@negocio.com");
        Assert.NotNull(usuario.BloqueadoHasta);
        var minutos = (usuario.BloqueadoHasta!.Value - DateTime.UtcNow).TotalMinutes;
        Assert.InRange(minutos, 14, 15);
    }

    [Fact]
    public async Task Cuando_vence_el_bloqueo_puede_entrar_y_el_contador_queda_en_cero()
    {
        await _fabrica.CrearUsuarioAsync("Ivan", "ivan.vencido@negocio.com", Contrasena, Rol.Estandar);
        await FallarAsync("ivan.vencido@negocio.com", 5);

        // Simula que pasaron los 15 minutos: el bloqueo queda en el pasado.
        await _fabrica.ConsultarBaseAsync(db => db.Usuarios
            .Where(u => u.Correo == "ivan.vencido@negocio.com")
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.BloqueadoHasta, DateTime.UtcNow.AddMinutes(-1))));

        var respuesta = await IniciarAsync("ivan.vencido@negocio.com", Contrasena);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var usuario = await LeerUsuarioAsync("ivan.vencido@negocio.com");
        Assert.Equal(0, usuario.IntentosFallidos);
        Assert.Null(usuario.BloqueadoHasta);
    }

    [Fact]
    public async Task Un_inicio_correcto_reinicia_el_contador_de_fallos()
    {
        await _fabrica.CrearUsuarioAsync("Lia", "lia.contador@negocio.com", Contrasena, Rol.Estandar);

        await FallarAsync("lia.contador@negocio.com", 4);
        await IniciarYObtenerTokenAsync("lia.contador@negocio.com", Contrasena);
        await FallarAsync("lia.contador@negocio.com", 4);

        // 4 + éxito + 4 no son 5 fallos consecutivos: la cuenta no se bloquea.
        var usuario = await LeerUsuarioAsync("lia.contador@negocio.com");
        Assert.Null(usuario.BloqueadoHasta);
        Assert.Equal(4, usuario.IntentosFallidos);
        Assert.Equal(HttpStatusCode.OK, (await IniciarAsync("lia.contador@negocio.com", Contrasena)).StatusCode);
    }
}
