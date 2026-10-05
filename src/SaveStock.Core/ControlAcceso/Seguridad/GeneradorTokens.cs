using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace SaveStock.Core.ControlAcceso.Seguridad;

/// <summary>
/// Genera los secretos aleatorios del sistema (tokens de sesión y de activación, códigos de
/// recuperación) y calcula el hash con el que se guardan. En la base nunca se guarda el secreto.
/// </summary>
public static class GeneradorTokens
{
    private const int BytesToken = 32;
    private const int LongitudCodigo = 8;

    /// <summary>Alfabeto del código de recuperación: sin 0/O ni 1/I para que no se confundan al leerlo.</summary>
    public const string AlfabetoCodigo = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    /// <summary>Token opaco de 32 bytes aleatorios en Base64Url (se puede usar en una URL).</summary>
    public static string GenerarToken() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(BytesToken));

    /// <summary>Código de 8 caracteres para restablecer la contraseña (RF-CA-10).</summary>
    public static string GenerarCodigoRecuperacion() => RandomNumberGenerator.GetString(AlfabetoCodigo, LongitudCodigo);

    /// <summary>
    /// Contraseña aleatoria que nadie conoce. Sirve para invalidar la anterior al forzar
    /// un restablecimiento (RF-CA-13).
    /// </summary>
    public static string GenerarContrasenaAleatoria() => GenerarToken();

    /// <summary>SHA-256 en hexadecimal (minúsculas) del token o código. Es lo único que se guarda.</summary>
    public static string CalcularHash(string valor) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(valor)));
}
