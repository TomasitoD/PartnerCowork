using Microsoft.EntityFrameworkCore;
using SaveStock.Core.Correo;
using SaveStock.Tests.Apoyo;

namespace SaveStock.Tests.Unitarias;

public sealed class ProcesadorColaCorreosTests : IDisposable
{
    private readonly BaseDatosEnMemoria _base = new();
    private readonly RelojFalso _reloj = new();

    public void Dispose() => _base.Dispose();

    private ProcesadorColaCorreos CrearProcesador(IEnviadorCorreo enviador) => new(_base.CrearContexto(), enviador, _reloj);

    /// <summary>Encola un correo con la cola real, como lo haría una operación de negocio.</summary>
    private async Task<int> EncolarAsync(string destinatario = "ana@negocio.com")
    {
        using var db = _base.CrearContexto();
        await new CorreoCola(db, _reloj).EncolarAsync(destinatario, "Activa tu cuenta", "Abre este enlace para activarla.");
        return await db.CorreosEnCola.MaxAsync(c => c.Id);
    }

    private async Task<CorreoEnCola> LeerAsync(int id)
    {
        using var db = _base.CrearContexto();
        return await db.CorreosEnCola.SingleAsync(c => c.Id == id);
    }

    [Fact]
    public async Task Procesar_envia_el_pendiente_y_lo_marca_enviado_con_la_fecha_del_reloj()
    {
        var id = await EncolarAsync();
        var enviador = new EnviadorFalso();
        _reloj.Avanzar(TimeSpan.FromMinutes(5));

        var resumen = await CrearProcesador(enviador).ProcesarAsync();

        Assert.Equal(1, resumen.Enviados);
        Assert.Equal(0, resumen.Fallidos);
        Assert.Equal(0, resumen.Pendientes);
        var enviado = Assert.Single(enviador.Enviados);
        Assert.Equal("ana@negocio.com", enviado.Destinatario);
        Assert.Equal("Activa tu cuenta", enviado.Asunto);

        var correo = await LeerAsync(id);
        Assert.Equal(EstadoCorreo.Enviado, correo.Estado);
        Assert.Equal(_reloj.AhoraUtc, correo.FechaEnvio);
        Assert.Null(correo.UltimoError);
    }

    [Fact]
    public async Task Procesar_dos_veces_no_reenvia_el_correo()
    {
        await EncolarAsync();
        var enviador = new EnviadorFalso();

        await CrearProcesador(enviador).ProcesarAsync();
        var segunda = await CrearProcesador(enviador).ProcesarAsync();

        Assert.Equal(1, enviador.Llamadas);
        Assert.Equal(0, segunda.Enviados);
        Assert.Equal(0, segunda.Pendientes);
    }

    [Fact]
    public async Task Un_correo_enviado_nunca_se_vuelve_a_tomar()
    {
        var id = await EncolarAsync();
        await CrearProcesador(new EnviadorFalso()).ProcesarAsync();
        var fechaEnvio = (await LeerAsync(id)).FechaEnvio;

        // Llega otro correo: solo se envía el nuevo.
        _reloj.Avanzar(TimeSpan.FromHours(1));
        var idNuevo = await EncolarAsync("luis@negocio.com");
        var enviador = new EnviadorFalso();
        await CrearProcesador(enviador).ProcesarAsync();

        var enviado = Assert.Single(enviador.Enviados);
        Assert.Equal("luis@negocio.com", enviado.Destinatario);
        Assert.Equal(fechaEnvio, (await LeerAsync(id)).FechaEnvio);
        Assert.Equal(EstadoCorreo.Enviado, (await LeerAsync(idNuevo)).Estado);
    }

    [Fact]
    public async Task Si_el_envio_falla_queda_pendiente_con_un_intento_y_el_error()
    {
        var id = await EncolarAsync();
        var enviador = new EnviadorFalso { Error = new ErrorEnvioCorreoException("No se pudo conectar con el servidor SMTP smtp.ejemplo.com:587.") };

        var resumen = await CrearProcesador(enviador).ProcesarAsync();

        Assert.Equal(0, resumen.Enviados);
        Assert.Equal(1, resumen.Fallidos);
        Assert.Equal(1, resumen.Pendientes);
        var fallo = Assert.Single(resumen.Fallos);
        Assert.Equal(id, fallo.CorreoId);

        var correo = await LeerAsync(id);
        Assert.Equal(EstadoCorreo.Pendiente, correo.Estado);
        Assert.Equal(1, correo.Intentos);
        Assert.Equal("No se pudo conectar con el servidor SMTP smtp.ejemplo.com:587.", correo.UltimoError);
        Assert.Null(correo.FechaEnvio);
    }

