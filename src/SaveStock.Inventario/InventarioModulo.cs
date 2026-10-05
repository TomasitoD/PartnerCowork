using Microsoft.Extensions.DependencyInjection;
using SaveStock.Core.Comun;

namespace SaveStock.Inventario;

/// <summary>
/// Servicios del módulo de inventario (#19). Usa su propio archivo SQLite
/// (ConfiguracionSaveStock.RutaBaseDatosInventario). El Core no conoce este proyecto (RD-03).
/// </summary>
public static class InventarioModulo
{
    public static IServiceCollection AddSaveStockInventario(this IServiceCollection services, ConfiguracionSaveStock configuracion)
    {
        // Pendiente (#19): registrar aquí InventarioDbContext y la máquina de estados.
        return services;
    }
}
