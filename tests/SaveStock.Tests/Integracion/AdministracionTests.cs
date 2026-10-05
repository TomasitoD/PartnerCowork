using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SaveStock.Core.ControlAcceso.Entidades;
using SaveStock.Core.ControlAcceso.Sesiones;

namespace SaveStock.Tests.Integracion;

public class AdministracionTests : IClassFixture<SaveStockFactory>
{
    private const string MensajeSinPermiso = "No tienes permiso para realizar esta operación.";

    private readonly SaveStockFactory _fabrica;

    public AdministracionTests(SaveStockFactory fabrica)
    {
        _fabrica = fabrica;
    }

    /// <summary>Cada prueba usa correos propios porque la base se comparte dentro de la clase.</summary>
    private static string Correo(string prefijo) => $"{prefijo}.{Guid.NewGuid():N}@negocio.com";

    private Task<Usuario> CrearAsync(string prefijo, Rol rol) =>
        _fabrica.CrearUsuarioAsync("Usuario " + prefijo, Correo(prefijo), "clave1234", rol);

    private Task<Usuario> LeerUsuarioAsync(int id) =>
        _fabrica.ConsultarBaseAsync(db => db.Usuarios.AsNoTracking().SingleAsync(u => u.Id == id));

    private static async Task<string> LeerMensajeAsync(HttpResponseMessage respuesta)
    {
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        return cuerpo!["mensaje"];
    }

    private static HttpRequestMessage Peticion(HttpMethod metodo, string ruta, string? json = null) => new(metodo, ruta)
    {
        Content = json is null ? null : new StringContent(json, Encoding.UTF8, "application/json"),
    };

    /// <summary>Todas las operaciones de administración de este módulo, contra el usuario indicado.</summary>
    private static IEnumerable<HttpRequestMessage> TodasLasPeticionesDeAdministracion(int idObjetivo) =>
    [
        Peticion(HttpMethod.Get, "/api/admin/usuarios"),
        Peticion(HttpMethod.Put, $"/api/admin/usuarios/{idObjetivo}/rol", "{\"rol\":\"Administrador\"}"),
        Peticion(HttpMethod.Post, $"/api/admin/usuarios/{idObjetivo}/desactivar"),
        Peticion(HttpMethod.Post, $"/api/admin/usuarios/{idObjetivo}/reactivar"),
    ];

    // RF-CA-06 / RD-06: el rechazo es del servidor, aunque la petición se arme a mano.

    [Fact]
    public async Task Un_Estandar_recibe_403_en_cada_endpoint_de_administracion_y_nada_cambia()
    {
        var estandar = await CrearAsync("estandar", Rol.Estandar);
        var objetivo = await CrearAsync("objetivo", Rol.Estandar);
        using var cliente = await _fabrica.ClienteConSesionAsync(estandar);

        foreach (var peticion in TodasLasPeticionesDeAdministracion(objetivo.Id))
        {
            var respuesta = await cliente.SendAsync(peticion);

            Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
            Assert.Equal(MensajeSinPermiso, await LeerMensajeAsync(respuesta));
        }

        var objetivoDespues = await LeerUsuarioAsync(objetivo.Id);
        Assert.Equal(Rol.Estandar, objetivoDespues.Rol);
        Assert.True(objetivoDespues.Activo);
        Assert.Equal(Rol.Estandar, (await LeerUsuarioAsync(estandar.Id)).Rol);
    }

    [Fact]
    public async Task Sin_sesion_cada_endpoint_de_administracion_da_401()
    {
        var objetivo = await CrearAsync("objetivo", Rol.Estandar);
        using var cliente = _fabrica.CreateClient();

        foreach (var peticion in TodasLasPeticionesDeAdministracion(objetivo.Id))
        {
            var respuesta = await cliente.SendAsync(peticion);

            Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
            Assert.Equal("Necesitas iniciar sesión.", await LeerMensajeAsync(respuesta));
        }

        Assert.True((await LeerUsuarioAsync(objetivo.Id)).Activo);
    }

    // RF-CA-08: el cambio de rol es solo del Administrador.

