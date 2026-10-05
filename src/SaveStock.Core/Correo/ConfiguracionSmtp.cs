using System.Net.Mail;
using SaveStock.Core.Comun;

namespace SaveStock.Core.Correo;

/// <summary>
/// Datos del servidor SMTP ya validados. Salen de las variables SMTP_* (RF-NOT-13, RD-10):
/// nunca están escritos en el código ni en el repositorio.
/// </summary>
public record ConfiguracionSmtp(string Host, int Puerto, string Usuario, string Contrasena, string Remitente)
{
    /// <summary>
    /// Comprueba que estén todas las variables SMTP_*. SMTP_PUERTO es opcional (por defecto 587).
    /// Si falta alguna, el mensaje dice cuáles, pero nunca muestra sus valores.
    /// </summary>
    public static Resultado<ConfiguracionSmtp> Validar(ConfiguracionSaveStock configuracion)
    {
        var faltantes = new List<string>();
        if (configuracion.SmtpHost is null)
        {
            faltantes.Add("SMTP_HOST");
        }

        if (configuracion.SmtpUsuario is null)
        {
            faltantes.Add("SMTP_USUARIO");
        }

        if (configuracion.SmtpContrasena is null)
        {
            faltantes.Add("SMTP_CONTRASENA");
        }

        if (configuracion.SmtpRemitente is null)
        {
            faltantes.Add("SMTP_REMITENTE");
        }

        if (faltantes.Count > 0)
        {
            return Resultado<ConfiguracionSmtp>.Error(
                TipoError.Validacion,
                $"Falta configurar {string.Join(", ", faltantes)}. Agrégalas al archivo .env (mira .env.example) o como variables de entorno.");
        }

        if (configuracion.SmtpPuerto is < 1 or > 65535)
        {
            return Resultado<ConfiguracionSmtp>.Error(TipoError.Validacion, "SMTP_PUERTO debe ser un número entre 1 y 65535.");
        }

        // Acepta "correo@dominio.com" o "Nombre <correo@dominio.com>".
        if (!MailAddress.TryCreate(configuracion.SmtpRemitente, out _))
        {
            return Resultado<ConfiguracionSmtp>.Error(TipoError.Validacion, "SMTP_REMITENTE no es una dirección de correo válida.");
        }

        return Resultado<ConfiguracionSmtp>.Ok(new ConfiguracionSmtp(
            configuracion.SmtpHost!,
            configuracion.SmtpPuerto,
            configuracion.SmtpUsuario!,
            configuracion.SmtpContrasena!,
            configuracion.SmtpRemitente!));
    }

    /// <summary>Evita que la contraseña aparezca si alguien imprime este objeto.</summary>
    public override string ToString() => $"ConfiguracionSmtp {{ Host = {Host}, Puerto = {Puerto}, Usuario = {Usuario}, Remitente = {Remitente} }}";
}
