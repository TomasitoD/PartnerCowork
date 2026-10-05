namespace SaveStock.Web.Endpoints;

public static class AdministracionEndpoints
{
    /// <summary>
    /// Administración de usuarios (#17): GET /api/admin/usuarios, PUT /api/admin/usuarios/{id}/rol,
    /// POST /api/admin/usuarios/{id}/desactivar y /reactivar. Cada endpoint declara .RequiereOperacion(...).
    /// </summary>
    public static IEndpointRouteBuilder MapAdministracionEndpoints(this IEndpointRouteBuilder app)
    {
        // Pendiente (#17).
        return app;
    }
}
