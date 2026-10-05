using Microsoft.EntityFrameworkCore;
using SaveStock.Core.Datos;

namespace SaveStock.Core.ControlAcceso.Administracion;

/// <summary>
/// Operaciones del administrador sobre los usuarios (RF-CA-08, RF-CA-20, RF-CA-21).
/// Quién puede llamarlas no se decide aquí: lo declara Permisos y lo aplica la Web (RF-CA-05).
/// </summary>
public class ServicioAdministracionUsuarios
{
    private readonly CoreDbContext _db;

    public ServicioAdministracionUsuarios(CoreDbContext db)
    {
        _db = db;
    }

    /// <summary>Todos los usuarios con su rol y estado, ordenados por id (RF-CA-21).</summary>
    public async Task<IReadOnlyList<UsuarioResumen>> ListarAsync()
    {
        // La proyección a UsuarioResumen se hace en la consulta: el hash ni siquiera se lee de la base.
        return await _db.Usuarios
            .AsNoTracking()
            .OrderBy(u => u.Id)
            .Select(u => new UsuarioResumen(u.Id, u.Nombre, u.Correo, u.Rol, u.Activo, u.FechaActivacion != null))
            .ToListAsync();
    }
}
