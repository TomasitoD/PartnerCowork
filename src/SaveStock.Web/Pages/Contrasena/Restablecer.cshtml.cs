using Microsoft.AspNetCore.Mvc;
using SaveStock.Core.ControlAcceso.Autorizacion;
using SaveStock.Core.ControlAcceso.Contrasenas;
using SaveStock.Web.Paginas;

namespace SaveStock.Web.Pages.Contrasena;

/// <summary>Contraseña nueva con el código de recuperación (RF-CA-10, RF-CA-11, RF-CA-12).</summary>
public class RestablecerModel : PaginaSaveStock
{
    private readonly ServicioContrasenas _servicio;

    public RestablecerModel(ServicioContrasenas servicio)
    {
        _servicio = servicio;
    }

    [BindProperty]
    public string? Correo { get; set; }

    [BindProperty]
    public string? Codigo { get; set; }

    [BindProperty]
    public string? ContrasenaNueva { get; set; }

    public async Task<IActionResult> OnGetAsync() => await AutorizarAsync(Operacion.RestablecerContrasena) ?? Page();

    public async Task<IActionResult> OnPostAsync()
    {
        var rechazo = await AutorizarAsync(Operacion.RestablecerContrasena);
        if (rechazo is not null)
        {
            return rechazo;
        }

        var resultado = await _servicio.RestablecerAsync(new SolicitudRestablecimiento(Correo, Codigo, ContrasenaNueva));
        Mensaje = MensajePagina.De(resultado);
        ContrasenaNueva = null;
        return Page();
    }
}
