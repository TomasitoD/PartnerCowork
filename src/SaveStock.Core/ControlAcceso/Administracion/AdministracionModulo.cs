using Microsoft.Extensions.DependencyInjection;

namespace SaveStock.Core.ControlAcceso.Administracion;

/// <summary>
/// Servicios de administración de usuarios y roles (#17: RF-CA-04, RF-CA-08, RF-CA-20, RF-CA-21).
/// </summary>
public static class AdministracionModulo
{
    public static IServiceCollection AddAdministracion(this IServiceCollection services)
    {
        // Pendiente (#17): registrar aquí los servicios de esta feature.
        return services;
    }
}
