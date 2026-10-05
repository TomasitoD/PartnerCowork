using Microsoft.EntityFrameworkCore;
using SaveStock.Core.Datos;
using SaveStock.Inventario.OrdenesDeCompra;

namespace SaveStock.Inventario.Datos;

/// <summary>
/// Base de datos del inventario en su propio archivo SQLite (datos/inventario.db por defecto).
/// Está separada de la del Core: el Core no sabe que existe (RD-03).
/// </summary>
public class InventarioDbContext : DbContext
{
    public InventarioDbContext(DbContextOptions<InventarioDbContext> opciones)
        : base(opciones)
    {
    }

    public DbSet<OrdenDeCompra> OrdenesDeCompra => Set<OrdenDeCompra>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Igual que en el Core: las fechas se guardan en UTC y al leerlas se marcan como UTC (RD-11).
        configurationBuilder.Properties<DateTime>().HaveConversion<ConversorFechaUtc>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrdenDeCompra>(orden =>
        {
            orden.Property(o => o.Proveedor).HasMaxLength(OrdenDeCompra.LargoMaximoProveedor).IsRequired();

            // El estado se guarda como texto ("Borrador", "Enviada"...) para que la base se lea sola.
            orden.Property(o => o.Estado).HasConversion<string>().HasMaxLength(20);
            orden.HasIndex(o => o.Estado);

            // CreadaPorUsuarioId apunta a un usuario de otra base: es un número simple, sin clave foránea.
            orden.Property(o => o.CreadaPorUsuarioId).IsRequired();
        });
    }
}
