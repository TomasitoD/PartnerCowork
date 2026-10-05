namespace SaveStock.Core.Correo;

/// <summary>
/// Falla conocida al enviar un correo. El mensaje es corto, está en español y no incluye
/// credenciales ni trazas: es lo que se guarda en CorreoEnCola.UltimoError.
/// </summary>
public class ErrorEnvioCorreoException : Exception
{
    public ErrorEnvioCorreoException(string mensaje, Exception? causa = null)
        : base(mensaje, causa)
    {
    }
}
