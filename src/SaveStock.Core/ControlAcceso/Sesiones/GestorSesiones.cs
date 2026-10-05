using Microsoft.EntityFrameworkCore;
using SaveStock.Core.Comun;
using SaveStock.Core.ControlAcceso.Entidades;
using SaveStock.Core.ControlAcceso.Seguridad;
using SaveStock.Core.Datos;

namespace SaveStock.Core.ControlAcceso.Sesiones;

/// <summary>
/// Token recién emitido. Es la única vez que el token existe en texto: en la base queda su hash.
/// </summary>
public record SesionCreada(string Token, DateTime FechaVencimiento);

/// <summary>
/// Crea, valida y revoca sesiones (RF-CA-03, RF-CA-18, RF-CA-12, RF-CA-20).
/// </summary>
public class GestorSesiones
{
    public static readonly TimeSpan DuracionSesion = TimeSpan.FromHours(8);

    private readonly CoreDbContext _db;
    private readonly IReloj _reloj;

    public GestorSesiones(CoreDbContext db, IReloj reloj)
    {
        _db = db;
        _reloj = reloj;
    }

    /// <summary>Abre una sesión de 8 horas para el usuario y devuelve el token.</summary>
    public async Task<SesionCreada> CrearAsync(Usuario usuario)
    {
        var token = GeneradorTokens.GenerarToken();
        var ahora = _reloj.AhoraUtc;
        var sesion = new Sesion
        {
            UsuarioId = usuario.Id,
            HashToken = GeneradorTokens.CalcularHash(token),
            FechaCreacion = ahora,
            FechaVencimiento = ahora + DuracionSesion,
        };

        _db.Sesiones.Add(sesion);
        await _db.SaveChangesAsync();

        return new SesionCreada(token, sesion.FechaVencimiento);
    }

    /// <summary>
    /// Devuelve la sesión (con su usuario cargado) si el token es válido: la sesión existe,
    /// no está revocada ni vencida y el usuario está activo. Si no, devuelve null.
    /// </summary>
    public async Task<Sesion?> ValidarAsync(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var hash = GeneradorTokens.CalcularHash(token);
        var sesion = await _db.Sesiones
            .Include(s => s.Usuario)
            .SingleOrDefaultAsync(s => s.HashToken == hash);

        var esValida = sesion is not null
            && sesion.FechaRevocacion is null
            && sesion.FechaVencimiento > _reloj.AhoraUtc
            && sesion.Usuario.Activo;

        return esValida ? sesion : null;
    }

    /// <summary>Revoca la sesión de ese token (cerrar sesión). Devuelve false si no existía o ya estaba revocada.</summary>
    public async Task<bool> RevocarAsync(string token)
    {
        var hash = GeneradorTokens.CalcularHash(token);
        var sesion = await _db.Sesiones.SingleOrDefaultAsync(s => s.HashToken == hash && s.FechaRevocacion == null);
        if (sesion is null)
        {
            return false;
        }

        sesion.FechaRevocacion = _reloj.AhoraUtc;
        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Revoca todas las sesiones abiertas del usuario, por ejemplo al cambiar la contraseña
    /// o al desactivarlo. Devuelve cuántas revocó.
    /// </summary>
    public async Task<int> RevocarTodasAsync(int usuarioId)
    {
        var abiertas = await _db.Sesiones
            .Where(s => s.UsuarioId == usuarioId && s.FechaRevocacion == null)
            .ToListAsync();

        var ahora = _reloj.AhoraUtc;
        foreach (var sesion in abiertas)
        {
            sesion.FechaRevocacion = ahora;
        }

        await _db.SaveChangesAsync();
        return abiertas.Count;
    }
}