    [Theory]
    [InlineData("Administrador")]
    [InlineData("Estandar")]
    public async Task Un_Estandar_no_puede_cambiar_ningun_rol_ni_el_propio(string rol)
    {
        var estandar = await CrearAsync("estandar", Rol.Estandar);
        var otro = await CrearAsync("otro", Rol.Administrador);
        using var cliente = await _fabrica.ClienteConSesionAsync(estandar);

        foreach (var id in new[] { estandar.Id, otro.Id })
        {
            var respuesta = await cliente.PutAsJsonAsync($"/api/admin/usuarios/{id}/rol", new { rol });

            Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
            Assert.Equal(MensajeSinPermiso, await LeerMensajeAsync(respuesta));
        }

        Assert.Equal(Rol.Estandar, (await LeerUsuarioAsync(estandar.Id)).Rol);
        Assert.Equal(Rol.Administrador, (await LeerUsuarioAsync(otro.Id)).Rol);
    }

    [Fact]
    public async Task El_administrador_cambia_el_rol_de_otro_y_queda_guardado()
    {
        var admin = await CrearAsync("admin", Rol.Administrador);
        var otro = await CrearAsync("otro", Rol.Estandar);
        using var cliente = await _fabrica.ClienteConSesionAsync(admin);

        var respuesta = await cliente.PutAsJsonAsync($"/api/admin/usuarios/{otro.Id}/rol", new { rol = "Administrador" });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal(Rol.Administrador, (await LeerUsuarioAsync(otro.Id)).Rol);

        var deVuelta = await cliente.PutAsJsonAsync($"/api/admin/usuarios/{otro.Id}/rol", new { rol = "Estandar" });

        Assert.Equal(HttpStatusCode.OK, deVuelta.StatusCode);
        Assert.Equal(Rol.Estandar, (await LeerUsuarioAsync(otro.Id)).Rol);
    }

    [Fact]
    public async Task El_administrador_no_puede_cambiar_su_propio_rol()
    {
        var admin = await CrearAsync("admin", Rol.Administrador);
        using var cliente = await _fabrica.ClienteConSesionAsync(admin);

        var respuesta = await cliente.PutAsJsonAsync($"/api/admin/usuarios/{admin.Id}/rol", new { rol = "Estandar" });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("No puedes cambiar tu propio rol.", await LeerMensajeAsync(respuesta));
        Assert.Equal(Rol.Administrador, (await LeerUsuarioAsync(admin.Id)).Rol);
    }

    [Theory]
    [InlineData("{\"rol\":\"SuperAdmin\"}")]
    [InlineData("{\"rol\":\"\"}")]
    [InlineData("{}")]
    [InlineData(null)] // Sin cuerpo.
    public async Task Un_rol_invalido_da_400_y_no_cambia_nada(string? json)
    {
        var admin = await CrearAsync("admin", Rol.Administrador);
        var otro = await CrearAsync("otro", Rol.Estandar);
        using var cliente = await _fabrica.ClienteConSesionAsync(admin);

        var respuesta = await cliente.SendAsync(Peticion(HttpMethod.Put, $"/api/admin/usuarios/{otro.Id}/rol", json));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("El rol no es válido. Usa \"Administrador\" o \"Estandar\".", await LeerMensajeAsync(respuesta));
        Assert.Equal(Rol.Estandar, (await LeerUsuarioAsync(otro.Id)).Rol);
    }

    [Fact]
    public async Task Un_id_inexistente_da_404_en_rol_desactivar_y_reactivar()
    {
        var admin = await CrearAsync("admin", Rol.Administrador);
        using var cliente = await _fabrica.ClienteConSesionAsync(admin);

        var peticiones = TodasLasPeticionesDeAdministracion(999_999).Skip(1); // El listado no lleva id.
        foreach (var peticion in peticiones)
        {
            var respuesta = await cliente.SendAsync(peticion);

            Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
            Assert.Equal("No existe un usuario con ese id.", await LeerMensajeAsync(respuesta));
        }
    }

    // RF-CA-20: desactivar y reactivar.

