using Microsoft.EntityFrameworkCore;
using SaveStock.Core.ControlAcceso.Entidades;
using SaveStock.Core.Correo;

namespace SaveStock.Core.Datos;

/// <summary>
/// Base de datos del Core en SQLite (RD-09). La web y el enviador usan el mismo archivo.
/// </summary>
public class CoreDbContext : DbContext
{
    public CoreDbContext(DbContextOptions<CoreDbContext> opciones)
        : base(opciones)
    {
    }

    public DbSet<Usuario> Usuarios => Set<Usuario>();

    public DbSet<Sesion> Sesiones => Set<Sesion>();

    public DbSet<TokenActivacion> TokensActivacion => Set<TokenActivacion>();

    public DbSet<CodigoRecuperacion> CodigosRecuperacion => Set<CodigoRecuperacion>();

    public DbSet<CorreoEnCola> CorreosEnCola => Set<CorreoEnCola>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // SQLite no guarda si una fecha es UTC: al leerla la marcamos como UTC (RD-11).
        // EF Core aplica este conversor también a las fechas opcionales (DateTime?).
        configurationBuilder.Properties<DateTime>().HaveConversion<ConversorFechaUtc>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Usuario>(usuario =>
        {
            usuario.Property(u => u.Nombre).HasMaxLength(100).IsRequired();
            usuario.Property(u => u.Correo).HasMaxLength(254).IsRequired();
            usuario.HasIndex(u => u.Correo).IsUnique(); // RF-CA-01: un correo, una cuenta.
            usuario.Property(u => u.HashContrasena).IsRequired();
            usuario.Property(u => u.Rol).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<Sesion>(sesion =>
        {
            sesion.Property(s => s.HashToken).HasMaxLength(64).IsRequired();
            sesion.HasIndex(s => s.HashToken).IsUnique();
            sesion.HasOne(s => s.Usuario).WithMany().HasForeignKey(s => s.UsuarioId);
        });

        modelBuilder.Entity<TokenActivacion>(token =>
        {
            token.Property(t => t.HashToken).HasMaxLength(64).IsRequired();
            token.HasIndex(t => t.HashToken).IsUnique();
            token.HasOne(t => t.Usuario).WithMany().HasForeignKey(t => t.UsuarioId);
        });

        modelBuilder.Entity<CodigoRecuperacion>(codigo =>
        {
            codigo.Property(c => c.HashCodigo).HasMaxLength(64).IsRequired();
            codigo.Property(c => c.Origen).HasConversion<string>().HasMaxLength(20);
            codigo.HasOne(c => c.Usuario).WithMany().HasForeignKey(c => c.UsuarioId);
        });

        modelBuilder.Entity<CorreoEnCola>(correo =>
        {
            correo.Property(c => c.Destinatario).HasMaxLength(254).IsRequired();
            correo.Property(c => c.Asunto).HasMaxLength(200).IsRequired();
            correo.Property(c => c.Cuerpo).IsRequired();
            correo.Property(c => c.Estado).HasConversion<string>().HasMaxLength(20);
            correo.Property(c => c.UltimoError).HasMaxLength(500);
            correo.HasIndex(c => c.Estado);
        });
    }
}
