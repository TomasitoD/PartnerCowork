namespace SaveStock.Core.Correo;

/// <summary>
/// Envía un correo de verdad (por ejemplo, por SMTP). Solo lo usa ProcesadorColaCorreos dentro de
/// SaveStock.Enviador; la web nunca lo llama (RF-NOT-08). En las pruebas se reemplaza por uno falso.
/// </summary>
public interface IEnviadorCorreo
{
    /// <summary>
    /// Envía un correo de texto plano. Si no se pudo enviar lanza <see cref="ErrorEnvioCorreoException"/>
    /// con un mensaje corto y sin credenciales.
    /// </summary>
    Task EnviarAsync(string destinatario, string asunto, string cuerpo, CancellationToken cancelacion = default);
}
