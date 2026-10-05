using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SaveStock.Core.Comun;
using SaveStock.Core.ControlAcceso.Entidades;
using SaveStock.Core.ControlAcceso.Registro;
using SaveStock.Core.ControlAcceso.Seguridad;
using SaveStock.Core.Correo;
using SaveStock.Tests.Apoyo;

namespace SaveStock.Tests.Unitarias;

public sealed partial class ServicioRegistroTests : IDisposable
{
    private const string UrlBase = "http://savestock.prueba";

    private readonly BaseDatosEnMemoria _base = new();
    private readonly RelojFalso _reloj = new();
    private readonly ConfiguracionSaveStock _configuracion = new() { UrlBase = UrlBase };

    public void Dispose() => _base.Dispose();

    /// <summary>Un servicio nuevo con su propio contexto, como en una petición web.</summary>
    private ServicioRegistro CrearServicio()
    {
        var db = _base.CrearContexto();
        return new ServicioRegistro(db, new CorreoCola(db, _reloj), _reloj, _configuracion);
    }

    private Task<Resultado> RegistrarAsync(string correo = "ana@negocio.com", string contrasena = "clave1234", string nombre = "Ana") =>
        CrearServicio().RegistrarAsync(new SolicitudRegistro(nombre, correo, contrasena));

    private async Task<List<CorreoEnCola>> CorreosAsync()
    {
        using var db = _base.CrearContexto();
        return await db.CorreosEnCola.OrderBy(c => c.Id).ToListAsync();
    }

    private async Task<Usuario> UsuarioAsync(string correo)
    {
        using var db = _base.CrearContexto();
        return await db.Usuarios.SingleAsync(u => u.Correo == correo);
    }

    /// <summary>El token del último correo de activación encolado (el único lugar donde existe en texto).</summary>
    private async Task<string> UltimoTokenAsync()
    {
        var correo = (await CorreosAsync()).Last();
        return RegexToken().Match(correo.Cuerpo).Groups[1].Value;
    }

    [GeneratedRegex(@"/cuentas/activar\?token=([A-Za-z0-9_-]+)")]
    private static partial Regex RegexToken();

    [Fact]
    public async Task Registrar_crea_un_usuario_Estandar_inactivo_y_sin_activar()
    {
        var resultado = await RegistrarAsync(correo: "  Ana@Negocio.COM ");

        Assert.True(resultado.Exito);
        Assert.Equal(ServicioRegistro.MensajeRegistrado, resultado.Mensaje);
        var usuario = await UsuarioAsync("ana@negocio.com");
        Assert.Equal("Ana", usuario.Nombre);
        Assert.Equal(Rol.Estandar, usuario.Rol);
        Assert.False(usuario.Activo);
        Assert.Null(usuario.FechaActivacion);
        Assert.Equal(_reloj.AhoraUtc, usuario.FechaCreacion);
    }

    [Fact]
    public async Task Registrar_encola_un_solo_correo_con_el_enlace_de_activacion()
    {
        await RegistrarAsync();

        var correo = Assert.Single(await CorreosAsync());
        Assert.Equal("ana@negocio.com", correo.Destinatario);
        Assert.Equal(ServicioRegistro.AsuntoActivacion, correo.Asunto);
        Assert.Equal(EstadoCorreo.Pendiente, correo.Estado);
        Assert.Contains($"{UrlBase}/cuentas/activar?token=", correo.Cuerpo);
    }

    [Fact]
    public async Task Registrar_guarda_solo_el_hash_del_token_y_vence_en_24_horas()
    {
        await RegistrarAsync();
        var token = await UltimoTokenAsync();

        using var db = _base.CrearContexto();
        var guardado = await db.TokensActivacion.SingleAsync();
        Assert.NotEqual(token, guardado.HashToken);
        Assert.Equal(GeneradorTokens.CalcularHash(token), guardado.HashToken);
        Assert.Equal(_reloj.AhoraUtc + TimeSpan.FromHours(24), guardado.FechaVencimiento);
        Assert.False(guardado.Usado);
        Assert.False(guardado.Invalidado);
    }

