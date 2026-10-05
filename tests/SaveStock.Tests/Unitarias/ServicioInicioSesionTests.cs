using Microsoft.EntityFrameworkCore;
using SaveStock.Core.Comun;
using SaveStock.Core.ControlAcceso.Entidades;
using SaveStock.Core.ControlAcceso.InicioSesion;
using SaveStock.Core.ControlAcceso.Sesiones;
using SaveStock.Tests.Apoyo;

namespace SaveStock.Tests.Unitarias;

public sealed class ServicioInicioSesionTests : IDisposable
{
    private const string Correo = "ana@negocio.com";
    private const string Contrasena = "clave1234";
    private const string ContrasenaIncorrecta = "otraClave99";

    private readonly BaseDatosEnMemoria _base = new();
    private readonly RelojFalso _reloj = new();

    public void Dispose() => _base.Dispose();

    /// <summary>Un servicio nuevo (con su propio contexto) por llamada, como en una petición web.</summary>
    private ServicioInicioSesion CrearServicio()
    {
        var db = _base.CrearContexto();
        return new ServicioInicioSesion(db, _reloj, new GestorSesiones(db, _reloj));
    }

    private Task<Resultado<SesionCreada>> IniciarAsync(string? correo, string? contrasena) =>
        CrearServicio().IniciarAsync(correo, contrasena);

    private async Task<Usuario> LeerUsuarioAsync()
    {
        using var db = _base.CrearContexto();
        return await db.Usuarios.SingleAsync(u => u.Correo == Correo);
    }

    private async Task FallarAsync(int veces)
    {
        for (var i = 0; i < veces; i++)
        {
            var resultado = await IniciarAsync(Correo, ContrasenaIncorrecta);
            Assert.Equal(TipoError.NoAutenticado, resultado.TipoError);
        }
    }

    [Fact]
    public async Task Con_credenciales_correctas_devuelve_un_token_valido()
    {
        await _base.CrearUsuarioAsync(Correo, Contrasena);

        var resultado = await IniciarAsync(Correo, Contrasena);

        Assert.True(resultado.Exito);
        Assert.Equal(_reloj.AhoraUtc + GestorSesiones.DuracionSesion, resultado.Valor!.FechaVencimiento);
        using var db = _base.CrearContexto();
        Assert.NotNull(await new GestorSesiones(db, _reloj).ValidarAsync(resultado.Valor.Token));
    }

    [Fact]
    public async Task El_correo_se_compara_sin_espacios_ni_mayusculas()
    {
        await _base.CrearUsuarioAsync(Correo, Contrasena);

        var resultado = await IniciarAsync("  ANA@Negocio.com ", Contrasena);

        Assert.True(resultado.Exito);
    }

