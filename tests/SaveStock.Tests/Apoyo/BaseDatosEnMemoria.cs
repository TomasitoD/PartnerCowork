using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SaveStock.Core.ControlAcceso.Entidades;
using SaveStock.Core.ControlAcceso.Seguridad;
using SaveStock.Core.Datos;

namespace SaveStock.Tests.Apoyo;

/// <summary>
/// Base SQLite en memoria para probar servicios del Core sin levantar la web (RD-12).
/// Vive mientras la conexión esté abierta; cada prueba crea la suya.
/// </summary>
public sealed class BaseDatosEnMemoria : IDisposable
{
    private readonly SqliteConnection _conexion;

    public BaseDatosEnMemoria()
    {
        _conexion = new SqliteConnection("Data Source=:memory:");
        _conexion.Open();

        using var db = CrearContexto();
        db.Database.EnsureCreated();
    }

    /// <summary>Un contexto nuevo sobre la misma base (útil para leer lo que guardó otro contexto).</summary>
    public CoreDbContext CrearContexto()
    {
        var opciones = new DbContextOptionsBuilder<CoreDbContext>().UseSqlite(_conexion).Options;
        return new CoreDbContext(opciones);
    }

    /// <summary>Guarda un usuario activado con la contraseña indicada y lo devuelve.</summary>
    public async Task<Usuario> CrearUsuarioAsync(string correo, string contrasena = "clave1234", Rol rol = Rol.Estandar, bool activo = true)
    {
        using var db = CrearContexto();
        var usuario = new Usuario
        {
            Nombre = "Usuario de prueba",
            Correo = correo,
            HashContrasena = HasherContrasenas.Hashear(contrasena),
            Rol = rol,
            Activo = activo,
            FechaActivacion = activo ? new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) : null,
            FechaCreacion = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync();
        return usuario;
    }

    public void Dispose() => _conexion.Dispose();
}
