namespace SaveStock.Core.ControlAcceso.Entidades;

/// <summary>
/// Sesión abierta por un usuario. En la base solo se guarda el SHA-256 del token, nunca el token.
/// </summary>
public class Sesion
{
    public int Id { get; set; }

    public int UsuarioId { get; set; }

    public Usuario Usuario { get; set; } = null!;

    /// <summary>SHA-256 del token en hexadecimal. Único.</summary>
    public string HashToken { get; set; } = string.Empty;

    public DateTime FechaCreacion { get; set; }

    public DateTime FechaVencimiento { get; set; }

    /// <summary>null = sigue abierta. Con fecha = cerrada o revocada (RF-CA-18, RF-CA-12, RF-CA-20).</summary>
    public DateTime? FechaRevocacion { get; set; }
}
