using Microsoft.EntityFrameworkCore;
using SaveStock.Core.Comun;
using SaveStock.Core.ControlAcceso.Entidades;
using SaveStock.Core.ControlAcceso.Seguridad;
using SaveStock.Core.Correo;
using SaveStock.Core.Datos;

namespace SaveStock.Core.ControlAcceso.Registro;

/// <summary>
/// Registro de cuentas (RF-CA-01, RF-CA-02, RF-CA-14, RF-CA-15), activación por enlace
/// (RF-CA-16) y reenvío del enlace (RF-CA-17). El correo de activación se deja en la cola: esta operación nunca habla con el servidor SMTP.
/// </summary>
public class ServicioRegistro
{
    public const string MensajeRegistrado = "Te enviamos un correo para activar tu cuenta.";
    public const string MensajeCorreoDuplicado = "Ya existe una cuenta con ese correo.";
    public const string AsuntoActivacion = "Activa tu cuenta de SaveStock";
    public const string MensajeCuentaActivada = "Tu cuenta fue activada. Ya puedes iniciar sesión.";
    public const string MensajeEnlaceInvalido = "El enlace no es válido, ya fue usado o venció.";
    public const string MensajeReenvio = "Si el correo está registrado y la cuenta no está activa, te enviamos un nuevo enlace.";

    /// <summary>Cuánto dura el enlace de activación desde que se emite.</summary>
    public static readonly TimeSpan VigenciaTokenActivacion = TimeSpan.FromHours(24);

    private readonly CoreDbContext _db;
    private readonly ICorreoCola _cola;
    private readonly IReloj _reloj;
    private readonly ConfiguracionSaveStock _configuracion;

    public ServicioRegistro(CoreDbContext db, ICorreoCola cola, IReloj reloj, ConfiguracionSaveStock configuracion)
    {
        _db = db;
        _cola = cola;
        _reloj = reloj;
        _configuracion = configuracion;
    }

    /// <summary>
    /// Crea la cuenta inactiva (Estándar, sin activar) y encola el correo con el enlace de activación.
    /// </summary>
    public async Task<Resultado> RegistrarAsync(SolicitudRegistro solicitud)
    {
        // 1. Validar la entrada: nombre, correo y política de contraseña (RF-CA-14).
        var validacion = Validar(solicitud);
        if (!validacion.Exito)
        {
            return validacion;
        }

        // 2. Correo único (RF-CA-01). Se compara ya normalizado: "Ana@X.com " es el mismo que "ana@x.com".
        var correo = ValidadorEntrada.NormalizarCorreo(solicitud.Correo!);
        if (await _db.Usuarios.AnyAsync(u => u.Correo == correo))
        {
            return Resultado.Error(TipoError.Conflicto, MensajeCorreoDuplicado);
        }

        // 3. El usuario nace inactivo y sin activar (RF-CA-15). Solo se guarda el hash de la contraseña (RF-CA-02).
        var ahora = _reloj.AhoraUtc;
        var usuario = new Usuario
        {
            Nombre = solicitud.Nombre!.Trim(),
            Correo = correo,
            HashContrasena = HasherContrasenas.Hashear(solicitud.Contrasena!),
            Rol = Rol.Estandar,
            Activo = false,
            FechaActivacion = null,
            FechaCreacion = ahora,
        };

        // Una transacción: si falla encolar el correo, tampoco queda el usuario creado.
        await using var transaccion = await _db.Database.BeginTransactionAsync();
        _db.Usuarios.Add(usuario);
        await _db.SaveChangesAsync();
        await EmitirEnlaceActivacionAsync(usuario);
        await transaccion.CommitAsync();

        return Resultado.Ok(MensajeRegistrado);
    }

    /// <summary>
    /// Activa la cuenta con el token del enlace (RF-CA-16). El token sirve una sola vez y solo
    /// antes de vencer; si no es válido, no se cambia nada.
    /// </summary>
    public async Task<Resultado> ActivarAsync(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Resultado.Error(TipoError.Validacion, MensajeEnlaceInvalido);
        }

