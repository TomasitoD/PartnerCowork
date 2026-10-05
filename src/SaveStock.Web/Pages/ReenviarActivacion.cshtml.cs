using Microsoft.AspNetCore.Mvc;
using SaveStock.Core.ControlAcceso.Autorizacion;
using SaveStock.Core.ControlAcceso.Registro;
using SaveStock.Web.Paginas;

namespace SaveStock.Web.Pages;

/// <summary>Reenvío del enlace de activación (RF-CA-17). El mensaje es el mismo exista o no el correo.</summary>
public class ReenviarActivacionModel : PaginaSaveStock
{
    private readonly ServicioRegistro _servicio;

    public ReenviarActivacionModel(ServicioRegistro servicio)
    {
        _servicio = servicio;
    }

    [BindProperty]
    public string? Correo { get; set; }

    public async Task<IActionResult> OnGetAsync() => await AutorizarAsync(Operacion.ReenviarActivacion) ?? Page();

    public async Task<IActionResult> OnPostAsync()
    {
        var rechazo = await AutorizarAsync(Operacion.ReenviarActivacion);
        if (rechazo is not null)
        {
            return rechazo;
        }

        var resultado = await _servicio.ReenviarActivacionAsync(new SolicitudReenvioActivacion(Correo));
        Mensaje = MensajePagina.De(resultado);
        return Page();
    }
}
