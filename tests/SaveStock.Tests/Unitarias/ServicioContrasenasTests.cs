using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SaveStock.Core.Comun;
using SaveStock.Core.ControlAcceso.Contrasenas;
using SaveStock.Core.ControlAcceso.Entidades;
using SaveStock.Core.ControlAcceso.Seguridad;
using SaveStock.Core.ControlAcceso.Sesiones;
using SaveStock.Core.Correo;
using SaveStock.Tests.Apoyo;

namespace SaveStock.Tests.Unitarias;

public sealed partial class ServicioContrasenasTests : IDisposable
{
    private const string ContrasenaInicial = "clave1234";
    private const string ContrasenaNueva = "nueva5678";

    private readonly BaseDatosEnMemoria _base = new();
    private readonly RelojFalso _reloj = new();
    private readonly ConfiguracionSaveStock _configuracion = new() { UrlBase = "http://localhost:5080" };

    public void Dispose() => _base.Dispose();

    /// <summary>Arma el servicio como lo haría la inyección de dependencias: todo sobre el mismo contexto.</summary>
    private ServicioContrasenas CrearServicio()
    {
        var db = _base.CrearContexto();
        return new ServicioContrasenas(db, _reloj, new GestorSesiones(db, _reloj), new CorreoCola(db, _reloj), _configuracion);
    }

    private GestorSesiones CrearGestor() => new(_base.CrearContexto(), _reloj);

    private async Task<Usuario> LeerUsuarioAsync(int id)
    {
        using var db = _base.CrearContexto();
        return await db.Usuarios.SingleAsync(u => u.Id == id);
    }

    private async Task<List<CorreoEnCola>> LeerCorreosAsync()
    {
        using var db = _base.CrearContexto();
        return await db.CorreosEnCola.OrderBy(c => c.Id).ToListAsync();
    }

    [GeneratedRegex(@"Código: ([A-Z2-9]{8})")]
    private static partial Regex PatronCodigo();

    private static string ExtraerCodigo(CorreoEnCola correo) => PatronCodigo().Match(correo.Cuerpo).Groups[1].Value;

    private async Task<string> PedirCodigoAsync(string correo)
    {
        await CrearServicio().IniciarRecuperacionAsync(new SolicitudRecuperacion(correo));
        return ExtraerCodigo((await LeerCorreosAsync()).Last());
    }

    [Fact]
    public async Task Recuperar_responde_lo_mismo_exista_o_no_el_correo_y_solo_encola_si_existe()
    {
        var ana = await _base.CrearUsuarioAsync("ana@negocio.com");

        var existente = await CrearServicio().IniciarRecuperacionAsync(new SolicitudRecuperacion("ana@negocio.com"));
        var inexistente = await CrearServicio().IniciarRecuperacionAsync(new SolicitudRecuperacion("nadie@negocio.com"));

        Assert.True(existente.Exito);
        Assert.True(inexistente.Exito);
        Assert.Equal(existente.Mensaje, inexistente.Mensaje);
        Assert.Equal(ServicioContrasenas.MensajeRecuperacion, existente.Mensaje);

        var correo = Assert.Single(await LeerCorreosAsync());
        Assert.Equal(ana.Correo, correo.Destinatario);
        Assert.Equal(ServicioContrasenas.AsuntoRecuperacion, correo.Asunto);
        Assert.Equal(EstadoCorreo.Pendiente, correo.Estado);
    }

    [Fact]
    public async Task Recuperar_no_encola_nada_si_la_cuenta_no_esta_activada()
    {
        await _base.CrearUsuarioAsync("ana@negocio.com", activo: false);

        var resultado = await CrearServicio().IniciarRecuperacionAsync(new SolicitudRecuperacion("ana@negocio.com"));

        Assert.Equal(ServicioContrasenas.MensajeRecuperacion, resultado.Mensaje);
        Assert.Empty(await LeerCorreosAsync());
    }

    [Fact]
    public async Task Recuperar_rechaza_un_correo_mal_formado()
    {
        var resultado = await CrearServicio().IniciarRecuperacionAsync(new SolicitudRecuperacion("no-es-un-correo"));

        Assert.False(resultado.Exito);
        Assert.Equal(TipoError.Validacion, resultado.TipoError);
    }

