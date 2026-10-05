using SaveStock.Core.Comun;
using SaveStock.Core.ControlAcceso.Autorizacion;
using SaveStock.Core.ControlAcceso.Entidades;

namespace SaveStock.Tests.Unitarias;

public class PermisosTests
{
    public static TheoryData<Operacion> TodasLasOperaciones()
    {
        var datos = new TheoryData<Operacion>();
        foreach (var operacion in Enum.GetValues<Operacion>())
        {
            datos.Add(operacion);
        }

        return datos;
    }

    [Theory]
    [MemberData(nameof(TodasLasOperaciones))]
    public void Toda_operacion_declara_su_nivel_en_Permisos(Operacion operacion)
    {
        Assert.True(Permisos.PorOperacion.ContainsKey(operacion), $"Falta {operacion} en Permisos.");
    }

    [Theory]
    [InlineData(Operacion.ListarUsuarios)]
    [InlineData(Operacion.CambiarRol)]
    [InlineData(Operacion.ForzarRestablecimiento)]
    public void Un_Estandar_recibe_Prohibido_en_una_operacion_de_Administrador(Operacion operacion)
    {
        var estandar = new Usuario { Rol = Rol.Estandar, Activo = true };

        var resultado = Autorizador.Autorizar(operacion, estandar);

        Assert.Equal(TipoError.Prohibido, resultado.TipoError);
    }

    [Fact]
    public void Un_Administrador_puede_ejecutar_una_operacion_de_Administrador()
    {
        var administrador = new Usuario { Rol = Rol.Administrador, Activo = true };

        Assert.True(Autorizador.Autorizar(Operacion.ListarUsuarios, administrador).Exito);
    }

    [Fact]
    public void Sin_sesion_una_operacion_autenticada_da_NoAutenticado()
    {
        var resultado = Autorizador.Autorizar(Operacion.ConsultarUsuarioActual, usuario: null);

        Assert.Equal(TipoError.NoAutenticado, resultado.TipoError);
    }

    [Fact]
    public void Una_operacion_publica_no_necesita_sesion()
    {
        Assert.True(Autorizador.Autorizar(Operacion.Registrar, usuario: null).Exito);
    }
}
