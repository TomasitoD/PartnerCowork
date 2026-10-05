using SaveStock.Core.ControlAcceso.Autorizacion;
using SaveStock.Web.Filtros;

namespace SaveStock.Web.Endpoints;

public record RespuestaSalud(string Estado);

public static class SaludEndpoints
{
    /// <summary>GET /api/salud: responde si la aplicación está en pie. Es pública.</summary>
    public static IEndpointRouteBuilder MapSaludEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/salud", () => Results.Ok(new RespuestaSalud("ok")))
            .RequiereOperacion(Operacion.Salud);

        return app;
    }
}
