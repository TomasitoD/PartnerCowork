using System.Net;
using System.Net.Http.Json;
using System.Text;
using SaveStock.Core.ControlAcceso.Entidades;

namespace SaveStock.Tests.Integracion;

public class ErroresYPermisosTests : IClassFixture<SaveStockFactoryConEndpointsDePrueba>
{
    private readonly SaveStockFactoryConEndpointsDePrueba _fabrica;

    public ErroresYPermisosTests(SaveStockFactoryConEndpointsDePrueba fabrica)
    {
        _fabrica = fabrica;
    }

    private static async Task<string> LeerMensajeAsync(HttpResponseMessage respuesta)
    {
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        return cuerpo!["mensaje"];
    }

    [Theory]
    [InlineData("{ esto no es json")]
    [InlineData("{\"nombre\": \"Ana\", \"cantidad\": \"no es un número\"}")]
    public async Task Un_json_mal_formado_o_con_tipo_incorrecto_da_400_con_mensaje_y_sin_traza(string json)
    {
        var cliente = _fabrica.CreateClient();

        var respuesta = await cliente.PostAsync("/prueba/json", new StringContent(json, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var texto = await respuesta.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Exception", texto);
        Assert.DoesNotContain(" at ", texto);
        Assert.Equal("La solicitud no es válida.", await LeerMensajeAsync(respuesta));
    }

    [Fact]
    public async Task Una_excepcion_inesperada_da_500_sin_detalles_internos()
    {
        var cliente = _fabrica.CreateClient();

        var respuesta = await cliente.GetAsync("/prueba/error");

        Assert.Equal(HttpStatusCode.InternalServerError, respuesta.StatusCode);
        var texto = await respuesta.Content.ReadAsStringAsync();
        Assert.DoesNotContain("SELECT", texto);
        Assert.DoesNotContain("InvalidOperationException", texto);
        Assert.Equal("Ocurrió un error inesperado. Intenta de nuevo.", await LeerMensajeAsync(respuesta));
    }

    [Fact]
    public async Task Sin_sesion_una_operacion_autenticada_da_401()
    {
        var cliente = _fabrica.CreateClient();

        var respuesta = await cliente.GetAsync("/prueba/autenticado");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        Assert.Equal("Necesitas iniciar sesión.", await LeerMensajeAsync(respuesta));
    }

    [Fact]
    public async Task Con_un_token_inventado_da_401()
    {
        var cliente = _fabrica.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", "token-inventado");

        var respuesta = await cliente.GetAsync("/prueba/autenticado");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Con_sesion_el_endpoint_recibe_el_usuario_actual()
    {
        var usuario = await _fabrica.CrearUsuarioAsync("Ana", "ana.sesion@negocio.com", "clave1234", Rol.Estandar);
        var cliente = await _fabrica.ClienteConSesionAsync(usuario);

        var respuesta = await cliente.GetAsync("/prueba/autenticado");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.Equal("ana.sesion@negocio.com", cuerpo!["correo"]);
    }

    [Fact]
    public async Task Un_Estandar_en_una_operacion_de_Administrador_da_403()
    {
        var usuario = await _fabrica.CrearUsuarioAsync("Luis", "luis.estandar@negocio.com", "clave1234", Rol.Estandar);
        var cliente = await _fabrica.ClienteConSesionAsync(usuario);

        var respuesta = await cliente.GetAsync("/prueba/administrador");

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        Assert.Equal("No tienes permiso para realizar esta operación.", await LeerMensajeAsync(respuesta));
    }

    [Fact]
    public async Task Un_Administrador_en_una_operacion_de_Administrador_pasa()
    {
        var usuario = await _fabrica.CrearUsuarioAsync("Marta", "marta.admin@negocio.com", "clave1234", Rol.Administrador);
        var cliente = await _fabrica.ClienteConSesionAsync(usuario);

        var respuesta = await cliente.GetAsync("/prueba/administrador");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Fact]
    public async Task Un_usuario_sin_activar_no_tiene_sesion_valida()
    {
        var usuario = await _fabrica.CrearUsuarioAsync("Pedro", "pedro.inactivo@negocio.com", "clave1234", Rol.Estandar, activado: false);
        var cliente = await _fabrica.ClienteConSesionAsync(usuario);

        var respuesta = await cliente.GetAsync("/prueba/autenticado");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task La_cola_de_correos_empieza_vacia()
    {
        _fabrica.CreateClient();

        Assert.Empty(await _fabrica.LeerCorreosEnColaAsync());
    }
}
