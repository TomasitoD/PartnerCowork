using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SaveStock.Core.Comun;
using SaveStock.Core.ControlAcceso.Entidades;
using SaveStock.Core.ControlAcceso.Seguridad;

namespace SaveStock.Core.Datos;

/// <summary>
/// Prepara la base al iniciar: crea la carpeta y las tablas si no existen (los datos que ya
/// estaban se conservan, RD-09) y crea el administrador inicial desde las variables de entorno.
/// </summary>
public static class InicializadorBaseDatos
{
    public static async Task InicializarAsync(IServiceProvider servicios)
    {
        using var alcance = servicios.CreateScope();
        var proveedor = alcance.ServiceProvider;
        var configuracion = proveedor.GetRequiredService<ConfiguracionSaveStock>();
        var db = proveedor.GetRequiredService<CoreDbContext>();
        var reloj = proveedor.GetRequiredService<IReloj>();
        var logger = proveedor.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(InicializadorBaseDatos));

        configuracion.AsegurarCarpetas();

        // V1 no usa migraciones: EnsureCreated crea las tablas solo si la base no existe.
        await db.Database.EnsureCreatedAsync();

        await CrearAdministradorInicialAsync(db, configuracion, reloj, logger);
    }

    private static async Task CrearAdministradorInicialAsync(CoreDbContext db, ConfiguracionSaveStock configuracion, IReloj reloj, ILogger logger)
    {
        if (configuracion.AdminCorreo is null || configuracion.AdminContrasena is null || configuracion.AdminNombre is null)
        {
            return;
        }

        if (!ValidadorEntrada.ValidarCorreo(configuracion.AdminCorreo).Exito
            || !PoliticaContrasena.Validar(configuracion.AdminContrasena).Exito)
        {
            logger.LogWarning("No se creó el administrador inicial: SAVESTOCK_ADMIN_CORREO o SAVESTOCK_ADMIN_CONTRASENA no son válidos.");
            return;
        }

        var correo = ValidadorEntrada.NormalizarCorreo(configuracion.AdminCorreo);
        if (await db.Usuarios.AnyAsync(u => u.Correo == correo))
        {
            return;
        }

        var ahora = reloj.AhoraUtc;
        db.Usuarios.Add(new Usuario
        {
            Nombre = configuracion.AdminNombre,
            Correo = correo,
            HashContrasena = HasherContrasenas.Hashear(configuracion.AdminContrasena),
            Rol = Rol.Administrador,
            Activo = true,
            FechaActivacion = ahora,
            FechaCreacion = ahora,
        });

        await db.SaveChangesAsync();
        logger.LogInformation("Se creó el administrador inicial {Correo}.", correo);
    }
}
