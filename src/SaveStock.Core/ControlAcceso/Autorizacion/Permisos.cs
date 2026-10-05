namespace SaveStock.Core.ControlAcceso.Autorizacion;

/// <summary>
/// EL único lugar donde se lee qué rol exige cada operación (RF-CA-05).
/// La Web lo aplica del lado del servidor en cada endpoint (RD-06).
/// </summary>
public static class Permisos
{
    public static readonly IReadOnlyDictionary<Operacion, NivelAcceso> PorOperacion = new Dictionary<Operacion, NivelAcceso>
    {
        [Operacion.Salud] = NivelAcceso.Publico,

        [Operacion.Registrar] = NivelAcceso.Publico,
        [Operacion.ActivarCuenta] = NivelAcceso.Publico,
        [Operacion.ReenviarActivacion] = NivelAcceso.Publico,
        [Operacion.IniciarSesion] = NivelAcceso.Publico,
        [Operacion.IniciarRecuperacion] = NivelAcceso.Publico,
        [Operacion.RestablecerContrasena] = NivelAcceso.Publico,

        [Operacion.ConsultarUsuarioActual] = NivelAcceso.Autenticado,
        [Operacion.CerrarSesion] = NivelAcceso.Autenticado,
        [Operacion.CambiarContrasena] = NivelAcceso.Autenticado,

        [Operacion.ListarUsuarios] = NivelAcceso.Administrador,
        [Operacion.CambiarRol] = NivelAcceso.Administrador,
        [Operacion.DesactivarUsuario] = NivelAcceso.Administrador,
        [Operacion.ReactivarUsuario] = NivelAcceso.Administrador,
        [Operacion.ForzarRestablecimiento] = NivelAcceso.Administrador,
    };

    /// <summary>
    /// Nivel que exige la operación. Si alguien agrega una operación y olvida declararla aquí,
    /// falla de forma explícita (y lo detecta la prueba de permisos).
    /// </summary>
    public static NivelAcceso NivelDe(Operacion operacion)
    {
        if (!PorOperacion.TryGetValue(operacion, out var nivel))
        {
            throw new InvalidOperationException($"La operación {operacion} no tiene un nivel de acceso declarado en Permisos.");
        }

        return nivel;
    }
}
