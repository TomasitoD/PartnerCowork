namespace SaveStock.Core.ControlAcceso.Entidades;

/// <summary>
/// Código de un solo uso para restablecer la contraseña (RF-CA-10). Vence a los 15 minutos.
/// Son 8 caracteres del alfabeto ABCDEFGHJKLMNPQRSTUVWXYZ23456789 (ver GeneradorTokens).
/// </summary>
public class CodigoRecuperacion
{
    public int Id { get; set; }

    public int UsuarioId { get; set; }

    public Usuario Usuario { get; set; } = null!;

    /// <summary>SHA-256 del código en hexadecimal. El código solo viaja en el correo.</summary>
    public string HashCodigo { get; set; } = string.Empty;

    public DateTime FechaEmision { get; set; }

    public DateTime FechaVencimiento { get; set; }

    public bool Usado { get; set; }

    /// <summary>true cuando se emitió un código nuevo y este dejó de servir.</summary>
    public bool Invalidado { get; set; }

    public OrigenCodigoRecuperacion Origen { get; set; }
}
