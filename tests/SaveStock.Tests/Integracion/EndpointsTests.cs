using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SaveStock.Core.ControlAcceso.Autorizacion;
using SaveStock.Web.Filtros;

namespace SaveStock.Tests.Integracion;

public class EndpointsTests : IClassFixture<SaveStockFactory>
{
    private readonly SaveStockFactory _fabrica;

    public EndpointsTests(SaveStockFactory fabrica)
    {
        _fabrica = fabrica;
    }

    [Fact]
    public void Todo_endpoint_de_api_declara_su_operacion_con_RequiereOperacion()
    {
        _fabrica.CreateClient(); // Arranca la aplicación para que se registren los endpoints.
        var fuente = _fabrica.Services.GetRequiredService<EndpointDataSource>();

        var endpointsApi = fuente.Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText?.StartsWith("/api", StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        Assert.NotEmpty(endpointsApi);
        foreach (var endpoint in endpointsApi)
        {
            var operacion = endpoint.Metadata.GetMetadata<OperacionRequerida>();
            Assert.True(operacion is not null, $"{endpoint.DisplayName} no declara .RequiereOperacion(...).");
            Assert.True(Permisos.PorOperacion.ContainsKey(operacion.Operacion), $"{operacion.Operacion} no está en Permisos.");
        }
    }
}
