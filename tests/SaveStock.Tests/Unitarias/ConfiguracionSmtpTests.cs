using SaveStock.Core.Comun;
using SaveStock.Core.Correo;

namespace SaveStock.Tests.Unitarias;

public class ConfiguracionSmtpTests
{
    private static ConfiguracionSaveStock Completa() => new()
    {
        SmtpHost = "smtp.ejemplo.com",
        SmtpUsuario = "usuario@ejemplo.com",
        SmtpContrasena = "secreta123",
        SmtpRemitente = "SaveStock <no-responder@ejemplo.com>",
    };

    [Fact]
    public void Con_todas_las_variables_es_valida_y_usa_el_puerto_587_por_defecto()
    {
        var resultado = ConfiguracionSmtp.Validar(Completa());

        Assert.True(resultado.Exito);
        Assert.Equal("smtp.ejemplo.com", resultado.Valor!.Host);
        Assert.Equal(587, resultado.Valor.Puerto);
    }

    [Fact]
    public void Si_faltan_variables_dice_cuales_sin_mostrar_valores()
    {
        var configuracion = new ConfiguracionSaveStock { SmtpHost = "smtp.ejemplo.com", SmtpContrasena = "secreta123" };

        var resultado = ConfiguracionSmtp.Validar(configuracion);

        Assert.False(resultado.Exito);
        Assert.Contains("SMTP_USUARIO", resultado.Mensaje);
        Assert.Contains("SMTP_REMITENTE", resultado.Mensaje);
        Assert.DoesNotContain("SMTP_HOST", resultado.Mensaje);
        Assert.DoesNotContain("secreta123", resultado.Mensaje);
    }

    [Fact]
    public void Rechaza_un_remitente_que_no_es_un_correo()
    {
        var configuracion = new ConfiguracionSaveStock
        {
            SmtpHost = "smtp.ejemplo.com",
            SmtpUsuario = "usuario@ejemplo.com",
            SmtpContrasena = "secreta123",
            SmtpRemitente = "no es un correo",
        };

        var resultado = ConfiguracionSmtp.Validar(configuracion);

        Assert.False(resultado.Exito);
        Assert.Equal("SMTP_REMITENTE no es una dirección de correo válida.", resultado.Mensaje);
    }

    [Fact]
    public void ToString_no_muestra_la_contrasena()
    {
        var smtp = ConfiguracionSmtp.Validar(Completa()).Valor!;

        Assert.DoesNotContain("secreta123", smtp.ToString());
    }
}
