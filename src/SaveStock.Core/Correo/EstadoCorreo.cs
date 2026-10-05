namespace SaveStock.Core.Correo;

/// <summary>
/// Estado de un correo en la cola. Se guarda como texto.
/// </summary>
public enum EstadoCorreo
{
    /// <summary>Esperando a que el enviador lo procese.</summary>
    Pendiente,

    /// <summary>Reclamado por un enviador; evita que se mande dos veces (RF-NOT-12).</summary>
    Enviando,

    Enviado,

    /// <summary>Reservado para cuando se agote el número de reintentos (todavía sin uso).</summary>
    Fallido,
}
