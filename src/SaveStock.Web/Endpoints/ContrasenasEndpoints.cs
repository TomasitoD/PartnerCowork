using SaveStock.Core.Comun;
using SaveStock.Core.ControlAcceso.Autorizacion;
using SaveStock.Core.ControlAcceso.Contrasenas;
using SaveStock.Web.Errores;
using SaveStock.Web.Filtros;

namespace SaveStock.Web.Endpoints;

public static class ContrasenasEndpoints
{
    /// <summary>
    /// Contraseñas (#18): POST /api/contrasena/recuperar, POST /api/contrasena/restablecer,
    /// PUT /api/contrasena/cambiar y POST /api/admin/usuarios/{id}/forzar-restablecimiento.
    /// Cada endpoint declara .RequiereOperacion(...). La lógica vive en ServicioContrasenas.
    /// </summary>
    public static IEndpointRouteBuilder MapContrasenasEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/contrasena/recuperar", async (SolicitudRecuperacion solicitud, ServicioContrasenas servicio) =>
                Responder(await servicio.IniciarRecuperacionAsync(solicitud)))
            .RequiereOperacion(Operacion.IniciarRecuperacion);

        app.MapPost("/api/contrasena/restablecer", async (SolicitudRestablecimiento solicitud, ServicioContrasenas servicio) =>
                Responder(await servicio.RestablecerAsync(solicitud)))
            .RequiereOperacion(Operacion.RestablecerContrasena);

        // El usuario sale de la sesión que validó el filtro, nunca del cuerpo de la petición.
        app.MapPut("/api/contrasena/cambiar", async (SolicitudCambioContrasena solicitud, HttpContext http, ServicioContrasenas servicio) =>
                Responder(await servicio.CambiarAsync(http.ObtenerUsuarioActual().Id, solicitud)))
            .RequiereOperacion(Operacion.CambiarContrasena);

        return app;
    }

    private static IResult Responder(Resultado resultado) =>
        resultado.Exito ? ResultadoHttp.Mensaje(resultado.Mensaje) : ResultadoHttp.Error(resultado);
}
