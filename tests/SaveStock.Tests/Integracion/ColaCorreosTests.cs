using Microsoft.Extensions.DependencyInjection;
using SaveStock.Core.Comun;
using SaveStock.Core.Correo;
using SaveStock.Core.Datos;

namespace SaveStock.Tests.Integracion;

/// <summary>
/// La web solo encola; el procesador (lo que corre SaveStock.Enviador) envía desde el mismo archivo SQLite.
/// </summary>
public class ColaCorreosTests : IClassFixture<SaveStockFactory>
{
    private readonly SaveStockFactory _fabrica;

    public ColaCorreosTests(SaveStockFactory fabrica)
    {
        _fabrica = fabrica;
    }

    [Fact]
    public void La_web_no_tiene_ningun_enviador_de_correos()
    {
        // RF-NOT-08: ninguna operación de la web puede hablar con el servidor SMTP.
        using var alcance = _fabrica.Services.CreateScope();

        Assert.Null(alcance.ServiceProvider.GetService<IEnviadorCorreo>());
        Assert.Null(alcance.ServiceProvider.GetService<ProcesadorColaCorreos>());
    }

    [Fact]
    public async Task Con_el_smtp_caido_el_correo_queda_pendiente_y_sale_en_la_siguiente_ejecucion_una_sola_vez()
    {
        var destinatario = $"{Guid.NewGuid():N}@negocio.com";
        using (var alcance = _fabrica.Services.CreateScope())
        {
            await alcance.ServiceProvider.GetRequiredService<ICorreoCola>()
                .EncolarAsync(destinatario, "Activa tu cuenta", "Abre este enlace para activarla.");
        }

        // Primera ejecución: el servidor SMTP no responde.
        var caido = new EnviadorDePrueba { Falla = true };
        await ProcesarAsync(caido);
        var correo = (await _fabrica.LeerCorreosEnColaAsync()).Single(c => c.Destinatario == destinatario);
        Assert.Equal(EstadoCorreo.Pendiente, correo.Estado);
        Assert.Equal(1, correo.Intentos);
        Assert.NotNull(correo.UltimoError);

        // Segunda y tercera ejecución con el servidor de vuelta: se envía una sola vez (RF-NOT-12).
        var enLinea = new EnviadorDePrueba();
        await ProcesarAsync(enLinea);
        await ProcesarAsync(enLinea);

        Assert.Equal(1, enLinea.Destinatarios.Count(d => d == destinatario));
        correo = (await _fabrica.LeerCorreosEnColaAsync()).Single(c => c.Destinatario == destinatario);
        Assert.Equal(EstadoCorreo.Enviado, correo.Estado);
        Assert.NotNull(correo.FechaEnvio);
    }

    /// <summary>Corre el procesador sobre la base de la web, como lo hace SaveStock.Enviador.</summary>
    private async Task<ResumenEnvio> ProcesarAsync(IEnviadorCorreo enviador)
    {
        using var alcance = _fabrica.Services.CreateScope();
        var proveedor = alcance.ServiceProvider;
        var procesador = new ProcesadorColaCorreos(
            proveedor.GetRequiredService<CoreDbContext>(), enviador, proveedor.GetRequiredService<IReloj>());
        return await procesador.ProcesarAsync();
    }

    private sealed class EnviadorDePrueba : IEnviadorCorreo
    {
        public bool Falla { get; init; }

        public List<string> Destinatarios { get; } = [];

        public Task EnviarAsync(string destinatario, string asunto, string cuerpo, CancellationToken cancelacion = default)
        {
            if (Falla)
            {
                throw new ErrorEnvioCorreoException("No se pudo conectar con el servidor SMTP.");
            }

            Destinatarios.Add(destinatario);
            return Task.CompletedTask;
        }
    }
}
