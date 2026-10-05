using Microsoft.AspNetCore.Mvc;
using SaveStock.Core.ControlAcceso.Autorizacion;
using SaveStock.Core.ControlAcceso.InicioSesion;
using SaveStock.Web.Paginas;

namespace SaveStock.Web.Pages;

/// <summary>
/// Inicio de sesión (RF-CA-03, RF-CA-19). Credenciales, bloqueo y estado de la cuenta los decide
/// <see cref="ServicioInicioSesion"/>; la página solo guarda el token en la cookie.
/// </summary>
public class IniciarSesionModel : PaginaSaveStock
{
    private readonly ServicioInicioSesion _servicio;

    public IniciarSesionModel(ServicioInicioSesion servicio)
    {
        _servicio = servicio;
    }

    [BindProperty]
    public string? Correo { get; set; }

    [BindProperty]
    public string? Contrasena { get; set; }

    public async Task<IActionResult> OnGetAsync() => await AutorizarAsync(Operacion.IniciarSesion) ?? Page();

    public async Task<IActionResult> OnPostAsync()
    {
        var rechazo = await AutorizarAsync(Operacion.IniciarSesion);
        if (rechazo is not null)
        {
            return rechazo;
        }

        var resultado = await _servicio.IniciarAsync(Correo, Contrasena);
        if (!resultado.Exito)
        {
            Mensaje = MensajePagina.De(resultado);
            Contrasena = null;
            return Page();
        }

        GuardarCookieSesion(resultado.Valor!);
        return Redirect("/perfil");
    }
}
