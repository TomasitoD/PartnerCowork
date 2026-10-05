using System.Net;
using System.Net.Http.Json;

namespace SaveStock.Tests.Integracion;

public class SaludTests : IClassFixture<SaveStockFactory>
{
    private readonly SaveStockFactory _fabrica;

    public SaludTests(SaveStockFactory fabrica)
    {
        _fabrica = fabrica;
    }

    [Fact]
    public async Task Salud_responde_ok_sin_sesion()
    {
        var cliente = _fabrica.CreateClient();

        var respuesta = await cliente.GetAsync("/api/salud");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.Equal("ok", cuerpo!["estado"]);
    }

    [Fact]
    public async Task La_base_temporal_de_la_prueba_se_crea_al_iniciar()
    {
        _fabrica.CreateClient();

        Assert.True(File.Exists(_fabrica.Configuracion.RutaBaseDatos));
    }
}
