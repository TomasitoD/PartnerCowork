using SaveStock.Core.ControlAcceso.Administracion;
using SaveStock.Core.ControlAcceso.Autorizacion;
using SaveStock.Web.Filtros;

namespace SaveStock.Web.Endpoints;

public static class AdministracionEndpoints
{
    /// <summary>
    /// Administración de usuarios (#17): GET /api/admin/usuarios, PUT /api/admin/usuarios/{id}/rol,
    /// POST /api/admin/usuarios/{id}/desactivar y /reactivar. Cada endpoint declara .RequiereOperacion(...):
    /// el filtro rechaza con 401 sin sesión y con 403 a un Estándar antes de llegar al servicio (RF-CA-06).
    /// </summary>
    public static IEndpointRouteBuilder MapAdministracionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/admin/usuarios", async (ServicioAdministracionUsuarios servicio) =>
                Results.Ok(await servicio.ListarAsync()))
            .RequiereOperacion(Operacion.ListarUsuarios);

        return app;
    }
}
