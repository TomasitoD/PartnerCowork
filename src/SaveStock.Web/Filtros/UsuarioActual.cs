using SaveStock.Core.ControlAcceso.Entidades;

namespace SaveStock.Web.Filtros;

/// <summary>
/// Da acceso, dentro de un endpoint, al usuario y la sesión que validó <see cref="FiltroOperacion"/>.
/// Solo tiene sentido en endpoints con una operación Autenticado o Administrador.
/// </summary>
public static class UsuarioActual
{
    private const string ClaveSesion = "SaveStock.Sesion";
    private const string ClaveToken = "SaveStock.Token";

    /// <summary>
    /// El usuario autenticado. Lo cargó el mismo CoreDbContext de la petición, así que se puede
    /// modificar y guardar con SaveChanges.
    /// </summary>
    public static Usuario ObtenerUsuarioActual(this HttpContext http) => http.ObtenerSesionActual().Usuario;

    public static Sesion ObtenerSesionActual(this HttpContext http) =>
        http.Items[ClaveSesion] as Sesion
        ?? throw new InvalidOperationException("No hay sesión validada: el endpoint debe declarar RequiereOperacion con una operación que exija sesión.");

    /// <summary>El token de la petición, por ejemplo para revocar la sesión al cerrarla.</summary>
    public static string ObtenerTokenActual(this HttpContext http) =>
        http.Items[ClaveToken] as string
        ?? throw new InvalidOperationException("No hay sesión validada: el endpoint debe declarar RequiereOperacion con una operación que exija sesión.");

    internal static void Guardar(HttpContext http, Sesion sesion, string token)
    {
        http.Items[ClaveSesion] = sesion;
        http.Items[ClaveToken] = token;
    }
}
