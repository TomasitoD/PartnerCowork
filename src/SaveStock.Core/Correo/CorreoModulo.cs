using Microsoft.Extensions.DependencyInjection;

namespace SaveStock.Core.Correo;

/// <summary>
/// Servicios del procesamiento de la cola de correos (#14: RF-NOT-09, RF-NOT-12, RF-NOT-13).
/// La cola en sí (ICorreoCola) ya se registra en ServiciosCore.
/// </summary>
public static class CorreoModulo
{
    /// <summary>
    /// Lo que necesita cualquier proceso que use el Core. La web solo encola (ICorreoCola), así que
    /// aquí no se registra nada que envíe correos: el envío nunca ocurre dentro de una operación (RF-NOT-08).
    /// </summary>
    public static IServiceCollection AddCorreo(this IServiceCollection services)
    {
        return services;
    }

    /// <summary>
    /// Registra el procesador de la cola con el enviador indicado. Solo lo llama SaveStock.Enviador,
    /// que es el proceso aparte que manda los correos (RF-NOT-09).
    /// </summary>
    public static IServiceCollection AddProcesadorColaCorreos<TEnviador>(this IServiceCollection services)
        where TEnviador : class, IEnviadorCorreo
    {
        services.AddScoped<IEnviadorCorreo, TEnviador>();
        services.AddScoped<ProcesadorColaCorreos>();
        return services;
    }
}