    [Fact]
    public async Task El_codigo_se_guarda_solo_como_hash_vence_en_15_minutos_y_el_correo_explica_como_usarlo()
    {
        var ana = await _base.CrearUsuarioAsync("ana@negocio.com");

        var codigo = await PedirCodigoAsync("ana@negocio.com");

        Assert.Matches("^[ABCDEFGHJKLMNPQRSTUVWXYZ23456789]{8}$", codigo);
        using var db = _base.CrearContexto();
        var guardado = await db.CodigosRecuperacion.SingleAsync();
        Assert.Equal(ana.Id, guardado.UsuarioId);
        Assert.Equal(GeneradorTokens.CalcularHash(codigo), guardado.HashCodigo);
        Assert.DoesNotContain(codigo, guardado.HashCodigo);
        Assert.Equal(_reloj.AhoraUtc.AddMinutes(15), guardado.FechaVencimiento);
        Assert.Equal(OrigenCodigoRecuperacion.Usuario, guardado.Origen);

        var cuerpo = (await LeerCorreosAsync()).Single().Cuerpo;
        Assert.Contains("15 minutos", cuerpo);
        Assert.Contains("POST http://localhost:5080/api/contrasena/restablecer", cuerpo);
        Assert.Contains("http://localhost:5080/contrasena/restablecer", cuerpo);
    }

    [Fact]
    public async Task Un_codigo_nuevo_invalida_los_anteriores()
    {
        var ana = await _base.CrearUsuarioAsync("ana@negocio.com");
        var primero = await PedirCodigoAsync("ana@negocio.com");
        var segundo = await PedirCodigoAsync("ana@negocio.com");

        var conElPrimero = await CrearServicio().RestablecerAsync(new SolicitudRestablecimiento("ana@negocio.com", primero, ContrasenaNueva));

        Assert.False(conElPrimero.Exito);
        Assert.Equal(ServicioContrasenas.MensajeCodigoInvalido, conElPrimero.Mensaje);
        Assert.True(HasherContrasenas.Verificar(ContrasenaInicial, (await LeerUsuarioAsync(ana.Id)).HashContrasena));

        var conElSegundo = await CrearServicio().RestablecerAsync(new SolicitudRestablecimiento("ana@negocio.com", segundo, ContrasenaNueva));
        Assert.True(conElSegundo.Exito);
    }

    [Fact]
    public async Task Restablecer_con_un_codigo_valido_guarda_el_hash_nuevo_y_la_anterior_deja_de_servir()
    {
        var ana = await _base.CrearUsuarioAsync("ana@negocio.com");
        var codigo = await PedirCodigoAsync("ana@negocio.com");

        var resultado = await CrearServicio().RestablecerAsync(new SolicitudRestablecimiento("ana@negocio.com", codigo, ContrasenaNueva));

        Assert.True(resultado.Exito);
        var hash = (await LeerUsuarioAsync(ana.Id)).HashContrasena;
        Assert.DoesNotContain(ContrasenaNueva, hash);
        Assert.True(HasherContrasenas.Verificar(ContrasenaNueva, hash));
        Assert.False(HasherContrasenas.Verificar(ContrasenaInicial, hash));
    }

    [Fact]
    public async Task Restablecer_acepta_el_codigo_en_minusculas_y_con_espacios()
    {
        await _base.CrearUsuarioAsync("ana@negocio.com");
        var codigo = await PedirCodigoAsync("ana@negocio.com");

        var resultado = await CrearServicio().RestablecerAsync(new SolicitudRestablecimiento("ana@negocio.com", $"  {codigo.ToLowerInvariant()} ", ContrasenaNueva));

        Assert.True(resultado.Exito);
    }

