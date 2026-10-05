using Microsoft.EntityFrameworkCore;
using SaveStock.Core.Comun;
using SaveStock.Core.ControlAcceso.Entidades;
using SaveStock.Core.ControlAcceso.Seguridad;
using SaveStock.Core.ControlAcceso.Sesiones;
using SaveStock.Core.Correo;
using SaveStock.Core.Datos;

namespace SaveStock.Core.ControlAcceso.Contrasenas;

/// <summary>
/// Recuperación, restablecimiento y cambio de contraseña (RF-CA-09 a RF-CA-13, RF-CA-22).
/// Los códigos de recuperación son de un solo uso, vencen a los 15 minutos y en la base
/// solo se guarda su hash. Los correos se dejan en la cola; nunca se habla con SMTP.
/// </summary>
public class ServicioContrasenas
{
    public static readonly TimeSpan DuracionCodigo = TimeSpan.FromMinutes(15);

    public const string MensajeRecuperacion = "Si el correo está registrado, te enviamos un código para restablecer tu contraseña.";
    public const string AsuntoRecuperacion = "Código para restablecer tu contraseña de SaveStock";

    private readonly CoreDbContext _db;
    private readonly IReloj _reloj;
    private readonly GestorSesiones _sesiones;
    private readonly ICorreoCola _correos;
    private readonly ConfiguracionSaveStock _configuracion;

    public ServicioContrasenas(CoreDbContext db, IReloj reloj, GestorSesiones sesiones, ICorreoCola correos, ConfiguracionSaveStock configuracion)
    {
        _db = db;
        _reloj = reloj;
        _sesiones = sesiones;
        _correos = correos;
        _configuracion = configuracion;
    }

    /// <summary>
    /// Inicia la recuperación (RF-CA-09). Responde lo mismo exista o no el correo, para no revelar
    /// qué cuentas hay. Solo si existe y la cuenta está activada se emite un código y se encola el correo.
    /// </summary>
    public async Task<Resultado> IniciarRecuperacionAsync(SolicitudRecuperacion solicitud)
    {
        var validacion = ValidadorEntrada.ValidarCorreo(solicitud.Correo);
        if (!validacion.Exito)
        {
            return validacion;
        }

        var correo = ValidadorEntrada.NormalizarCorreo(solicitud.Correo!);
        var usuario = await _db.Usuarios.SingleOrDefaultAsync(u => u.Correo == correo);
        if (usuario is not null && usuario.FechaActivacion is not null)
        {
            var codigo = EmitirCodigo(usuario, OrigenCodigoRecuperacion.Usuario);
            var cuerpo = $"Hola, {usuario.Nombre}:\n\n"
                + "Recibimos una solicitud para restablecer la contraseña de tu cuenta de SaveStock.\n\n"
                + InstruccionesCodigo(usuario, codigo)
                + "Si no fuiste tú, ignora este correo: tu contraseña no cambia.\n";

            // EncolarAsync guarda también el código nuevo y los anteriores invalidados (mismo contexto).
            await _correos.EncolarAsync(usuario.Correo, AsuntoRecuperacion, cuerpo);
        }

        return Resultado.Ok(MensajeRecuperacion);
    }

    /// <summary>
    /// Invalida los códigos vigentes del usuario y agrega uno nuevo (sin guardar todavía).
    /// Devuelve el código en texto: es la única vez que existe, en la base queda su hash.
    /// </summary>
    private string EmitirCodigo(Usuario usuario, OrigenCodigoRecuperacion origen)
    {
        var anteriores = _db.CodigosRecuperacion
            .Where(c => c.UsuarioId == usuario.Id && !c.Usado && !c.Invalidado)
            .ToList();
        foreach (var anterior in anteriores)
        {
            anterior.Invalidado = true;
        }

        var codigo = GeneradorTokens.GenerarCodigoRecuperacion();
        var ahora = _reloj.AhoraUtc;
        _db.CodigosRecuperacion.Add(new CodigoRecuperacion
        {
            UsuarioId = usuario.Id,
            HashCodigo = GeneradorTokens.CalcularHash(codigo),
            FechaEmision = ahora,
            FechaVencimiento = ahora + DuracionCodigo,
            Origen = origen,
        });

        return codigo;
    }

    /// <summary>Parte del correo con el código, cuándo vence y cómo usarlo.</summary>
    private string InstruccionesCodigo(Usuario usuario, string codigo)
    {
        var vencimiento = (_reloj.AhoraUtc + DuracionCodigo).ToString("dd/MM/yyyy HH:mm");
        return $"Código: {codigo}\n\n"
            + $"El código sirve una sola vez y vence en {DuracionCodigo.TotalMinutes:0} minutos ({vencimiento} UTC).\n\n"
            + "Para definir tu contraseña nueva:\n"
            + $"- Entra a {_configuracion.UrlBase}/contrasena/restablecer, o\n"
            + $"- Envía POST {_configuracion.UrlBase}/api/contrasena/restablecer con el JSON "
            + $"{{ \"correo\": \"{usuario.Correo}\", \"codigo\": \"{codigo}\", \"contrasenaNueva\": \"...\" }}.\n\n"
            + "La contraseña nueva debe tener al menos 8 caracteres e incluir letras y números.\n\n";
    }
}
