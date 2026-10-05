using Microsoft.EntityFrameworkCore;
using SaveStock.Core.Comun;
using SaveStock.Core.ControlAcceso.Seguridad;
using SaveStock.Core.ControlAcceso.Sesiones;
using SaveStock.Core.Datos;

namespace SaveStock.Core.ControlAcceso.InicioSesion;

/// <summary>
/// Inicio y cierre de sesión (RF-CA-03, RF-CA-18).
/// </summary>
public class ServicioInicioSesion
{
    public const string MensajeCredencialesIncorrectas = "Correo o contraseña incorrectos.";
    public const string MensajeCuentaNoActivada = "La cuenta no está activa. Revisa tu correo para activarla.";
    public const string MensajeCuentaDesactivada = "La cuenta está desactivada. Contacta a un administrador.";
    public const string MensajeContrasenaObligatoria = "La contraseña es obligatoria.";

    /// <summary>
    /// Hash de una contraseña que nadie conoce. Si el correo no existe se verifica contra este hash
    /// para que la respuesta tarde lo mismo que con un correo real (no revela qué correos existen).
    /// </summary>
    private static readonly string HashFicticio = HasherContrasenas.Hashear(GeneradorTokens.GenerarContrasenaAleatoria());

    private readonly CoreDbContext _db;
    private readonly IReloj _reloj;
    private readonly GestorSesiones _gestorSesiones;

    public ServicioInicioSesion(CoreDbContext db, IReloj reloj, GestorSesiones gestorSesiones)
    {
        _db = db;
        _reloj = reloj;
        _gestorSesiones = gestorSesiones;
    }

    /// <summary>
    /// Verifica las credenciales y, si son correctas y la cuenta puede entrar, abre una sesión.
    /// Correo inexistente y contraseña incorrecta dan exactamente el mismo error (RF-CA-03).
    /// </summary>
    public async Task<Resultado<SesionCreada>> IniciarAsync(string? correo, string? contrasena)
    {
        var correoValido = ValidadorEntrada.ValidarCorreo(correo);
        if (!correoValido.Exito)
        {
            return Resultado<SesionCreada>.Error(correoValido.TipoError, correoValido.Mensaje);
        }

        if (string.IsNullOrEmpty(contrasena))
        {
            return Resultado<SesionCreada>.Error(TipoError.Validacion, MensajeContrasenaObligatoria);
        }

        if (contrasena.Length > ValidadorEntrada.LongitudMaximaContrasena)
        {
            return Resultado<SesionCreada>.Error(TipoError.Validacion, $"La contraseña no puede superar los {ValidadorEntrada.LongitudMaximaContrasena} caracteres.");
        }

        var correoNormalizado = ValidadorEntrada.NormalizarCorreo(correo!);
        var usuario = await _db.Usuarios.SingleOrDefaultAsync(u => u.Correo == correoNormalizado);
        if (usuario is null)
        {
            HasherContrasenas.Verificar(contrasena, HashFicticio);
            return Resultado<SesionCreada>.Error(TipoError.NoAutenticado, MensajeCredencialesIncorrectas);
        }

        if (!HasherContrasenas.Verificar(contrasena, usuario.HashContrasena))
        {
            return Resultado<SesionCreada>.Error(TipoError.NoAutenticado, MensajeCredencialesIncorrectas);
        }

        // La contraseña es correcta, pero la cuenta tiene que estar activada y no desactivada.
        if (usuario.FechaActivacion is null)
        {
            return Resultado<SesionCreada>.Error(TipoError.Prohibido, MensajeCuentaNoActivada);
        }

        if (!usuario.Activo)
        {
            return Resultado<SesionCreada>.Error(TipoError.Prohibido, MensajeCuentaDesactivada);
        }

        var sesion = await _gestorSesiones.CrearAsync(usuario);
        return Resultado<SesionCreada>.Ok(sesion);
    }
}
