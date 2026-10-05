using Microsoft.AspNetCore.Mvc;
using SaveStock.Core.Comun;
using SaveStock.Core.ControlAcceso.Administracion;
using SaveStock.Core.ControlAcceso.Autorizacion;
using SaveStock.Core.ControlAcceso.Contrasenas;
using SaveStock.Web.Paginas;

namespace SaveStock.Web.Pages.Admin;

/// <summary>
/// Administración de usuarios (RF-CA-08, RF-CA-13, RF-CA-20, RF-CA-21). Cada handler autoriza SU
/// operación con el mismo Autorizador que la API: un Estándar recibe 403 aunque arme el POST a mano (RD-06).
/// </summary>
public class UsuariosModel : PaginaSaveStock
{
    private readonly ServicioAdministracionUsuarios _administracion;
    private readonly ServicioContrasenas _contrasenas;

    public UsuariosModel(ServicioAdministracionUsuarios administracion, ServicioContrasenas contrasenas)
    {
        _administracion = administracion;
        _contrasenas = contrasenas;
    }

    /// <summary>Lo que muestra la tabla. Es el DTO del servicio: no trae hashes ni tokens.</summary>
    public IReadOnlyList<UsuarioResumen> Usuarios { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        var rechazo = await AutorizarAsync(Operacion.ListarUsuarios);
        if (rechazo is not null)
        {
            return rechazo;
        }

        Usuarios = await _administracion.ListarAsync();
        return Page();
    }

    public Task<IActionResult> OnPostCambiarRolAsync(int id, string? rol) =>
        EjecutarAsync(Operacion.CambiarRol, () => _administracion.CambiarRolAsync(UsuarioActual!.Id, id, rol));

    public Task<IActionResult> OnPostDesactivarAsync(int id) =>
        EjecutarAsync(Operacion.DesactivarUsuario, () => _administracion.DesactivarAsync(UsuarioActual!.Id, id));

    public Task<IActionResult> OnPostReactivarAsync(int id) =>
        EjecutarAsync(Operacion.ReactivarUsuario, () => _administracion.ReactivarAsync(id));

    public Task<IActionResult> OnPostForzarRestablecimientoAsync(int id) =>
        EjecutarAsync(Operacion.ForzarRestablecimiento, () => _contrasenas.ForzarRestablecimientoAsync(id));

    /// <summary>
    /// Autoriza la operación del botón y, solo si pasa, llama al servicio y muestra su mensaje
    /// junto con la tabla actualizada.
    /// </summary>
    private async Task<IActionResult> EjecutarAsync(Operacion operacion, Func<Task<Resultado>> accion)
    {
        var rechazo = await AutorizarAsync(operacion);
        if (rechazo is not null)
        {
            return rechazo;
        }

        Mensaje = MensajePagina.De(await accion());

        // La tabla también es una operación (ListarUsuarios): se vuelve a autorizar antes de mostrarla.
        rechazo = await AutorizarAsync(Operacion.ListarUsuarios);
        if (rechazo is not null)
        {
            return rechazo;
        }

        Usuarios = await _administracion.ListarAsync();
        return Page();
    }
}
