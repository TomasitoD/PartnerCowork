using Microsoft.Extensions.DependencyInjection;

namespace SaveStock.Core.ControlAcceso.Registro;

/// <summary>
/// Servicios de registro y activación de cuentas (#15: RF-CA-01, RF-CA-15, RF-CA-16, RF-CA-17).
/// </summary>
public static class RegistroModulo
{
    public static IServiceCollection AddRegistro(this IServiceCollection services)
    {
        // Pendiente (#15): registrar aquí los servicios de esta feature.
        return services;
    }
}
