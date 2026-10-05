using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SaveStock.Core.Comun;

namespace SaveStock.Inventario.Datos;

/// <summary>
/// Crea el archivo del inventario y sus tablas al iniciar la aplicación, si todavía no existen
/// (los datos que ya estaban se conservan). Es el equivalente del InicializadorBaseDatos del Core,
/// pero vive en este módulo para que el Core no dependa del inventario (RD-03).
/// </summary>
public class InicializadorInventario : IHostedService
{
    private readonly IServiceProvider _servicios;

    public InicializadorInventario(IServiceProvider servicios)
    {
        _servicios = servicios;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var alcance = _servicios.CreateScope();
        var configuracion = alcance.ServiceProvider.GetRequiredService<ConfiguracionSaveStock>();
        var db = alcance.ServiceProvider.GetRequiredService<InventarioDbContext>();

        configuracion.AsegurarCarpetas();

        // V1 no usa migraciones: EnsureCreated crea las tablas solo si la base no existe.
        await db.Database.EnsureCreatedAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
