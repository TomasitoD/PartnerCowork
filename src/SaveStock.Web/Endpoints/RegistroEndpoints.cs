namespace SaveStock.Web.Endpoints;

public static class RegistroEndpoints
{
    /// <summary>
    /// Registro y activación (#15): POST /api/cuentas/registro, GET /cuentas/activar,
    /// POST /api/cuentas/reenviar-activacion. Cada endpoint declara .RequiereOperacion(...).
    /// </summary>
    public static IEndpointRouteBuilder MapRegistroEndpoints(this IEndpointRouteBuilder app)
    {
        // Pendiente (#15).
        return app;
    }
}