        // En la base solo está el hash: se busca por el hash del token recibido.
        var hash = GeneradorTokens.CalcularHash(token);
        var tokenActivacion = await _db.TokensActivacion
            .Include(t => t.Usuario)
            .SingleOrDefaultAsync(t => t.HashToken == hash);

        var ahora = _reloj.AhoraUtc;
        var esValido = tokenActivacion is not null
            && !tokenActivacion.Usado
            && !tokenActivacion.Invalidado
            && tokenActivacion.FechaVencimiento > ahora;

        if (!esValido)
        {
            return Resultado.Error(TipoError.Validacion, MensajeEnlaceInvalido);
        }

        tokenActivacion!.Usado = true;
        tokenActivacion.Usuario.Activo = true;
        tokenActivacion.Usuario.FechaActivacion = ahora;
        await _db.SaveChangesAsync();

        return Resultado.Ok(MensajeCuentaActivada);
    }

    /// <summary>
    /// Reenvía el enlace de activación (RF-CA-17). Siempre responde el mismo mensaje, exista o no
    /// el correo, para no revelar qué correos están registrados. Solo si la cuenta existe y todavía
    /// no se activó, invalida los enlaces anteriores y encola uno nuevo.
    /// </summary>
    public async Task<Resultado> ReenviarActivacionAsync(SolicitudReenvioActivacion solicitud)
    {
        if (!ValidadorEntrada.ValidarCorreo(solicitud.Correo).Exito)
        {
            return Resultado.Ok(MensajeReenvio);
        }

        var correo = ValidadorEntrada.NormalizarCorreo(solicitud.Correo!);
        var usuario = await _db.Usuarios.SingleOrDefaultAsync(u => u.Correo == correo);
        if (usuario is null || usuario.FechaActivacion is not null)
        {
            return Resultado.Ok(MensajeReenvio);
        }

        // Los enlaces anteriores dejan de servir: solo vale el último que se envió.
        var anteriores = await _db.TokensActivacion
            .Where(t => t.UsuarioId == usuario.Id && !t.Usado && !t.Invalidado)
            .ToListAsync();

        await using var transaccion = await _db.Database.BeginTransactionAsync();
        foreach (var anterior in anteriores)
        {
            anterior.Invalidado = true;
        }

        await _db.SaveChangesAsync();
        await EmitirEnlaceActivacionAsync(usuario);
        await transaccion.CommitAsync();

        return Resultado.Ok(MensajeReenvio);
    }

    private static Resultado Validar(SolicitudRegistro solicitud)
    {
        var nombre = ValidadorEntrada.ValidarTextoObligatorio(solicitud.Nombre, "El nombre", ValidadorEntrada.LongitudMaximaNombre);
        if (!nombre.Exito)
        {
            return nombre;
        }

        var correo = ValidadorEntrada.ValidarCorreo(solicitud.Correo);
        if (!correo.Exito)
        {
            return correo;
        }

        return PoliticaContrasena.Validar(solicitud.Contrasena);
    }

    /// <summary>
    /// Crea un token de activación nuevo (en la base solo queda su hash) y encola el correo con el enlace.
    /// El token en texto existe únicamente dentro del correo.
    /// </summary>
    private async Task EmitirEnlaceActivacionAsync(Usuario usuario)
    {
        var token = GeneradorTokens.GenerarToken();
        var ahora = _reloj.AhoraUtc;
        _db.TokensActivacion.Add(new TokenActivacion
        {
            UsuarioId = usuario.Id,
            HashToken = GeneradorTokens.CalcularHash(token),
            FechaEmision = ahora,
            FechaVencimiento = ahora + VigenciaTokenActivacion,
            Usado = false,
            Invalidado = false,
        });
        await _db.SaveChangesAsync();

        // El token es Base64Url: se puede poner en la URL sin escaparlo.
        var enlace = $"{_configuracion.UrlBase}/cuentas/activar?token={token}";
        var cuerpo = $"""
            Hola {usuario.Nombre}:

            Para activar tu cuenta de SaveStock, abre este enlace:
            {enlace}

            El enlace vence en 24 horas y solo se puede usar una vez.
            Si no creaste esta cuenta, ignora este correo.
            """;

        await _cola.EncolarAsync(usuario.Correo, AsuntoActivacion, cuerpo);
    }
}