    [Fact]
    public async Task Desactivar_a_un_usuario_invalida_su_sesion_abierta()
    {
        var admin = await CrearAsync("admin", Rol.Administrador);
        var otro = await CrearAsync("otro", Rol.Administrador);
        string tokenDelOtro;
        using (var alcance = _fabrica.Services.CreateScope())
        {
            tokenDelOtro = (await alcance.ServiceProvider.GetRequiredService<GestorSesiones>().CrearAsync(otro)).Token;
        }

        using var cliente = await _fabrica.ClienteConSesionAsync(admin);

        var respuesta = await cliente.PostAsync($"/api/admin/usuarios/{otro.Id}/desactivar", null);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.False((await LeerUsuarioAsync(otro.Id)).Activo);

        // Su token ya no sirve: GestorSesiones lo rechaza y el filtro responde 401.
        using (var alcance = _fabrica.Services.CreateScope())
        {
            Assert.Null(await alcance.ServiceProvider.GetRequiredService<GestorSesiones>().ValidarAsync(tokenDelOtro));
        }

        using var clienteDelOtro = _fabrica.CreateClient();
        clienteDelOtro.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenDelOtro);
        var conTokenViejo = await clienteDelOtro.GetAsync("/api/admin/usuarios");
        Assert.Equal(HttpStatusCode.Unauthorized, conTokenViejo.StatusCode);

        var abiertas = await _fabrica.ConsultarBaseAsync(db =>
            db.Sesiones.CountAsync(s => s.UsuarioId == otro.Id && s.FechaRevocacion == null));
        Assert.Equal(0, abiertas);
    }

    [Fact]
    public async Task El_administrador_no_puede_desactivarse_a_si_mismo()
    {
        var admin = await CrearAsync("admin", Rol.Administrador);
        using var cliente = await _fabrica.ClienteConSesionAsync(admin);

        var respuesta = await cliente.PostAsync($"/api/admin/usuarios/{admin.Id}/desactivar", null);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("No puedes desactivar tu propia cuenta.", await LeerMensajeAsync(respuesta));
        Assert.True((await LeerUsuarioAsync(admin.Id)).Activo);

        // Su sesión sigue sirviendo.
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/api/admin/usuarios")).StatusCode);
    }

    [Fact]
    public async Task El_administrador_reactiva_a_un_usuario_desactivado()
    {
        var admin = await CrearAsync("admin", Rol.Administrador);
        var otro = await CrearAsync("otro", Rol.Estandar);
        using var cliente = await _fabrica.ClienteConSesionAsync(admin);
        await cliente.PostAsync($"/api/admin/usuarios/{otro.Id}/desactivar", null);

        var respuesta = await cliente.PostAsync($"/api/admin/usuarios/{otro.Id}/reactivar", null);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.True((await LeerUsuarioAsync(otro.Id)).Activo);

        // Ya reactivado, una sesión nueva vuelve a ser válida.
        using var clienteDelOtro = await _fabrica.ClienteConSesionAsync(await LeerUsuarioAsync(otro.Id));
        var conSesionNueva = await clienteDelOtro.GetAsync("/api/admin/usuarios");
        Assert.Equal(HttpStatusCode.Forbidden, conSesionNueva.StatusCode); // Válida (no 401), pero es Estándar.
    }

    // RF-CA-21: listado.

    [Fact]
    public async Task El_listado_trae_rol_y_estado_y_nunca_hashes_ni_tokens()
    {
        var admin = await CrearAsync("admin", Rol.Administrador);
        var otro = await CrearAsync("otro", Rol.Estandar);
        using var cliente = await _fabrica.ClienteConSesionAsync(admin);

        var respuesta = await cliente.GetAsync("/api/admin/usuarios");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var texto = await respuesta.Content.ReadAsStringAsync();
        Assert.DoesNotContain("hash", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("contrasena", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("pbkdf2", texto, StringComparison.OrdinalIgnoreCase);

        using var json = JsonDocument.Parse(texto);
        var delOtro = json.RootElement.EnumerateArray().Single(u => u.GetProperty("id").GetInt32() == otro.Id);
        var campos = delOtro.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(["activo", "correo", "cuentaActivada", "id", "nombre", "rol"], campos);
        Assert.Equal(otro.Correo, delOtro.GetProperty("correo").GetString());
        Assert.Equal("Estandar", delOtro.GetProperty("rol").GetString());
        Assert.True(delOtro.GetProperty("activo").GetBoolean());
        Assert.True(delOtro.GetProperty("cuentaActivada").GetBoolean());
    }
}
