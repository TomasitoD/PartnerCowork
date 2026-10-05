using SaveStock.Core.ControlAcceso.Seguridad;

namespace SaveStock.Tests.Unitarias;

public class HasherContrasenasTests
{
    [Fact]
    public void El_valor_guardado_no_es_la_contrasena()
    {
        var hash = HasherContrasenas.Hashear("clave1234");

        Assert.NotEqual("clave1234", hash);
        Assert.DoesNotContain("clave1234", hash);
        Assert.StartsWith("pbkdf2-sha256$100000$", hash);
    }

    [Fact]
    public void Dos_hashes_de_la_misma_contrasena_son_distintos_por_la_sal()
    {
        var primero = HasherContrasenas.Hashear("clave1234");
        var segundo = HasherContrasenas.Hashear("clave1234");

        Assert.NotEqual(primero, segundo);
    }

    [Fact]
    public void Verificar_acepta_la_contrasena_correcta()
    {
        var hash = HasherContrasenas.Hashear("clave1234");

        Assert.True(HasherContrasenas.Verificar("clave1234", hash));
    }

    [Theory]
    [InlineData("clave12345")]
    [InlineData("Clave1234")]
    [InlineData("")]
    public void Verificar_rechaza_una_contrasena_incorrecta(string intento)
    {
        var hash = HasherContrasenas.Hashear("clave1234");

        Assert.False(HasherContrasenas.Verificar(intento, hash));
    }

    [Theory]
    [InlineData("")]
    [InlineData("texto-plano")]
    [InlineData("pbkdf2-sha256$abc$AAAA$AAAA")]
    [InlineData("pbkdf2-sha256$100000$no-es-base64$AAAA")]
    public void Verificar_devuelve_false_con_un_hash_mal_formado(string hashGuardado)
    {
        Assert.False(HasherContrasenas.Verificar("clave1234", hashGuardado));
    }
}
