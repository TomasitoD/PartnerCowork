using SaveStock.Core.Comun;
using SaveStock.Core.ControlAcceso.Seguridad;

namespace SaveStock.Tests.Unitarias;

public class PoliticaContrasenaTests
{
    [Theory]
    [InlineData("clave123")]
    [InlineData("12345abc")]
    [InlineData("UnaClaveLarga2026")]
    public void Acepta_8_caracteres_con_letras_y_numeros(string contrasena)
    {
        Assert.True(PoliticaContrasena.Validar(contrasena).Exito);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc123")] // menos de 8
    [InlineData("solamenteletras")] // sin números
    [InlineData("1234567890")] // sin letras
    public void Rechaza_con_el_mensaje_de_la_politica(string? contrasena)
    {
        var resultado = PoliticaContrasena.Validar(contrasena);

        Assert.False(resultado.Exito);
        Assert.Equal(TipoError.Validacion, resultado.TipoError);
        Assert.Equal("La contraseña debe tener al menos 8 caracteres e incluir letras y números.", resultado.Mensaje);
    }
}
