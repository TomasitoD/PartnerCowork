using SaveStock.Core.Comun;
using SaveStock.Core.ControlAcceso.Administracion;
using SaveStock.Core.ControlAcceso.Entidades;
using SaveStock.Core.ControlAcceso.Sesiones;
using SaveStock.Tests.Apoyo;

namespace SaveStock.Tests.Unitarias;

public sealed class ServicioAdministracionUsuariosTests : IDisposable
{
    private readonly BaseDatosEnMemoria _base = new();
    private readonly RelojFalso _reloj = new();

    public void Dispose() => _base.Dispose();

    /// <summary>El servicio y su GestorSesiones comparten el contexto, como en una petición real.</summary>
    private ServicioAdministracionUsuarios CrearServicio()
    {
        var db = _base.CrearContexto();
        return new ServicioAdministracionUsuarios(db, new GestorSesiones(db, _reloj));
    }

    private GestorSesiones CrearGestor() => new(_base.CrearContexto(), _reloj);

    private async Task<Usuario> LeerUsuarioAsync(int id)
    {
        using var db = _base.CrearContexto();
        return (await db.Usuarios.FindAsync(id))!;
    }

    // RF-CA-04: todo usuario tiene exactamente un rol.

    [Fact]
    public void Un_usuario_nuevo_tiene_rol_Estandar_por_defecto()
    {
        Assert.Equal(Rol.Estandar, new Usuario().Rol);
    }

    [Fact]
    public void La_columna_del_rol_es_obligatoria_en_la_base()
    {
        using var db = _base.CrearContexto();

        var propiedad = db.Model.FindEntityType(typeof(Usuario))!.FindProperty(nameof(Usuario.Rol))!;

        Assert.False(propiedad.IsNullable);
    }

    // RF-CA-21: listado.

    [Fact]
    public async Task Listar_devuelve_cada_usuario_con_su_rol_y_estado()
    {
        var admin = await _base.CrearUsuarioAsync("admin@negocio.com", rol: Rol.Administrador);
        var pendiente = await _base.CrearUsuarioAsync("pendiente@negocio.com", activo: false);

        var usuarios = await CrearServicio().ListarAsync();

        Assert.Equal(2, usuarios.Count);
        Assert.Equal(new UsuarioResumen(admin.Id, admin.Nombre, "admin@negocio.com", Rol.Administrador, true, true), usuarios[0]);
        Assert.Equal(new UsuarioResumen(pendiente.Id, pendiente.Nombre, "pendiente@negocio.com", Rol.Estandar, false, false), usuarios[1]);
    }

    [Fact]
    public void El_resumen_del_listado_no_tiene_campos_de_hash_ni_token()
    {
        var nombres = typeof(UsuarioResumen).GetProperties().Select(p => p.Name.ToLowerInvariant());

        Assert.DoesNotContain(nombres, n => n.Contains("hash") || n.Contains("token") || n.Contains("contrasena"));
    }

    // RF-CA-08: cambio de rol.

    [Theory]
    [InlineData("Administrador", Rol.Administrador)]
    [InlineData("administrador", Rol.Administrador)]
    [InlineData("Estandar", Rol.Estandar)]
    public async Task Cambiar_rol_guarda_el_rol_nuevo(string texto, Rol esperado)
    {
        var admin = await _base.CrearUsuarioAsync("admin@negocio.com", rol: Rol.Administrador);
        var otro = await _base.CrearUsuarioAsync("otro@negocio.com", rol: esperado == Rol.Estandar ? Rol.Administrador : Rol.Estandar);

        var resultado = await CrearServicio().CambiarRolAsync(admin.Id, otro.Id, texto);

        Assert.True(resultado.Exito);
        Assert.Equal(esperado, (await LeerUsuarioAsync(otro.Id)).Rol);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("SuperAdmin")]
    [InlineData("1")]
    public async Task Cambiar_rol_rechaza_un_rol_invalido(string? texto)
    {
        var admin = await _base.CrearUsuarioAsync("admin@negocio.com", rol: Rol.Administrador);
        var otro = await _base.CrearUsuarioAsync("otro@negocio.com");

        var resultado = await CrearServicio().CambiarRolAsync(admin.Id, otro.Id, texto);

        Assert.Equal(TipoError.Validacion, resultado.TipoError);
        Assert.Equal(ServicioAdministracionUsuarios.MensajeRolInvalido, resultado.Mensaje);
        Assert.Equal(Rol.Estandar, (await LeerUsuarioAsync(otro.Id)).Rol);
    }