    [Fact]
    public async Task Usar_el_codigo_dos_veces_se_rechaza_y_la_contrasena_no_cambia()
    {
        var ana = await _base.CrearUsuarioAsync("ana@negocio.com");
        var codigo = await PedirCodigoAsync("ana@negocio.com");
        await CrearServicio().RestablecerAsync(new SolicitudRestablecimiento("ana@negocio.com", codigo, ContrasenaNueva));

        var segundaVez = await CrearServicio().RestablecerAsync(new SolicitudRestablecimiento("ana@negocio.com", codigo, "otra9999"));

        Assert.False(segundaVez.Exito);
        Assert.Equal(TipoError.Validacion, segundaVez.TipoError);
        Assert.Equal(ServicioContrasenas.MensajeCodigoInvalido, segundaVez.Mensaje);
        var hash = (await LeerUsuarioAsync(ana.Id)).HashContrasena;
        Assert.True(HasherContrasenas.Verificar(ContrasenaNueva, hash));
        Assert.False(HasherContrasenas.Verificar("otra9999", hash));
    }

    [Fact]
    public async Task Un_codigo_vencido_se_rechaza_y_la_contrasena_no_cambia()
    {
        var ana = await _base.CrearUsuarioAsync("ana@negocio.com");
        var codigo = await PedirCodigoAsync("ana@negocio.com");

        _reloj.Avanzar(ServicioContrasenas.DuracionCodigo + TimeSpan.FromSeconds(1));
        var resultado = await CrearServicio().RestablecerAsync(new SolicitudRestablecimiento("ana@negocio.com", codigo, ContrasenaNueva));

        Assert.False(resultado.Exito);
        Assert.Equal(ServicioContrasenas.MensajeCodigoInvalido, resultado.Mensaje);
        Assert.True(HasherContrasenas.Verificar(ContrasenaInicial, (await LeerUsuarioAsync(ana.Id)).HashContrasena));
    }

    [Fact]
    public async Task El_codigo_de_un_usuario_no_sirve_para_otro()
    {
        await _base.CrearUsuarioAsync("ana@negocio.com");
        var luis = await _base.CrearUsuarioAsync("luis@negocio.com");
        var codigoDeAna = await PedirCodigoAsync("ana@negocio.com");

        var resultado = await CrearServicio().RestablecerAsync(new SolicitudRestablecimiento("luis@negocio.com", codigoDeAna, ContrasenaNueva));

        Assert.False(resultado.Exito);
        Assert.True(HasherContrasenas.Verificar(ContrasenaInicial, (await LeerUsuarioAsync(luis.Id)).HashContrasena));
    }

    [Fact]
    public async Task Restablecer_aplica_la_politica_de_contrasena()
    {
        var ana = await _base.CrearUsuarioAsync("ana@negocio.com");
        var codigo = await PedirCodigoAsync("ana@negocio.com");

        var resultado = await CrearServicio().RestablecerAsync(new SolicitudRestablecimiento("ana@negocio.com", codigo, "corta"));

        Assert.False(resultado.Exito);
        Assert.Equal(PoliticaContrasena.MensajeIncumple, resultado.Mensaje);
        Assert.True(HasherContrasenas.Verificar(ContrasenaInicial, (await LeerUsuarioAsync(ana.Id)).HashContrasena));
    }

    [Fact]
    public async Task Restablecer_revoca_las_sesiones_anteriores_y_limpia_el_bloqueo()
    {
        var ana = await _base.CrearUsuarioAsync("ana@negocio.com");
        var sesion = await CrearGestor().CrearAsync(ana);
        using (var db = _base.CrearContexto())
        {
            var guardada = await db.Usuarios.SingleAsync(u => u.Id == ana.Id);
            guardada.IntentosFallidos = 3;
            guardada.BloqueadoHasta = _reloj.AhoraUtc.AddMinutes(10);
            await db.SaveChangesAsync();
        }

        var codigo = await PedirCodigoAsync("ana@negocio.com");
        await CrearServicio().RestablecerAsync(new SolicitudRestablecimiento("ana@negocio.com", codigo, ContrasenaNueva));

        Assert.Null(await CrearGestor().ValidarAsync(sesion.Token));
        var usuario = await LeerUsuarioAsync(ana.Id);
        Assert.Equal(0, usuario.IntentosFallidos);
        Assert.Null(usuario.BloqueadoHasta);
    }

