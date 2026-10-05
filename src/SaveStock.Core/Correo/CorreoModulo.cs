using Microsoft.Extensions.DependencyInjection;

namespace SaveStock.Core.Correo;

/// <summary>
/// Servicios del procesamiento de la cola de correos (#14: RF-NOT-09, RF-NOT-12, RF-NOT-13).
/// La cola en sí (ICorreoCola) ya se registra en ServiciosCore.
/// </summary>
public static class CorreoModulo
{
    public static IServiceCollection AddCorreo(this IServiceCollection services)
    {
        // Pendiente (#14): registrar aquí ProcesadorColaCorreos.
        return services;
    }
}
