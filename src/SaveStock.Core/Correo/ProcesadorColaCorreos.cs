using Microsoft.EntityFrameworkCore;
using SaveStock.Core.Comun;
using SaveStock.Core.Datos;

namespace SaveStock.Core.Correo;

/// <summary>
/// Envía los correos Pendientes de la cola (RF-NOT-09). Cada correo se reclama de forma atómica
/// antes de enviarlo, así que ejecutar el procesador dos veces, o dos procesadores a la vez,
/// no manda el mismo correo dos veces (RF-NOT-12).
/// </summary>
public class ProcesadorColaCorreos
{
    /// <summary>Largo máximo del mensaje que se guarda en UltimoError.</summary>
    public const int LargoMaximoError = 200;

    private readonly CoreDbContext _db;
    private readonly IEnviadorCorreo _enviador;
    private readonly IReloj _reloj;

    public ProcesadorColaCorreos(CoreDbContext db, IEnviadorCorreo enviador, IReloj reloj)
    {
        _db = db;
        _enviador = enviador;
        _reloj = reloj;
    }

    /// <summary>
    /// Hace una pasada por la cola: intenta enviar cada correo que estaba Pendiente al empezar.
    /// Un correo que falla vuelve a Pendiente y se reintenta en la próxima ejecución, no en esta.
    /// </summary>
    public async Task<ResumenEnvio> ProcesarAsync(CancellationToken cancelacion = default)
    {
        var idsPendientes = await _db.CorreosEnCola
            .Where(c => c.Estado == EstadoCorreo.Pendiente)
            .OrderBy(c => c.Id)
            .Select(c => c.Id)
            .ToListAsync(cancelacion);

        var enviados = 0;
        var fallos = new List<FalloEnvio>();

        foreach (var id in idsPendientes)
        {
            if (!await ReclamarAsync(id, cancelacion))
            {
                // Otro enviador ya lo tomó (o ya se envió): no se toca.
                continue;
            }

            var correo = await _db.CorreosEnCola.AsNoTracking().SingleAsync(c => c.Id == id, cancelacion);

            try
            {
                await _enviador.EnviarAsync(correo.Destinatario, correo.Asunto, correo.Cuerpo, cancelacion);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancelacion.IsCancellationRequested)
            {
                var error = ResumirError(ex);
                await DevolverAPendienteAsync(id, error);
                fallos.Add(new FalloEnvio(id, correo.Destinatario, error));
                continue;
            }

            await MarcarEnviadoAsync(id);
            enviados++;
        }

        var pendientes = await _db.CorreosEnCola.CountAsync(c => c.Estado == EstadoCorreo.Pendiente, cancelacion);

        return new ResumenEnvio(enviados, fallos.Count, pendientes, fallos);
    }

    /// <summary>
    /// Pasa el correo de Pendiente a Enviando en un solo UPDATE con la condición Estado = Pendiente.
    /// Si otro proceso lo reclamó primero, el UPDATE no afecta ninguna fila y devuelve false.
    /// </summary>
    private async Task<bool> ReclamarAsync(int id, CancellationToken cancelacion)
    {
        var filas = await _db.CorreosEnCola
            .Where(c => c.Id == id && c.Estado == EstadoCorreo.Pendiente)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Estado, EstadoCorreo.Enviando), cancelacion);

        return filas == 1;
    }

    private async Task MarcarEnviadoAsync(int id)
    {
        var ahora = _reloj.AhoraUtc;

        // Sin token de cancelación: el correo ya salió, así que hay que registrarlo sí o sí.
        await _db.CorreosEnCola
            .Where(c => c.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Estado, EstadoCorreo.Enviado)
                .SetProperty(c => c.FechaEnvio, ahora)
                .SetProperty(c => c.UltimoError, (string?)null));
    }

    private async Task DevolverAPendienteAsync(int id, string error)
    {
        await _db.CorreosEnCola
            .Where(c => c.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Estado, EstadoCorreo.Pendiente)
                .SetProperty(c => c.Intentos, c => c.Intentos + 1)
                .SetProperty(c => c.UltimoError, error));
    }

    /// <summary>
    /// Mensaje corto para UltimoError. Solo se confía en el texto de ErrorEnvioCorreoException
    /// (lo arma el enviador sin credenciales); de cualquier otra excepción se guarda solo su tipo.
    /// </summary>
    private static string ResumirError(Exception ex)
    {
        var mensaje = ex is ErrorEnvioCorreoException
            ? ex.Message
            : $"Error inesperado al enviar el correo ({ex.GetType().Name}).";

        return mensaje.Length <= LargoMaximoError ? mensaje : mensaje[..LargoMaximoError];
    }
}