    [Fact]
    public async Task Cambiar_con_la_contrasena_actual_incorrecta_se_rechaza()
    {
        var ana = await _base.CrearUsuarioAsync("ana@negocio.com");

        var resultado = await CrearServicio().CambiarAsync(ana.Id, new SolicitudCambioContrasena("equivocada1", ContrasenaNueva));

        Assert.False(resultado.Exito);
        Assert.Equal(TipoError.Validacion, resultado.TipoError);
        Assert.Equal(ServicioContrasenas.MensajeActualIncorrecta, resultado.Mensaje);
        Assert.True(HasherContrasenas.Verificar(ContrasenaInicial, (await LeerUsuarioAsync(ana.Id)).HashContrasena));
    }

    [Fact]
    public async Task Cambiar_a_una_contrasena_debil_se_rechaza()
    {
        var ana = await _base.CrearUsuarioAsync("ana@negocio.com");

        var resultado = await CrearServicio().CambiarAsync(ana.Id, new SolicitudCambioContrasena(ContrasenaInicial, "sololetras"));

        Assert.False(resultado.Exito);
        Assert.Equal(PoliticaContrasena.MensajeIncumple, resultado.Mensaje);
        Assert.True(HasherContrasenas.Verificar(ContrasenaInicial, (await LeerUsuarioAsync(ana.Id)).HashContrasena));
    }

    [Fact]
    public async Task Cambiar_guarda_la_nueva_y_revoca_todas_las_sesiones()
    {
        var ana = await _base.CrearUsuarioAsync("ana@negocio.com");
        var primera = await CrearGestor().CrearAsync(ana);
        var segunda = await CrearGestor().CrearAsync(ana);

        var resultado = await CrearServicio().CambiarAsync(ana.Id, new SolicitudCambioContrasena(ContrasenaInicial, ContrasenaNueva));

        Assert.True(resultado.Exito);
        Assert.Equal("Contraseña actualizada. Inicia sesión de nuevo.", resultado.Mensaje);
        var hash = (await LeerUsuarioAsync(ana.Id)).HashContrasena;
        Assert.True(HasherContrasenas.Verificar(ContrasenaNueva, hash));
        Assert.False(HasherContrasenas.Verificar(ContrasenaInicial, hash));
        Assert.Null(await CrearGestor().ValidarAsync(primera.Token));
        Assert.Null(await CrearGestor().ValidarAsync(segunda.Token));
    }

    [Fact]
    public async Task Forzar_invalida_la_contrasena_y_las_sesiones_y_encola_un_codigo_que_sirve()
    {
        var ana = await _base.CrearUsuarioAsync("ana@negocio.com");
        var sesion = await CrearGestor().CrearAsync(ana);
        var codigoViejo = await PedirCodigoAsync("ana@negocio.com");

        var resultado = await CrearServicio().ForzarRestablecimientoAsync(ana.Id);

        Assert.True(resultado.Exito);
        Assert.False(HasherContrasenas.Verificar(ContrasenaInicial, (await LeerUsuarioAsync(ana.Id)).HashContrasena));
        Assert.Null(await CrearGestor().ValidarAsync(sesion.Token));

        var correo = (await LeerCorreosAsync()).Last();
        Assert.Equal(ana.Correo, correo.Destinatario);
        Assert.Equal(ServicioContrasenas.AsuntoForzado, correo.Asunto);

        using (var db = _base.CrearContexto())
        {
            var vigente = await db.CodigosRecuperacion.SingleAsync(c => !c.Invalidado);
            Assert.Equal(OrigenCodigoRecuperacion.Administrador, vigente.Origen);
        }

        var conElViejo = await CrearServicio().RestablecerAsync(new SolicitudRestablecimiento("ana@negocio.com", codigoViejo, ContrasenaNueva));
        Assert.False(conElViejo.Exito);

        var conElNuevo = await CrearServicio().RestablecerAsync(new SolicitudRestablecimiento("ana@negocio.com", ExtraerCodigo(correo), ContrasenaNueva));
        Assert.True(conElNuevo.Exito);
        Assert.True(HasherContrasenas.Verificar(ContrasenaNueva, (await LeerUsuarioAsync(ana.Id)).HashContrasena));
    }

    [Fact]
    public async Task Forzar_con_un_id_inexistente_da_no_encontrado()
    {
        var resultado = await CrearServicio().ForzarRestablecimientoAsync(999);

        Assert.False(resultado.Exito);
        Assert.Equal(TipoError.NoEncontrado, resultado.TipoError);
        Assert.Empty(await LeerCorreosAsync());
    }
}
