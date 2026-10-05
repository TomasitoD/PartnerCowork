using Microsoft.EntityFrameworkCore;
using SaveStock.Core.Comun;
using SaveStock.Core.ControlAcceso.Entidades;
using SaveStock.Core.ControlAcceso.Seguridad;
using SaveStock.Core.Correo;
using SaveStock.Core.Datos;

namespace SaveStock.Core.ControlAcceso.Registro;

/// <summary>
/// Registro de cuentas (RF-CA-01, RF-CA-02, RF-CA-14, RF-CA-15). El correo de activación
/// se deja en la cola: esta operación nunca habla con el servidor SMTP.
/// </summary>
public class ServicioRegistro
{
    public const string MensajeRegistrado = "Te enviamos un correo para activar tu cuenta.";
    public const string MensajeCorreoDuplicado = "Ya existe una cuenta con ese correo.";
    public const string AsuntoActivacion = "Activa tu cuenta de SaveStock";

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
