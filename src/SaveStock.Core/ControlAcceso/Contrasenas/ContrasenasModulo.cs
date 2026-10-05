using Microsoft.Extensions.DependencyInjection;

namespace SaveStock.Core.ControlAcceso.Contrasenas;

/// <summary>
/// Servicios de recuperación, restablecimiento y cambio de contraseña (#18: RF-CA-09 a RF-CA-13, RF-CA-22).
/// </summary>
public static class ContrasenasModulo
{
    public static IServiceCollection AddContrasenas(this IServiceCollection services)
    {
        // Pendiente (#18): registrar aquí los servicios de esta feature.
        return services;
    }
}
