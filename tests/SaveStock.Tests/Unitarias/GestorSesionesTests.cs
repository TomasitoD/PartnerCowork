using Microsoft.EntityFrameworkCore;
using SaveStock.Core.ControlAcceso.Seguridad;
using SaveStock.Core.ControlAcceso.Sesiones;
using SaveStock.Tests.Apoyo;

namespace SaveStock.Tests.Unitarias;

public sealed class GestorSesionesTests : IDisposable
{
    private readonly BaseDatosEnMemoria _base = new();
    private readonly RelojFalso _reloj = new();

    public void Dispose() => _base.Dispose();

    private GestorSesiones CrearGestor() => new(_base.CrearContexto(), _reloj);

    [Fact]
    public async Task Crear_devuelve_un_token_que_vence_en_8_horas_y_guarda_solo_su_hash()
    {
        var usuario = await _base.CrearUsuarioAsync("ana@negocio.com");

        var creada = await CrearGestor().CrearAsync(usuario);

        Assert.Equal(_reloj.AhoraUtc.AddHours(8), creada.FechaVencimiento);
        using var db = _base.CrearContexto();
        var guardada = await db.Sesiones.SingleAsync();
        Assert.NotEqual(creada.Token, guardada.HashToken);
        Assert.Equal(GeneradorTokens.CalcularHash(creada.Token), guardada.HashToken);
    }

    [Fact]
    public async Task Validar_devuelve_la_sesion_con_su_usuario_si_el_token_es_valido()
    {
        var usuario = await _base.CrearUsuarioAsync("ana@negocio.com");
        var creada = await CrearGestor().CrearAsync(usuario);

        var sesion = await CrearGestor().ValidarAsync(creada.Token);

        Assert.NotNull(sesion);
        Assert.Equal(usuario.Id, sesion.Usuario.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("token-que-no-existe")]
    public async Task Validar_devuelve_null_si_el_token_no_existe(string? token)
    {
        Assert.Null(await CrearGestor().ValidarAsync(token));
    }

    [Fact]
    public async Task Validar_devuelve_null_si_la_sesion_vencio()
    {
        var usuario = await _base.CrearUsuarioAsync("ana@negocio.com");
        var creada = await CrearGestor().CrearAsync(usuario);

        _reloj.Avanzar(GestorSesiones.DuracionSesion + TimeSpan.FromSeconds(1));

        Assert.Null(await CrearGestor().ValidarAsync(creada.Token));
    }

    [Fact]
    public async Task Validar_devuelve_null_si_el_usuario_esta_inactivo()
    {
        var usuario = await _base.CrearUsuarioAsync("ana@negocio.com", activo: false);
        var creada = await CrearGestor().CrearAsync(usuario);

        Assert.Null(await CrearGestor().ValidarAsync(creada.Token));
    }

    [Fact]
    public async Task Revocar_deja_el_token_sin_validez()
    {
        var usuario = await _base.CrearUsuarioAsync("ana@negocio.com");
        var creada = await CrearGestor().CrearAsync(usuario);

        Assert.True(await CrearGestor().RevocarAsync(creada.Token));

        Assert.Null(await CrearGestor().ValidarAsync(creada.Token));
        Assert.False(await CrearGestor().RevocarAsync(creada.Token));

        // La fecha de revocación vuelve de la base marcada como UTC (RD-11).
        using var db = _base.CrearContexto();
        var guardada = await db.Sesiones.SingleAsync();
        Assert.Equal(DateTimeKind.Utc, guardada.FechaRevocacion!.Value.Kind);
        Assert.Equal(_reloj.AhoraUtc, guardada.FechaRevocacion);
    }

    [Fact]
    public async Task Revocar_todas_cierra_solo_las_sesiones_de_ese_usuario()
    {
        var ana = await _base.CrearUsuarioAsync("ana@negocio.com");
        var luis = await _base.CrearUsuarioAsync("luis@negocio.com");
        var primera = await CrearGestor().CrearAsync(ana);
        var segunda = await CrearGestor().CrearAsync(ana);
        var deLuis = await CrearGestor().CrearAsync(luis);

        var revocadas = await CrearGestor().RevocarTodasAsync(ana.Id);

        Assert.Equal(2, revocadas);
        Assert.Null(await CrearGestor().ValidarAsync(primera.Token));
        Assert.Null(await CrearGestor().ValidarAsync(segunda.Token));
        Assert.NotNull(await CrearGestor().ValidarAsync(deLuis.Token));
    }
}
