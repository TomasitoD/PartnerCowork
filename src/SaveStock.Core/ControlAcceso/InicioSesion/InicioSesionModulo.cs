using Microsoft.Extensions.DependencyInjection;

namespace SaveStock.Core.ControlAcceso.InicioSesion;

/// <summary>
/// Servicios de inicio de sesión, consulta del usuario actual y cierre (#16: RF-CA-03, RF-CA-07, RF-CA-18, RF-CA-19).
/// </summary>
/// <remarks>
/// La carpeta no se llama "Sesion" porque ese namespace taparía a la entidad Sesion en todo ControlAcceso.
/// </remarks>
public static class InicioSesionModulo
{
    public static IServiceCollection AddInicioSesion(this IServiceCollection services)
    {
        services.AddScoped<ServicioInicioSesion>();
        return services;
    }
}
