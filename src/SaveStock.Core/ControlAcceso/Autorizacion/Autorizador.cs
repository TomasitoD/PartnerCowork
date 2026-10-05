using SaveStock.Core.Comun;
using SaveStock.Core.ControlAcceso.Entidades;

namespace SaveStock.Core.ControlAcceso.Autorizacion;

/// <summary>
/// Decide si un usuario puede ejecutar una operación, según lo declarado en <see cref="Permisos"/>
/// (RF-CA-05, RF-CA-06). No sabe nada de HTTP: la Web solo traduce el resultado.
/// </summary>
public static class Autorizador
{
    public const string MensajeSinSesion = "Necesitas iniciar sesión.";
    public const string MensajeSinPermiso = "No tienes permiso para realizar esta operación.";

    /// <param name="operacion">La operación que se quiere ejecutar.</param>
    /// <param name="usuario">El usuario con sesión válida, o null si no hay sesión.</param>
    public static Resultado Autorizar(Operacion operacion, Usuario? usuario)
    {
        var nivel = Permisos.NivelDe(operacion);

        if (nivel == NivelAcceso.Publico)
        {
            return Resultado.Ok();
        }

        if (usuario is null)
        {
            return Resultado.Error(TipoError.NoAutenticado, MensajeSinSesion);
        }

        if (nivel == NivelAcceso.Administrador && usuario.Rol != Rol.Administrador)
        {
            return Resultado.Error(TipoError.Prohibido, MensajeSinPermiso);
        }

        return Resultado.Ok();
    }
}
