using SaveStock.Core.Comun;
using SaveStock.Core.ControlAcceso.Administracion;
using SaveStock.Core.ControlAcceso.Autorizacion;
using SaveStock.Web.Errores;
using SaveStock.Web.Filtros;

namespace SaveStock.Web.Endpoints;

/// <summary>Cuerpo de PUT /api/admin/usuarios/{id}/rol. El rol llega como texto y lo valida el servicio.</summary>
public record SolicitudCambioRol(string? Rol);

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

        // El cuerpo es opcional para que una petición sin cuerpo llegue al filtro (401/403) y, si pasa,
        // el servicio la rechace con 400 por rol inválido.
        app.MapPut("/api/admin/usuarios/{id:int}/rol", async (int id, SolicitudCambioRol? solicitud, HttpContext http, ServicioAdministracionUsuarios servicio) =>
                Responder(await servicio.CambiarRolAsync(http.ObtenerUsuarioActual().Id, id, solicitud?.Rol)))
            .RequiereOperacion(Operacion.CambiarRol);

        app.MapPost("/api/admin/usuarios/{id:int}/desactivar", async (int id, HttpContext http, ServicioAdministracionUsuarios servicio) =>
                Responder(await servicio.DesactivarAsync(http.ObtenerUsuarioActual().Id, id)))
            .RequiereOperacion(Operacion.DesactivarUsuario);

        app.MapPost("/api/admin/usuarios/{id:int}/reactivar", async (int id, ServicioAdministracionUsuarios servicio) =>
                Responder(await servicio.ReactivarAsync(id)))
            .RequiereOperacion(Operacion.ReactivarUsuario);

        return app;
    }

    /// <summary>200 con el mensaje si salió bien; si no, el código que corresponde al error.</summary>
    private static IResult Responder(Resultado resultado) =>
        resultado.Exito ? ResultadoHttp.Mensaje(resultado.Mensaje) : ResultadoHttp.Error(resultado);
}
