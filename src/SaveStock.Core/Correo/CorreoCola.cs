using SaveStock.Core.Comun;
using SaveStock.Core.Datos;

namespace SaveStock.Core.Correo;

/// <summary>
/// Cola de correos en la tabla CorreosEnCola (RF-NOT-08).
/// </summary>
public class CorreoCola : ICorreoCola
{
    private readonly CoreDbContext _db;
    private readonly IReloj _reloj;

    public CorreoCola(CoreDbContext db, IReloj reloj)
    {
        _db = db;
        _reloj = reloj;
    }

    /// <summary>
    /// Inserta el correo como Pendiente. Usa el mismo CoreDbContext de la petición, así que
    /// SaveChanges también guarda los cambios que la operación tenga pendientes.
    /// </summary>
    public async Task EncolarAsync(string destinatario, string asunto, string cuerpo)
    {
        _db.CorreosEnCola.Add(new CorreoEnCola
        {
            Destinatario = destinatario,
            Asunto = asunto,
            Cuerpo = cuerpo,
            Estado = EstadoCorreo.Pendiente,
            Intentos = 0,
            FechaCreacion = _reloj.AhoraUtc,
        });

        await _db.SaveChangesAsync();
    }
}
