using System.Text;
using SaveStock.Core.ControlAcceso.Autorizacion;
using SaveStock.Core.ControlAcceso.Registro;
using SaveStock.Web.Errores;
using SaveStock.Web.Filtros;

namespace SaveStock.Web.Endpoints;

public static class RegistroEndpoints
{
    /// <summary>
    /// Registro y activación (#15): POST /api/cuentas/registro, GET /cuentas/activar,
    /// POST /api/cuentas/reenviar-activacion. Cada endpoint declara .RequiereOperacion(...).
    /// La lógica vive en <see cref="ServicioRegistro"/>; aquí solo se traduce HTTP.
    /// </summary>
    public static IEndpointRouteBuilder MapRegistroEndpoints(this IEndpointRouteBuilder app)
    {
        // RF-CA-01, RF-CA-02, RF-CA-14, RF-CA-15: 201 si se creó la cuenta, 400 o 409 si no.
        app.MapPost("/api/cuentas/registro", async (SolicitudRegistro solicitud, ServicioRegistro servicio) =>
            {
                var resultado = await servicio.RegistrarAsync(solicitud);
                return resultado.Exito
                    ? ResultadoHttp.Mensaje(resultado.Mensaje, StatusCodes.Status201Created)
                    : ResultadoHttp.Error(resultado);
            })
            .RequiereOperacion(Operacion.Registrar);

        // RF-CA-16: es el enlace que llega por correo y se abre en el navegador, por eso responde HTML.
        app.MapGet("/cuentas/activar", async (string? token, ServicioRegistro servicio) =>
            {
                var resultado = await servicio.ActivarAsync(token);
                var codigo = resultado.Exito ? StatusCodes.Status200OK : StatusCodes.Status400BadRequest;
                return Results.Content(PaginaActivacion(resultado.Mensaje), "text/html", Encoding.UTF8, codigo);
            })
            .RequiereOperacion(Operacion.ActivarCuenta);

        // RF-CA-17: siempre 200 con el mismo mensaje, exista o no el correo.
        app.MapPost("/api/cuentas/reenviar-activacion", async (SolicitudReenvioActivacion solicitud, ServicioRegistro servicio) =>
            {
                var resultado = await servicio.ReenviarActivacionAsync(solicitud);
                return ResultadoHttp.Mensaje(resultado.Mensaje);
            })
            .RequiereOperacion(Operacion.ReenviarActivacion);

        return app;
    }

    /// <summary>
    /// Página mínima con el resultado de la activación. El mensaje es siempre uno de los textos fijos
    /// de <see cref="ServicioRegistro"/>, nunca algo que mandó el usuario.
    /// </summary>
    private static string PaginaActivacion(string mensaje) => $"""
        <!DOCTYPE html>
        <html lang="es">
        <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>Activación de cuenta - SaveStock</title>
        </head>
        <body>
            <h1>SaveStock</h1>
            <p>{mensaje}</p>
            <p><a href="/">Ir a SaveStock</a></p>
        </body>
        </html>
        """;
}
