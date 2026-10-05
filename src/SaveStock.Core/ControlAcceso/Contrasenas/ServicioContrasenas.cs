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
    public const string MensajeCodigoInvalido = "El código no es válido o ya venció.";
    public const string MensajeRestablecida = "Contraseña restablecida. Inicia sesión con tu contraseña nueva.";
    public const string MensajeActualIncorrecta = "La contraseña actual no es correcta.";
    public const string MensajeCambiada = "Contraseña actualizada. Inicia sesión de nuevo.";
    public const string MensajeUsuarioNoEncontrado = "No existe un usuario con ese id.";
    public const string AsuntoForzado = "Un administrador restableció tu contraseña de SaveStock";
    public const string MensajeForzado = "Se restableció la contraseña y se le envió al usuario un código para definir una nueva.";

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
    /// Define la contraseña nueva con un código válido (RF-CA-10, RF-CA-11, RF-CA-12): el código
    /// coincide, es del usuario de ese correo, no se usó, no se invalidó y no venció. Lo marca como
    /// usado, guarda el hash nuevo, limpia el bloqueo y revoca todas las sesiones. Si el código no
    /// sirve, la contraseña no cambia.
    /// </summary>
    public async Task<Resultado> RestablecerAsync(SolicitudRestablecimiento solicitud)
    {
        var validacion = ValidadorEntrada.ValidarCorreo(solicitud.Correo);
        if (!validacion.Exito)
        {
            return validacion;
        }

        var politica = PoliticaContrasena.Validar(solicitud.ContrasenaNueva);
        if (!politica.Exito)
        {
            return politica;
        }

        if (string.IsNullOrWhiteSpace(solicitud.Codigo))
        {
            return Resultado.Error(TipoError.Validacion, MensajeCodigoInvalido);
        }

        var correo = ValidadorEntrada.NormalizarCorreo(solicitud.Correo!);
        var usuario = await _db.Usuarios.SingleOrDefaultAsync(u => u.Correo == correo);
        if (usuario is null)
        {
            // Mismo mensaje que un código incorrecto: no revela si el correo existe.
            return Resultado.Error(TipoError.Validacion, MensajeCodigoInvalido);
        }

        // El código se escribe en mayúsculas en el correo; aceptamos que lo peguen en minúsculas o con espacios.
        var hash = GeneradorTokens.CalcularHash(solicitud.Codigo.Trim().ToUpperInvariant());
        var ahora = _reloj.AhoraUtc;
        var codigo = await _db.CodigosRecuperacion.FirstOrDefaultAsync(c =>
            c.UsuarioId == usuario.Id
            && c.HashCodigo == hash
            && !c.Usado
            && !c.Invalidado
            && c.FechaVencimiento > ahora);
        if (codigo is null)
        {
            return Resultado.Error(TipoError.Validacion, MensajeCodigoInvalido);
        }

        codigo.Usado = true;
        usuario.HashContrasena = HasherContrasenas.Hashear(solicitud.ContrasenaNueva!);
        usuario.IntentosFallidos = 0;
        usuario.BloqueadoHasta = null;

        // RevocarTodasAsync usa el mismo contexto: su SaveChanges guarda también lo anterior.
        await _sesiones.RevocarTodasAsync(usuario.Id);

        return Resultado.Ok(MensajeRestablecida);
    }

    /// <summary>
    /// Cambio de contraseña con sesión (RF-CA-22): exige la contraseña actual, aplica la política
    /// (RF-CA-14), guarda el hash nuevo y revoca todas las sesiones del usuario, incluida la que
    /// hizo el cambio (RF-CA-12).
    /// </summary>
    public async Task<Resultado> CambiarAsync(int usuarioId, SolicitudCambioContrasena solicitud)
    {
        var usuario = await _db.Usuarios.FindAsync(usuarioId);
        if (usuario is null)
        {
            return Resultado.Error(TipoError.NoEncontrado, MensajeUsuarioNoEncontrado);
        }

        if (string.IsNullOrEmpty(solicitud.ContrasenaActual)
            || !HasherContrasenas.Verificar(solicitud.ContrasenaActual, usuario.HashContrasena))
        {
            return Resultado.Error(TipoError.Validacion, MensajeActualIncorrecta);
        }

        var politica = PoliticaContrasena.Validar(solicitud.ContrasenaNueva);
        if (!politica.Exito)
        {
            return politica;
        }

        usuario.HashContrasena = HasherContrasenas.Hashear(solicitud.ContrasenaNueva!);
        usuario.IntentosFallidos = 0;
        usuario.BloqueadoHasta = null;
        await _sesiones.RevocarTodasAsync(usuario.Id);

        return Resultado.Ok(MensajeCambiada);
    }

    /// <summary>
    /// Restablecimiento forzado por un administrador (RF-CA-13): reemplaza el hash por el de una
    /// contraseña aleatoria que nadie conoce (la anterior deja de servir), revoca las sesiones,
    /// invalida los códigos anteriores y encola un código nuevo para que el usuario defina la suya.
    /// </summary>
    public async Task<Resultado> ForzarRestablecimientoAsync(int usuarioId)
    {
        var usuario = await _db.Usuarios.FindAsync(usuarioId);
        if (usuario is null)
        {
            return Resultado.Error(TipoError.NoEncontrado, MensajeUsuarioNoEncontrado);
        }

        usuario.HashContrasena = HasherContrasenas.Hashear(GeneradorTokens.GenerarContrasenaAleatoria());
        var codigo = EmitirCodigo(usuario, OrigenCodigoRecuperacion.Administrador);
        await _sesiones.RevocarTodasAsync(usuario.Id);

        var cuerpo = $"Hola, {usuario.Nombre}:\n\n"
            + "Un administrador restableció la contraseña de tu cuenta de SaveStock. Tu contraseña anterior ya no sirve "
            + "y se cerraron todas tus sesiones.\n\n"
            + InstruccionesCodigo(usuario, codigo);
        await _correos.EncolarAsync(usuario.Correo, AsuntoForzado, cuerpo);

        return Resultado.Ok(MensajeForzado);
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
