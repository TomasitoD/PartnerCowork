using SaveStock.Core.ControlAcceso.Autorizacion;
using SaveStock.Core.ControlAcceso.Entidades;
using SaveStock.Core.ControlAcceso.InicioSesion;
using SaveStock.Web.Errores;
using SaveStock.Web.Filtros;

namespace SaveStock.Web.Endpoints;

/// <summary>Cuerpo de POST /api/sesion/iniciar.</summary>
public record SolicitudInicioSesion(string? Correo, string? Contrasena);

/// <summary>Respuesta de un inicio de sesión correcto: el token y cuándo vence (UTC).</summary>
public record RespuestaInicioSesion(string Token, DateTime ExpiraEn);

/// <summary>Datos del usuario autenticado. Nunca incluye el hash de la contraseña.</summary>
public record RespuestaUsuarioActual(int Id, string Nombre, string Correo, Rol Rol);

public static class SesionEndpoints
{
    /// <summary>
    /// Sesión (#16): POST /api/sesion/iniciar, GET /api/sesion/yo, POST /api/sesion/cerrar.
    /// Cada endpoint declara .RequiereOperacion(...).
    /// </summary>
    public static IEndpointRouteBuilder MapSesionEndpoints(this IEndpointRouteBuilder app)
    {
        // RF-CA-03, RF-CA-19: la lógica (credenciales, bloqueo, estado de la cuenta) está en el servicio del Core.
        app.MapPost("/api/sesion/iniciar", async (SolicitudInicioSesion solicitud, ServicioInicioSesion servicio) =>
        {
            var resultado = await servicio.IniciarAsync(solicitud.Correo, solicitud.Contrasena);
            return resultado.Exito
                ? Results.Ok(new RespuestaInicioSesion(resultado.Valor!.Token, resultado.Valor.FechaVencimiento))
                : ResultadoHttp.Error(resultado);
        })
            .RequiereOperacion(Operacion.IniciarSesion);

        // RF-CA-07: sin sesión válida el filtro responde 401 antes de llegar aquí.
        app.MapGet("/api/sesion/yo", (HttpContext http) =>
        {
            var usuario = http.ObtenerUsuarioActual();
            return Results.Ok(new RespuestaUsuarioActual(usuario.Id, usuario.Nombre, usuario.Correo, usuario.Rol));
        })
            .RequiereOperacion(Operacion.ConsultarUsuarioActual);

        // RF-CA-18: revoca la sesión del token con el que se llamó. Después, ese token da 401.
        app.MapPost("/api/sesion/cerrar", async (HttpContext http, ServicioInicioSesion servicio) =>
        {
            var resultado = await servicio.CerrarAsync(http.ObtenerTokenActual());
            return ResultadoHttp.Mensaje(resultado.Mensaje);
        })
            .RequiereOperacion(Operacion.CerrarSesion);

        return app;
    }
}
