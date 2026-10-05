namespace SaveStock.Core.ControlAcceso.Entidades;

/// <summary>
/// Token de un solo uso del enlace de activación (RF-CA-15, RF-CA-16). Vence a las 24 horas.
/// </summary>
public class TokenActivacion
{
    public int Id { get; set; }

    public int UsuarioId { get; set; }

    public Usuario Usuario { get; set; } = null!;

    /// <summary>SHA-256 del token en hexadecimal. El token solo viaja en el correo.</summary>
    public string HashToken { get; set; } = string.Empty;

    public DateTime FechaEmision { get; set; }

    public DateTime FechaVencimiento { get; set; }

    public bool Usado { get; set; }

    /// <summary>true cuando se reenvió un enlace nuevo y este dejó de servir (RF-CA-17).</summary>
    public bool Invalidado { get; set; }
}