    [Fact]
    public async Task Nadie_cambia_su_propio_rol()
    {
        var admin = await _base.CrearUsuarioAsync("admin@negocio.com", rol: Rol.Administrador);

        var resultado = await CrearServicio().CambiarRolAsync(admin.Id, admin.Id, "Estandar");

        Assert.Equal(TipoError.Validacion, resultado.TipoError);
        Assert.Equal("No puedes cambiar tu propio rol.", resultado.Mensaje);
        Assert.Equal(Rol.Administrador, (await LeerUsuarioAsync(admin.Id)).Rol);
    }

    [Fact]
    public async Task Cambiar_rol_de_un_id_inexistente_da_no_encontrado()
    {
        var admin = await _base.CrearUsuarioAsync("admin@negocio.com", rol: Rol.Administrador);

        var resultado = await CrearServicio().CambiarRolAsync(admin.Id, 9999, "Administrador");

        Assert.Equal(TipoError.NoEncontrado, resultado.TipoError);
    }

    // RF-CA-20: desactivar y reactivar.

    [Fact]
    public async Task Desactivar_pone_Activo_en_false_y_revoca_todas_sus_sesiones()
    {
        var admin = await _base.CrearUsuarioAsync("admin@negocio.com", rol: Rol.Administrador);
        var otro = await _base.CrearUsuarioAsync("otro@negocio.com");
        var primera = await CrearGestor().CrearAsync(otro);
        var segunda = await CrearGestor().CrearAsync(otro);

        var resultado = await CrearServicio().DesactivarAsync(admin.Id, otro.Id);

        Assert.True(resultado.Exito);
        Assert.False((await LeerUsuarioAsync(otro.Id)).Activo);
        Assert.Null(await CrearGestor().ValidarAsync(primera.Token));
        Assert.Null(await CrearGestor().ValidarAsync(segunda.Token));
        using var db = _base.CrearContexto();
        Assert.All(db.Sesiones.Where(s => s.UsuarioId == otro.Id), s => Assert.NotNull(s.FechaRevocacion));
    }

    [Fact]
    public async Task Un_administrador_no_puede_desactivarse_a_si_mismo()
    {
        var admin = await _base.CrearUsuarioAsync("admin@negocio.com", rol: Rol.Administrador);
        var sesion = await CrearGestor().CrearAsync(admin);

        var resultado = await CrearServicio().DesactivarAsync(admin.Id, admin.Id);

        Assert.Equal(TipoError.Validacion, resultado.TipoError);
        Assert.Equal("No puedes desactivar tu propia cuenta.", resultado.Mensaje);
        Assert.True((await LeerUsuarioAsync(admin.Id)).Activo);
        Assert.NotNull(await CrearGestor().ValidarAsync(sesion.Token));
    }

    [Fact]
    public async Task Desactivar_un_id_inexistente_da_no_encontrado()
    {
        var admin = await _base.CrearUsuarioAsync("admin@negocio.com", rol: Rol.Administrador);

        var resultado = await CrearServicio().DesactivarAsync(admin.Id, 9999);

        Assert.Equal(TipoError.NoEncontrado, resultado.TipoError);
    }

    [Fact]
    public async Task Reactivar_vuelve_a_poner_Activo_en_true_pero_las_sesiones_viejas_no_vuelven()
    {
        var admin = await _base.CrearUsuarioAsync("admin@negocio.com", rol: Rol.Administrador);
        var otro = await _base.CrearUsuarioAsync("otro@negocio.com");
        var vieja = await CrearGestor().CrearAsync(otro);
        await CrearServicio().DesactivarAsync(admin.Id, otro.Id);

        var resultado = await CrearServicio().ReactivarAsync(otro.Id);

        Assert.True(resultado.Exito);
        Assert.True((await LeerUsuarioAsync(otro.Id)).Activo);
        Assert.Null(await CrearGestor().ValidarAsync(vieja.Token));
    }

    [Fact]
    public async Task Reactivar_un_id_inexistente_da_no_encontrado()
    {
        var resultado = await CrearServicio().ReactivarAsync(9999);

        Assert.Equal(TipoError.NoEncontrado, resultado.TipoError);
    }
}
