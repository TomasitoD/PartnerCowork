using System.Security.Cryptography;
using System.Text;

namespace SaveStock.Core.ControlAcceso.Seguridad;

/// <summary>
/// Hash de contraseñas con PBKDF2-SHA256 y una sal aleatoria por usuario (RF-CA-02, RD-05).
/// El valor guardado tiene el formato <c>pbkdf2-sha256$100000$&lt;salBase64&gt;$&lt;hashBase64&gt;</c>:
/// guarda todo lo necesario para verificar, pero no permite recuperar la contraseña.
/// </summary>
public static class HasherContrasenas
{
    private const string Algoritmo = "pbkdf2-sha256";
    private const int Iteraciones = 100_000;
    private const int BytesSal = 16;
    private const int BytesHash = 32;

    public static string Hashear(string contrasena)
    {
        // Una sal nueva en cada hash: dos usuarios con la misma contraseña tienen hashes distintos.
        var sal = RandomNumberGenerator.GetBytes(BytesSal);
        var hash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(contrasena), sal, Iteraciones, HashAlgorithmName.SHA256, BytesHash);

        return $"{Algoritmo}${Iteraciones}${Convert.ToBase64String(sal)}${Convert.ToBase64String(hash)}";
    }

    /// <summary>
    /// true si la contraseña corresponde al hash guardado. Un hash con formato inválido devuelve false.
    /// </summary>
    public static bool Verificar(string contrasena, string hashGuardado)
    {
        var partes = hashGuardado.Split('$');
        if (partes.Length != 4 || partes[0] != Algoritmo || !int.TryParse(partes[1], out var iteraciones))
        {
            return false;
        }

        byte[] sal;
        byte[] hashEsperado;
        try
        {
            sal = Convert.FromBase64String(partes[2]);
            hashEsperado = Convert.FromBase64String(partes[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        if (iteraciones <= 0 || sal.Length == 0 || hashEsperado.Length == 0)
        {
            return false;
        }

        var hashCalculado = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(contrasena), sal, iteraciones, HashAlgorithmName.SHA256, hashEsperado.Length);

        // Comparación en tiempo constante: no revela cuántos bytes coinciden.
        return CryptographicOperations.FixedTimeEquals(hashCalculado, hashEsperado);
    }
}