    [Fact]
    public async Task Contrasena_incorrecta_y_correo_inexistente_dan_el_mismo_error()
    {
        await _base.CrearUsuarioAsync(Correo, Contrasena);

        var contrasenaMala = await IniciarAsync(Correo, ContrasenaIncorrecta);
        var correoInexistente = await IniciarAsync("nadie@negocio.com", Contrasena);

        Assert.Equal(TipoError.NoAutenticado, contrasenaMala.TipoError);
        Assert.Equal(contrasenaMala.TipoError, correoInexistente.TipoError);
        Assert.Equal("Correo o contraseña incorrectos.", contrasenaMala.Mensaje);
        Assert.Equal(contrasenaMala.Mensaje, correoInexistente.Mensaje);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no-es-un-correo")]
    [InlineData("ana@negocio")]
    public async Task Un_correo_vacio_o_mal_formado_se_rechaza_como_validacion(string? correo)
    {
        var resultado = await IniciarAsync(correo, Contrasena);

        Assert.Equal(TipoError.Validacion, resultado.TipoError);
    }

    [Fact]
    public async Task Una_contrasena_vacia_se_rechaza_como_validacion()
    {
        var resultado = await IniciarAsync(Correo, "");

        Assert.Equal(TipoError.Validacion, resultado.TipoError);
        Assert.Equal("La contraseña es obligatoria.", resultado.Mensaje);
    }

    [Fact]
    public async Task Cada_contrasena_incorrecta_suma_un_intento_fallido()
    {
        await _base.CrearUsuarioAsync(Correo, Contrasena);

        await FallarAsync(3);

        var usuario = await LeerUsuarioAsync();
        Assert.Equal(3, usuario.IntentosFallidos);
        Assert.Null(usuario.BloqueadoHasta);
    }

    [Fact]
    public async Task El_quinto_fallo_bloquea_la_cuenta_15_minutos_y_pone_el_contador_en_cero()
    {
        await _base.CrearUsuarioAsync(Correo, Contrasena);

        await FallarAsync(5);

        var usuario = await LeerUsuarioAsync();
        Assert.Equal(_reloj.AhoraUtc.AddMinutes(15), usuario.BloqueadoHasta);
        Assert.Equal(0, usuario.IntentosFallidos);
    }

    [Fact]
    public async Task Bloqueada_rechaza_el_sexto_intento_aunque_la_contrasena_sea_correcta()
    {
        await _base.CrearUsuarioAsync(Correo, Contrasena);
        await FallarAsync(5);

        var resultado = await IniciarAsync(Correo, Contrasena);

        Assert.False(resultado.Exito);
        Assert.Equal(TipoError.Bloqueado, resultado.TipoError);
        Assert.Equal("La cuenta está bloqueada temporalmente por intentos fallidos. Intenta de nuevo más tarde.", resultado.Mensaje);
    }

    [Fact]
    public async Task Sigue_bloqueada_un_instante_antes_de_que_pasen_los_15_minutos()
    {
        await _base.CrearUsuarioAsync(Correo, Contrasena);
        await FallarAsync(5);

        _reloj.Avanzar(TimeSpan.FromMinutes(15) - TimeSpan.FromSeconds(1));

        Assert.Equal(TipoError.Bloqueado, (await IniciarAsync(Correo, Contrasena)).TipoError);
    }

    [Fact]
    public async Task Al_vencer_el_bloqueo_puede_entrar_y_el_bloqueo_se_limpia()
    {
        await _base.CrearUsuarioAsync(Correo, Contrasena);
        await FallarAsync(5);

        _reloj.Avanzar(TimeSpan.FromMinutes(15));
        var resultado = await IniciarAsync(Correo, Contrasena);

        Assert.True(resultado.Exito);
        var usuario = await LeerUsuarioAsync();
        Assert.Equal(0, usuario.IntentosFallidos);
        Assert.Null(usuario.BloqueadoHasta);
    }

    [Fact]
    public async Task Un_inicio_correcto_pone_el_contador_en_cero()
    {
        await _base.CrearUsuarioAsync(Correo, Contrasena);

        // 4 fallos + éxito + 4 fallos: nunca llega a 5 consecutivos.
        await FallarAsync(4);
        Assert.True((await IniciarAsync(Correo, Contrasena)).Exito);
        Assert.Equal(0, (await LeerUsuarioAsync()).IntentosFallidos);
        await FallarAsync(4);

        var usuario = await LeerUsuarioAsync();
        Assert.Equal(4, usuario.IntentosFallidos);
        Assert.Null(usuario.BloqueadoHasta);
        Assert.True((await IniciarAsync(Correo, Contrasena)).Exito);
    }

    [Fact]
    public async Task Una_cuenta_sin_activar_con_la_contrasena_correcta_da_prohibido()
    {
        await _base.CrearUsuarioAsync(Correo, Contrasena, activo: false);

        var resultado = await IniciarAsync(Correo, Contrasena);

        Assert.Equal(TipoError.Prohibido, resultado.TipoError);
        Assert.Equal("La cuenta no está activa. Revisa tu correo para activarla.", resultado.Mensaje);
    }

    [Fact]
    public async Task Una_cuenta_desactivada_con_la_contrasena_correcta_da_prohibido()
    {
        var usuario = await _base.CrearUsuarioAsync(Correo, Contrasena);
        using (var db = _base.CrearContexto())
        {
            await db.Usuarios.Where(u => u.Id == usuario.Id).ExecuteUpdateAsync(u => u.SetProperty(x => x.Activo, false));
        }

        var resultado = await IniciarAsync(Correo, Contrasena);

        Assert.Equal(TipoError.Prohibido, resultado.TipoError);
        Assert.Equal("La cuenta está desactivada. Contacta a un administrador.", resultado.Mensaje);
    }

    [Fact]
    public async Task Una_cuenta_sin_activar_con_contrasena_incorrecta_da_el_mensaje_generico()
    {
        await _base.CrearUsuarioAsync(Correo, Contrasena, activo: false);

        var resultado = await IniciarAsync(Correo, ContrasenaIncorrecta);

        Assert.Equal(TipoError.NoAutenticado, resultado.TipoError);
        Assert.Equal("Correo o contraseña incorrectos.", resultado.Mensaje);
    }

    [Fact]
    public async Task Cerrar_revoca_la_sesion_del_token()
    {
        await _base.CrearUsuarioAsync(Correo, Contrasena);
        var token = (await IniciarAsync(Correo, Contrasena)).Valor!.Token;

        var resultado = await CrearServicio().CerrarAsync(token);

        Assert.True(resultado.Exito);
        Assert.Equal("Sesión cerrada.", resultado.Mensaje);
        using var db = _base.CrearContexto();
        Assert.Null(await new GestorSesiones(db, _reloj).ValidarAsync(token));
    }
}