    [Theory]
    [InlineData("ana@negocio.com")]
    [InlineData("ANA@Negocio.com")]
    [InlineData("  ana@negocio.com  ")]
    public async Task Registrar_un_correo_que_ya_existe_devuelve_Conflicto(string repetido)
    {
        await RegistrarAsync();

        var resultado = await RegistrarAsync(correo: repetido, nombre: "Otra Ana");

        Assert.False(resultado.Exito);
        Assert.Equal(TipoError.Conflicto, resultado.TipoError);
        Assert.Equal(ServicioRegistro.MensajeCorreoDuplicado, resultado.Mensaje);
        using var db = _base.CrearContexto();
        Assert.Equal(1, await db.Usuarios.CountAsync());
        Assert.Single(await CorreosAsync());
    }

    [Fact]
    public async Task Registrar_guarda_un_hash_distinto_para_cada_usuario_aunque_la_contrasena_sea_igual()
    {
        await RegistrarAsync(correo: "ana@negocio.com", contrasena: "clave1234");
        await RegistrarAsync(correo: "luis@negocio.com", contrasena: "clave1234");

        var ana = await UsuarioAsync("ana@negocio.com");
        var luis = await UsuarioAsync("luis@negocio.com");
        Assert.NotEqual("clave1234", ana.HashContrasena);
        Assert.DoesNotContain("clave1234", ana.HashContrasena);
        Assert.NotEqual(ana.HashContrasena, luis.HashContrasena);
        Assert.True(HasherContrasenas.Verificar("clave1234", ana.HashContrasena));
        Assert.True(HasherContrasenas.Verificar("clave1234", luis.HashContrasena));
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc123")]
    [InlineData("solamenteletras")]
    [InlineData("12345678")]
    public async Task Registrar_con_una_contrasena_que_no_cumple_la_politica_devuelve_Validacion(string contrasena)
    {
        var resultado = await RegistrarAsync(contrasena: contrasena);

        Assert.False(resultado.Exito);
        Assert.Equal(TipoError.Validacion, resultado.TipoError);
        Assert.Equal(PoliticaContrasena.MensajeIncumple, resultado.Mensaje);
        await AssertNoSeRegistroNadaAsync();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ana")]
    [InlineData("ana@")]
    [InlineData("ana@negocio")]
    [InlineData("Ana <ana@negocio.com>")]
    public async Task Registrar_con_un_correo_vacio_o_mal_formado_devuelve_Validacion(string correo)
    {
        var resultado = await RegistrarAsync(correo: correo);

        Assert.False(resultado.Exito);
        Assert.Equal(TipoError.Validacion, resultado.TipoError);
        Assert.False(string.IsNullOrWhiteSpace(resultado.Mensaje));
        await AssertNoSeRegistroNadaAsync();
    }

    [Fact]
    public async Task Registrar_con_campos_ausentes_devuelve_Validacion_sin_excepcion()
    {
        var resultado = await CrearServicio().RegistrarAsync(new SolicitudRegistro(null, null, null));

        Assert.False(resultado.Exito);
        Assert.Equal(TipoError.Validacion, resultado.TipoError);
        Assert.Equal("El nombre es obligatorio.", resultado.Mensaje);
        await AssertNoSeRegistroNadaAsync();
    }

    private async Task AssertNoSeRegistroNadaAsync()
    {
        using var db = _base.CrearContexto();
        Assert.Equal(0, await db.Usuarios.CountAsync());
        Assert.Empty(await CorreosAsync());
    }

    [Fact]
    public async Task Activar_con_el_token_del_correo_activa_la_cuenta_y_marca_el_token_usado()
    {
        await RegistrarAsync();
        var token = await UltimoTokenAsync();
        _reloj.Avanzar(TimeSpan.FromHours(1));

        var resultado = await CrearServicio().ActivarAsync(token);

        Assert.True(resultado.Exito);
        Assert.Equal(ServicioRegistro.MensajeCuentaActivada, resultado.Mensaje);
        var usuario = await UsuarioAsync("ana@negocio.com");
        Assert.True(usuario.Activo);
        Assert.Equal(_reloj.AhoraUtc, usuario.FechaActivacion);
        using var db = _base.CrearContexto();
        Assert.True((await db.TokensActivacion.SingleAsync()).Usado);
    }

    [Fact]
    public async Task Activar_dos_veces_con_el_mismo_token_rechaza_la_segunda_y_no_cambia_el_estado()
    {
        await RegistrarAsync();
        var token = await UltimoTokenAsync();
        await CrearServicio().ActivarAsync(token);
        var activadaEn = (await UsuarioAsync("ana@negocio.com")).FechaActivacion;
        _reloj.Avanzar(TimeSpan.FromMinutes(5));

        var segunda = await CrearServicio().ActivarAsync(token);

        Assert.False(segunda.Exito);
        Assert.Equal(ServicioRegistro.MensajeEnlaceInvalido, segunda.Mensaje);
        Assert.Equal(activadaEn, (await UsuarioAsync("ana@negocio.com")).FechaActivacion);
    }

    [Fact]
    public async Task Activar_con_un_token_vencido_se_rechaza_y_la_cuenta_sigue_sin_activar()
    {
        await RegistrarAsync();
        var token = await UltimoTokenAsync();
        _reloj.Avanzar(ServicioRegistro.VigenciaTokenActivacion + TimeSpan.FromSeconds(1));

        var resultado = await CrearServicio().ActivarAsync(token);

        Assert.False(resultado.Exito);
        Assert.Equal(ServicioRegistro.MensajeEnlaceInvalido, resultado.Mensaje);
        var usuario = await UsuarioAsync("ana@negocio.com");
        Assert.False(usuario.Activo);
        Assert.Null(usuario.FechaActivacion);
        using var db = _base.CrearContexto();
        Assert.False((await db.TokensActivacion.SingleAsync()).Usado);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("token-inventado")]
    public async Task Activar_con_un_token_que_no_existe_se_rechaza(string? token)
    {
        await RegistrarAsync();

        var resultado = await CrearServicio().ActivarAsync(token);

        Assert.False(resultado.Exito);
        Assert.Equal(ServicioRegistro.MensajeEnlaceInvalido, resultado.Mensaje);
        Assert.Null((await UsuarioAsync("ana@negocio.com")).FechaActivacion);
    }

    [Fact]
    public async Task Reenviar_invalida_el_enlace_anterior_y_encola_uno_nuevo_que_funciona()
    {
        await RegistrarAsync();
        var tokenViejo = await UltimoTokenAsync();

        var resultado = await CrearServicio().ReenviarActivacionAsync(new SolicitudReenvioActivacion("ANA@negocio.com"));

        Assert.True(resultado.Exito);
        Assert.Equal(ServicioRegistro.MensajeReenvio, resultado.Mensaje);
        Assert.Equal(2, (await CorreosAsync()).Count);
        var tokenNuevo = await UltimoTokenAsync();
        Assert.NotEqual(tokenViejo, tokenNuevo);

        Assert.False((await CrearServicio().ActivarAsync(tokenViejo)).Exito);
        Assert.Null((await UsuarioAsync("ana@negocio.com")).FechaActivacion);
        Assert.True((await CrearServicio().ActivarAsync(tokenNuevo)).Exito);
        Assert.True((await UsuarioAsync("ana@negocio.com")).Activo);
    }

    [Theory]
    [InlineData("nadie@negocio.com")]
    [InlineData("correo-mal-formado")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Reenviar_a_un_correo_no_registrado_responde_lo_mismo_y_no_encola_nada(string? correo)
    {
        var resultado = await CrearServicio().ReenviarActivacionAsync(new SolicitudReenvioActivacion(correo));

        Assert.True(resultado.Exito);
        Assert.Equal(ServicioRegistro.MensajeReenvio, resultado.Mensaje);
        Assert.Empty(await CorreosAsync());
    }

    [Fact]
    public async Task Reenviar_a_una_cuenta_ya_activada_responde_lo_mismo_y_no_encola_nada()
    {
        await _base.CrearUsuarioAsync("activa@negocio.com");

        var resultado = await CrearServicio().ReenviarActivacionAsync(new SolicitudReenvioActivacion("activa@negocio.com"));

        Assert.True(resultado.Exito);
        Assert.Equal(ServicioRegistro.MensajeReenvio, resultado.Mensaje);
        Assert.Empty(await CorreosAsync());
    }
}
