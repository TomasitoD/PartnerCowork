using System.Net.Mail;

namespace SaveStock.Core.Comun;

/// <summary>
/// Validaciones comunes de la entrada externa (RD-07). Un dato ausente o mal formado
/// se rechaza con un <see cref="Resultado"/> de tipo Validacion, nunca con una excepción.
/// </summary>
public static class ValidadorEntrada
{
    public const int LongitudMaximaCorreo = 254;
    public const int LongitudMaximaNombre = 100;
    public const int LongitudMaximaContrasena = 128;

    /// <summary>
    /// Deja el correo como se guarda en la base: sin espacios alrededor y en minúsculas.
    /// </summary>
    public static string NormalizarCorreo(string correo) => correo.Trim().ToLowerInvariant();

    /// <summary>
    /// El correo es obligatorio, no supera la longitud máxima y tiene un formato válido
    /// con un dominio que incluye un punto (por ejemplo, ana@negocio.com).
    /// </summary>
    public static Resultado ValidarCorreo(string? correo)
    {
        if (string.IsNullOrWhiteSpace(correo))
        {
            return Resultado.Error(TipoError.Validacion, "El correo es obligatorio.");
        }

        var limpio = correo.Trim();
        if (limpio.Length > LongitudMaximaCorreo)
        {
            return Resultado.Error(TipoError.Validacion, $"El correo no puede superar los {LongitudMaximaCorreo} caracteres.");
        }

        // MailAddress acepta cosas como "Ana <ana@x.com>", por eso exigimos que la dirección sea todo el texto.
        var formatoValido = MailAddress.TryCreate(limpio, out var direccion)
            && direccion.Address == limpio
            && direccion.Host.Contains('.')
            && !direccion.Host.StartsWith('.')
            && !direccion.Host.EndsWith('.');

        return formatoValido
            ? Resultado.Ok()
            : Resultado.Error(TipoError.Validacion, "El correo no tiene un formato válido.");
    }

    /// <summary>
    /// Un texto obligatorio (no vacío ni solo espacios) que no supera la longitud máxima.
    /// </summary>
    /// <param name="valor">El texto recibido.</param>
    /// <param name="nombreCampo">Cómo se llama el campo en el mensaje, por ejemplo "El nombre".</param>
    /// <param name="longitudMaxima">Cantidad máxima de caracteres.</param>
    public static Resultado ValidarTextoObligatorio(string? valor, string nombreCampo, int longitudMaxima)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return Resultado.Error(TipoError.Validacion, $"{nombreCampo} es obligatorio.");
        }

        if (valor.Trim().Length > longitudMaxima)
        {
            return Resultado.Error(TipoError.Validacion, $"{nombreCampo} no puede superar los {longitudMaxima} caracteres.");
        }

        return Resultado.Ok();
    }
}
