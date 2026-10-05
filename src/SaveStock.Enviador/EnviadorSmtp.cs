using System.Net.Sockets;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MimeKit.Text;
using SaveStock.Core.Correo;

namespace SaveStock.Enviador;

/// <summary>
/// Envía los correos por SMTP con MailKit (RF-NOT-09). Los datos del servidor salen de las
/// variables SMTP_* (RF-NOT-13). Los errores se traducen a mensajes cortos sin credenciales.
/// </summary>
public class EnviadorSmtp : IEnviadorCorreo
{
    /// <summary>Tiempo máximo de espera de cada operación con el servidor, para no colgarse si no responde.</summary>
    private const int TiempoEsperaMilisegundos = 15_000;

    private readonly ConfiguracionSmtp _smtp;

    public EnviadorSmtp(ConfiguracionSmtp smtp)
    {
        _smtp = smtp;
    }

    public async Task EnviarAsync(string destinatario, string asunto, string cuerpo, CancellationToken cancelacion = default)
    {
        if (!MailboxAddress.TryParse(destinatario, out var direccionDestino))
        {
            throw new ErrorEnvioCorreoException("La dirección del destinatario no es válida.");
        }

        var mensaje = new MimeMessage();
        mensaje.From.Add(MailboxAddress.Parse(_smtp.Remitente));
        mensaje.To.Add(direccionDestino);
        mensaje.Subject = asunto;
        mensaje.Body = new TextPart(TextFormat.Plain) { Text = cuerpo };

        using var cliente = new SmtpClient { Timeout = TiempoEsperaMilisegundos };

        try
        {
            // Auto: TLS directo en el puerto 465 y STARTTLS (si el servidor lo ofrece) en los demás, como el 587.
            await cliente.ConnectAsync(_smtp.Host, _smtp.Puerto, SecureSocketOptions.Auto, cancelacion);
            await cliente.AuthenticateAsync(_smtp.Usuario, _smtp.Contrasena, cancelacion);
            await cliente.SendAsync(mensaje, cancelacion);
            await cliente.DisconnectAsync(quit: true, cancelacion);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancelacion.IsCancellationRequested)
        {
            throw new ErrorEnvioCorreoException(Traducir(ex), ex);
        }
    }

    /// <summary>
    /// Mensaje corto para cada tipo de falla. No se copia el texto de la excepción original
    /// porque podría incluir datos del servidor o de la cuenta.
    /// </summary>
    private string Traducir(Exception ex) => ex switch
    {
        AuthenticationException => "El servidor SMTP rechazó el usuario o la contraseña.",
        SslHandshakeException => "No se pudo establecer una conexión segura con el servidor SMTP.",
        SmtpCommandException comando => $"El servidor SMTP rechazó el envío (código {(int)comando.StatusCode}).",
        SmtpProtocolException => "El servidor SMTP respondió algo inesperado.",
        ServiceNotConnectedException => "Se perdió la conexión con el servidor SMTP.",
        SocketException or IOException or TimeoutException or OperationCanceledException =>
            $"No se pudo conectar con el servidor SMTP {_smtp.Host}:{_smtp.Puerto}.",
        _ => $"Error al enviar por SMTP ({ex.GetType().Name}).",
    };
}
