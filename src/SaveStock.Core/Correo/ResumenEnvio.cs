namespace SaveStock.Core.Correo;

/// <summary>
/// Lo que pasó en una pasada del procesador de la cola.
/// </summary>
/// <param name="Enviados">Correos que salieron en esta pasada.</param>
/// <param name="Fallidos">Correos que no se pudieron enviar y volvieron a Pendiente.</param>
/// <param name="Pendientes">Correos que quedan en Pendiente al terminar (incluye los fallidos).</param>
/// <param name="Fallos">Detalle de cada fallo, para mostrarlo en la consola.</param>
public record ResumenEnvio(int Enviados, int Fallidos, int Pendientes, IReadOnlyList<FalloEnvio> Fallos);

/// <summary>Un correo que no se pudo enviar y el motivo (sin credenciales).</summary>
public record FalloEnvio(int CorreoId, string Destinatario, string Error);
