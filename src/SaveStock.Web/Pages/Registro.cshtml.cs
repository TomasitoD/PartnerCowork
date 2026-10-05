using Microsoft.AspNetCore.Mvc;
using SaveStock.Core.ControlAcceso.Autorizacion;
using SaveStock.Core.ControlAcceso.Registro;
using SaveStock.Web.Paginas;

namespace SaveStock.Web.Pages;

/// <summary>Registro de cuenta (RF-CA-01, RF-CA-14, RF-CA-15). Las reglas están en <see cref="ServicioRegistro"/>.</summary>
public class RegistroModel : PaginaSaveStock
{
    private readonly ServicioRegistro _servicio;

    public RegistroModel(ServicioRegistro servicio)
    {
        _servicio = servicio;
    }

    [BindProperty]
    public string? Nombre { get; set; }

    [BindProperty]
    public string? Correo { get; set; }

    [BindProperty]
    public string? Contrasena { get; set; }

    public async Task<IActionResult> OnGetAsync() => await AutorizarAsync(Operacion.Registrar) ?? Page();

    public async Task<IActionResult> OnPostAsync()
    {
        var rechazo = await AutorizarAsync(Operacion.Registrar);
        if (rechazo is not null)
        {
            return rechazo;
        }

        var resultado = await _servicio.RegistrarAsync(new SolicitudRegistro(Nombre, Correo, Contrasena));
        Mensaje = MensajePagina.De(resultado);
        Contrasena = null;
        return Page();
    }
}
