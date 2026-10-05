namespace SaveStock.Inventario.OrdenesDeCompra;

/// <summary>
/// EL único lugar donde se declaran los estados de una orden de compra (RF-NEG-03).
/// Qué cambios entre ellos están permitidos lo decide solo <see cref="MaquinaEstadosOrdenCompra"/> (RD-04).
/// Se guarda como texto en la base.
/// </summary>
public enum EstadoOrdenCompra
{
    /// <summary>Recién creada: todavía se puede preparar. Es el estado inicial.</summary>
    Borrador,

    /// <summary>Se mandó al proveedor y se espera la mercancía.</summary>
    Enviada,

    /// <summary>Llegó la mercancía y repuso el stock. Estado terminal (RF-NEG-05).</summary>
    Recibida,

    /// <summary>Se anuló antes de recibirse. Estado terminal (RF-NEG-05).</summary>
    Cancelada,
}