    [Fact]
    public async Task Un_correo_que_fallo_se_envia_en_la_siguiente_ejecucion()
    {
        var id = await EncolarAsync();
        var enviador = new EnviadorFalso { Error = new ErrorEnvioCorreoException("No se pudo conectar con el servidor SMTP.") };
        await CrearProcesador(enviador).ProcesarAsync();

        enviador.Error = null;
        var resumen = await CrearProcesador(enviador).ProcesarAsync();

        Assert.Equal(1, resumen.Enviados);
        Assert.Equal(2, enviador.Llamadas);
        var correo = await LeerAsync(id);
        Assert.Equal(EstadoCorreo.Enviado, correo.Estado);
        Assert.Equal(1, correo.Intentos);
        Assert.Null(correo.UltimoError);
    }

    [Fact]
    public async Task Un_error_inesperado_no_guarda_su_mensaje_original()
    {
        var id = await EncolarAsync();
        var enviador = new EnviadorFalso { Error = new InvalidOperationException("usuario=admin contrasena=secreta") };

        await CrearProcesador(enviador).ProcesarAsync();

        var correo = await LeerAsync(id);
        Assert.Equal(EstadoCorreo.Pendiente, correo.Estado);
        Assert.Equal("Error inesperado al enviar el correo (InvalidOperationException).", correo.UltimoError);
        Assert.DoesNotContain("secreta", correo.UltimoError);
    }

    [Fact]
    public async Task Un_error_muy_largo_se_recorta()
    {
        var id = await EncolarAsync();
        var enviador = new EnviadorFalso { Error = new ErrorEnvioCorreoException(new string('x', 1000)) };

        await CrearProcesador(enviador).ProcesarAsync();

        Assert.Equal(ProcesadorColaCorreos.LargoMaximoError, (await LeerAsync(id)).UltimoError!.Length);
    }

    [Fact]
    public async Task Un_fallo_no_impide_enviar_los_demas()
    {
        var idFalla = await EncolarAsync("falla@negocio.com");
        var idBien = await EncolarAsync("luis@negocio.com");
        var enviador = new EnviadorFalso { FallarPara = "falla@negocio.com" };

        var resumen = await CrearProcesador(enviador).ProcesarAsync();

        Assert.Equal(1, resumen.Enviados);
        Assert.Equal(1, resumen.Fallidos);
        Assert.Equal(1, resumen.Pendientes);
        Assert.Equal(EstadoCorreo.Pendiente, (await LeerAsync(idFalla)).Estado);
        Assert.Equal(EstadoCorreo.Enviado, (await LeerAsync(idBien)).Estado);
    }

    [Fact]
    public async Task Un_correo_que_otro_enviador_ya_reclamo_no_se_toca()
    {
        var id = await EncolarAsync();
        using (var db = _base.CrearContexto())
        {
            await db.CorreosEnCola.Where(c => c.Id == id)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.Estado, EstadoCorreo.Enviando));
        }

        var enviador = new EnviadorFalso();
        var resumen = await CrearProcesador(enviador).ProcesarAsync();

        Assert.Equal(0, enviador.Llamadas);
        Assert.Equal(0, resumen.Enviados);
        Assert.Equal(EstadoCorreo.Enviando, (await LeerAsync(id)).Estado);
    }

    [Fact]
    public async Task Dos_enviadores_a_la_vez_no_mandan_el_mismo_correo_dos_veces()
    {
        await EncolarAsync("ana@negocio.com");
        await EncolarAsync("luis@negocio.com");
        var enviador = new EnviadorFalso();

        // Mientras el primero envía el primer correo, arranca un segundo enviador sobre la misma base.
        ProcesadorColaCorreos? segundo = null;
        enviador.AlEnviar = async () =>
        {
            if (segundo is null)
            {
                segundo = CrearProcesador(enviador);
                await segundo.ProcesarAsync();
            }
        };

        await CrearProcesador(enviador).ProcesarAsync();

        Assert.Equal(2, enviador.Llamadas);
        Assert.Equal(["ana@negocio.com", "luis@negocio.com"], enviador.Enviados.Select(e => e.Destinatario).Order());
    }

    /// <summary>Enviador de prueba: no manda nada, cuenta las llamadas y puede simular fallas.</summary>
    private sealed class EnviadorFalso : IEnviadorCorreo
    {
        public List<(string Destinatario, string Asunto, string Cuerpo)> Enviados { get; } = [];

        public int Llamadas { get; private set; }

        /// <summary>Si no es null, toda llamada lanza esta excepción.</summary>
        public Exception? Error { get; set; }

        /// <summary>Si no es null, solo falla el envío a este destinatario.</summary>
        public string? FallarPara { get; set; }

        /// <summary>Se ejecuta al empezar cada envío (para simular otro proceso al mismo tiempo).</summary>
        public Func<Task>? AlEnviar { get; set; }

        public async Task EnviarAsync(string destinatario, string asunto, string cuerpo, CancellationToken cancelacion = default)
        {
            Llamadas++;
            if (AlEnviar is not null)
            {
                await AlEnviar();
            }

            if (Error is not null)
            {
                throw Error;
            }

            if (destinatario == FallarPara)
            {
                throw new ErrorEnvioCorreoException("El servidor SMTP rechazó el envío (código 550).");
            }

            Enviados.Add((destinatario, asunto, cuerpo));
        }
    }
}
