namespace SaveStock.Core.Correo;

/// <summary>
/// Punto de entrada para mandar un correo: solo lo deja en la cola (RF-NOT-08).
/// La operación de negocio nunca habla con el servidor SMTP.
/// </summary>
public interface ICorreoCola
{
    /// <summary>Guarda el correo en estado Pendiente para que lo envíe SaveStock.Enviador.</summary>
    Task EncolarAsync(string destinatario, string asunto, string cuerpo);
}
