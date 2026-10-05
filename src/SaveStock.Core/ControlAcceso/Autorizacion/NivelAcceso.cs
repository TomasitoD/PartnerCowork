namespace SaveStock.Core.ControlAcceso.Autorizacion;

/// <summary>
/// Quién puede ejecutar una operación.
/// </summary>
public enum NivelAcceso
{
    /// <summary>Cualquiera, sin sesión.</summary>
    Publico,

    /// <summary>Cualquier usuario con una sesión válida (Estándar o Administrador).</summary>
    Autenticado,

    /// <summary>Solo un Administrador con una sesión válida.</summary>
    Administrador,
}
