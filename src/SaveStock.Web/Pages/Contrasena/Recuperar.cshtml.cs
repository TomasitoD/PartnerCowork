using Microsoft.AspNetCore.Mvc;
using SaveStock.Core.ControlAcceso.Autorizacion;
using SaveStock.Core.ControlAcceso.Contrasenas;
using SaveStock.Web.Paginas;

namespace SaveStock.Web.Pages.Contrasena;

/// <summary>Inicio de la recuperación (RF-CA-09). El mensaje es el mismo exista o no el correo.</summary>
public class RecuperarModel : PaginaSaveStock
{
    private readonly ServicioContrasenas _servicio;

    public RecuperarModel(ServicioContrasenas servicio)
    {
        _servicio = servicio;
    }

    [BindProperty]
    public string? Correo { get; set; }

    public async Task<IActionResult> OnGetAsync() => await AutorizarAsync(Operacion.IniciarRecuperacion) ?? Page();

    public async Task<IActionResult> OnPostAsync()
    {
        var rechazo = await AutorizarAsync(Operacion.IniciarRecuperacion);
        if (rechazo is not null)
        {
            return rechazo;
        }

        var resultado = await _servicio.IniciarRecuperacionAsync(new SolicitudRecuperacion(Correo));
        Mensaje = MensajePagina.De(resultado);
        return Page();
    }
}
