using Microsoft.AspNetCore.Mvc;
using SaveStock.Core.ControlAcceso.Autorizacion;
using SaveStock.Core.ControlAcceso.Contrasenas;
using SaveStock.Core.ControlAcceso.InicioSesion;
using SaveStock.Web.Paginas;

namespace SaveStock.Web.Pages;

/// <summary>
/// Datos del usuario actual (RF-CA-07), cierre de sesión (RF-CA-18) y cambio de contraseña (RF-CA-22).
/// Cada handler autoriza su propia operación.
/// </summary>
public class PerfilModel : PaginaSaveStock
{
    private readonly ServicioInicioSesion _sesion;
    private readonly ServicioContrasenas _contrasenas;

    public PerfilModel(ServicioInicioSesion sesion, ServicioContrasenas contrasenas)
    {
        _sesion = sesion;
        _contrasenas = contrasenas;
    }

    [BindProperty]
    public string? ContrasenaActual { get; set; }

    [BindProperty]
    public string? ContrasenaNueva { get; set; }

    /// <summary>true después de cambiar la contraseña: la sesión ya se revocó y hay que entrar de nuevo.</summary>
    public bool SesionTerminada { get; private set; }

    public async Task<IActionResult> OnGetAsync() => await AutorizarAsync(Operacion.ConsultarUsuarioActual) ?? Page();

    public async Task<IActionResult> OnPostCerrarSesionAsync()
    {
        var rechazo = await AutorizarAsync(Operacion.CerrarSesion);
        if (rechazo is not null)
        {
            return rechazo;
        }

        await _sesion.CerrarAsync(TokenActual!);
        BorrarCookieSesion();
        return Redirect("/");
    }

    public async Task<IActionResult> OnPostCambiarContrasenaAsync()
    {
        var rechazo = await AutorizarAsync(Operacion.CambiarContrasena);
        if (rechazo is not null)
        {
            return rechazo;
        }

        var resultado = await _contrasenas.CambiarAsync(
            UsuarioActual!.Id, new SolicitudCambioContrasena(ContrasenaActual, ContrasenaNueva));
        Mensaje = MensajePagina.De(resultado);
        ContrasenaActual = null;
        ContrasenaNueva = null;

        if (resultado.Exito)
        {
            // El servicio revocó todas las sesiones, incluida esta (RF-CA-12): la cookie ya no sirve.
            BorrarCookieSesion();
            SesionTerminada = true;
        }

        return Page();
    }
}
