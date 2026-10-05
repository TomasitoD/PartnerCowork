using Microsoft.EntityFrameworkCore;
using SaveStock.Core.Comun;
using SaveStock.Core.ControlAcceso.Entidades;
using SaveStock.Core.Datos;

namespace SaveStock.Core.ControlAcceso.Administracion;

/// <summary>
/// Operaciones del administrador sobre los usuarios (RF-CA-08, RF-CA-20, RF-CA-21).
/// Quién puede llamarlas no se decide aquí: lo declara Permisos y lo aplica la Web (RF-CA-05).
/// </summary>
public class ServicioAdministracionUsuarios
{
    public const string MensajeUsuarioNoExiste = "No existe un usuario con ese id.";
    public const string MensajeRolInvalido = "El rol no es válido. Usa \"Administrador\" o \"Estandar\".";
    public const string MensajePropioRol = "No puedes cambiar tu propio rol.";
    public const string MensajeRolCambiado = "El rol del usuario se actualizó.";

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

    /// <summary>
    /// Cambia el rol de otro usuario (RF-CA-08). Nadie cambia su propio rol: así el sistema
    /// siempre conserva al menos un Administrador (el que hace el cambio).
    /// </summary>
    /// <param name="idAdministrador">Quién hace el cambio (el usuario de la sesión).</param>
    /// <param name="idUsuario">A quién se le cambia el rol.</param>
    /// <param name="rolTexto">"Administrador" o "Estandar", tal como llega en el JSON.</param>
    public async Task<Resultado> CambiarRolAsync(int idAdministrador, int idUsuario, string? rolTexto)
    {
        var rolNuevo = LeerRol(rolTexto);
        if (rolNuevo is null)
        {
            return Resultado.Error(TipoError.Validacion, MensajeRolInvalido);
        }

        if (idUsuario == idAdministrador)
        {
            return Resultado.Error(TipoError.Validacion, MensajePropioRol);
        }

        var usuario = await _db.Usuarios.FindAsync(idUsuario);
        if (usuario is null)
        {
            return Resultado.Error(TipoError.NoEncontrado, MensajeUsuarioNoExiste);
        }

        usuario.Rol = rolNuevo.Value;
        await _db.SaveChangesAsync();
        return Resultado.Ok(MensajeRolCambiado);
    }

    /// <summary>
    /// Convierte el texto en un <see cref="Rol"/>. Solo acepta los dos nombres (sin importar mayúsculas);
    /// un número como "1" o cualquier otro texto da null.
    /// </summary>
    private static Rol? LeerRol(string? rolTexto) => rolTexto?.Trim().ToLowerInvariant() switch
    {
        "administrador" => Rol.Administrador,
        "estandar" or "estándar" => Rol.Estandar,
        _ => null,
    };
}
