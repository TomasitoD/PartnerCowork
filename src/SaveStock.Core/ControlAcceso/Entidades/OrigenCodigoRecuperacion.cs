namespace SaveStock.Core.ControlAcceso.Entidades;

/// <summary>
/// Quién originó el código de recuperación: el propio usuario (RF-CA-09) o un administrador (RF-CA-13).
/// </summary>
public enum OrigenCodigoRecuperacion
{
    Usuario,
    Administrador,
}
