using SaveStock.Core.Comun;

namespace SaveStock.Core.ControlAcceso.Seguridad;

/// <summary>
/// Política mínima de contraseña (RF-CA-14): al menos 8 caracteres, al menos una letra y al menos
/// un número. Se aplica en el registro, el restablecimiento y el cambio de contraseña.
/// </summary>
public static class PoliticaContrasena
{
    public const int LongitudMinima = 8;

    public const string MensajeIncumple = "La contraseña debe tener al menos 8 caracteres e incluir letras y números.";

    public static Resultado Validar(string? contrasena)
    {
        if (string.IsNullOrEmpty(contrasena)
            || contrasena.Length < LongitudMinima
            || !contrasena.Any(char.IsLetter)
            || !contrasena.Any(char.IsDigit))
        {
            return Resultado.Error(TipoError.Validacion, MensajeIncumple);
        }

        if (contrasena.Length > ValidadorEntrada.LongitudMaximaContrasena)
        {
            return Resultado.Error(TipoError.Validacion, $"La contraseña no puede superar los {ValidadorEntrada.LongitudMaximaContrasena} caracteres.");
        }

        return Resultado.Ok();
    }
}
