using SaveStock.Core.Comun;

namespace SaveStock.Tests.Unitarias;

public class ValidadorEntradaTests
{
    [Theory]
    [InlineData("ana@negocio.com")]
    [InlineData("  ana.perez@correo.com.do  ")]
    public void Acepta_un_correo_bien_formado(string correo)
    {
        Assert.True(ValidadorEntrada.ValidarCorreo(correo).Exito);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ana")]
    [InlineData("ana@")]
    [InlineData("ana@negocio")] // dominio sin punto
    [InlineData("@negocio.com")]
    [InlineData("Ana <ana@negocio.com>")]
    public void Rechaza_un_correo_vacio_o_mal_formado_sin_excepcion(string? correo)
    {
        var resultado = ValidadorEntrada.ValidarCorreo(correo);

        Assert.False(resultado.Exito);
        Assert.Equal(TipoError.Validacion, resultado.TipoError);
        Assert.False(string.IsNullOrWhiteSpace(resultado.Mensaje));
    }

    [Fact]
    public void Rechaza_un_correo_demasiado_largo()
    {
        var correo = new string('a', 250) + "@negocio.com";

        Assert.False(ValidadorEntrada.ValidarCorreo(correo).Exito);
    }

    [Fact]
    public void Normaliza_el_correo_a_minusculas_y_sin_espacios()
    {
        Assert.Equal("ana@negocio.com", ValidadorEntrada.NormalizarCorreo("  Ana@Negocio.COM "));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Texto_obligatorio_rechaza_vacio(string? valor)
    {
        var resultado = ValidadorEntrada.ValidarTextoObligatorio(valor, "El nombre", 100);

        Assert.False(resultado.Exito);
        Assert.Equal("El nombre es obligatorio.", resultado.Mensaje);
    }

    [Fact]
    public void Texto_obligatorio_rechaza_si_supera_la_longitud_maxima()
    {
        var resultado = ValidadorEntrada.ValidarTextoObligatorio(new string('x', 101), "El nombre", 100);

        Assert.False(resultado.Exito);
        Assert.Equal(TipoError.Validacion, resultado.TipoError);
    }

    [Fact]
    public void Texto_obligatorio_acepta_un_valor_valido()
    {
        Assert.True(ValidadorEntrada.ValidarTextoObligatorio("Ana", "El nombre", 100).Exito);
    }
}
