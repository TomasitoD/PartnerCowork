namespace SaveStock.Core.Correo;

/// <summary>
/// Correo pendiente de envío. La operación de negocio solo lo registra aquí; lo envía
/// SaveStock.Enviador por separado (RF-NOT-08).
/// </summary>
public class CorreoEnCola
{
    public int Id { get; set; }

    public string Destinatario { get; set; } = string.Empty;

    public string Asunto { get; set; } = string.Empty;

    public string Cuerpo { get; set; } = string.Empty;

    public EstadoCorreo Estado { get; set; } = EstadoCorreo.Pendiente;

    public int Intentos { get; set; }

    public DateTime FechaCreacion { get; set; }

    public DateTime? FechaEnvio { get; set; }

    /// <summary>Mensaje corto del último fallo, sin credenciales.</summary>
    public string? UltimoError { get; set; }
}
