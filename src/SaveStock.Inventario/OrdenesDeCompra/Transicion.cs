using SaveStock.Core.ControlAcceso.Entidades;

namespace SaveStock.Inventario.OrdenesDeCompra;

/// <summary>
/// Una fila de la tabla de transiciones permitidas de <see cref="MaquinaEstadosOrdenCompra"/>.
/// </summary>
/// <param name="Desde">Estado en el que tiene que estar la orden.</param>
/// <param name="Hacia">Estado al que pasa.</param>
/// <param name="QuienEjecuta">Roles que pueden hacer este cambio.</param>
/// <param name="Condicion">Qué tiene que pasar en el negocio para hacerlo (se muestra en la documentación).</param>
/// <param name="CumpleCondicion">
/// Revisión de la condición sobre la orden, cuando se puede comprobar con sus datos.
/// null: la condición la confirma quien ejecuta el cambio (por ejemplo, "llegó la mercancía").
/// </param>
/// <param name="MensajeSiNoCumple">Mensaje para el usuario si la condición no se cumple.</param>
public record Transicion(
    EstadoOrdenCompra Desde,
    EstadoOrdenCompra Hacia,
    IReadOnlyList<Rol> QuienEjecuta,
    string Condicion,
    Func<OrdenDeCompra, bool>? CumpleCondicion = null,
    string MensajeSiNoCumple = "");

/// <summary>
/// Un cambio de estado que está prohibido a propósito (RF-NEG-04), con el motivo del negocio.
/// </summary>
public record TransicionProhibida(EstadoOrdenCompra Desde, EstadoOrdenCompra Hacia, string Motivo);
