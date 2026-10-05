namespace SaveStock.Web.Endpoints;

public static class ContrasenasEndpoints
{
    /// <summary>
    /// Contraseñas (#18): POST /api/contrasena/recuperar, POST /api/contrasena/restablecer,
    /// PUT /api/contrasena/cambiar y POST /api/admin/usuarios/{id}/forzar-restablecimiento.
    /// Cada endpoint declara .RequiereOperacion(...).
    /// </summary>
    public static IEndpointRouteBuilder MapContrasenasEndpoints(this IEndpointRouteBuilder app)
    {
        // Pendiente (#18).
        return app;
    }
}
