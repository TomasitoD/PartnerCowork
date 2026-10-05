using Microsoft.AspNetCore.Diagnostics;

namespace SaveStock.Web.Errores;

/// <summary>
/// Manejador global de excepciones (RD-07, RD-08). El usuario nunca ve trazas, rutas ni consultas:
/// el detalle queda solo en el log del servidor.
/// </summary>
public static class ManejoErrores
{
    public const string MensajeErrorInesperado = "Ocurrió un error inesperado. Intenta de nuevo.";
    public const string MensajeSolicitudInvalida = "La solicitud no es válida.";

    public static async Task ResponderErrorAsync(HttpContext contexto)
    {
        var error = contexto.Features.Get<IExceptionHandlerFeature>()?.Error;

        // JSON mal formado, tipo incorrecto o cuerpo ausente: ASP.NET Core lanza BadHttpRequestException
        // porque activamos ThrowOnBadRequest en Program.cs (RD-07).
        if (error is BadHttpRequestException solicitudInvalida)
        {
            contexto.Response.StatusCode = solicitudInvalida.StatusCode;
            await contexto.Response.WriteAsJsonAsync(new RespuestaMensaje(MensajeSolicitudInvalida));
            return;
        }

        contexto.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await contexto.Response.WriteAsJsonAsync(new RespuestaMensaje(MensajeErrorInesperado));
    }
}
