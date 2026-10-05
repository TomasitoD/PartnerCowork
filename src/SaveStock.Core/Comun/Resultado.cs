namespace SaveStock.Core.Comun;

/// <summary>
/// Resultado de una operación de negocio. Los servicios devuelven esto en lugar de
/// lanzar excepciones cuando la regla de negocio no se cumple.
/// </summary>
public class Resultado
{
    protected Resultado(bool exito, TipoError tipoError, string mensaje)
    {
        Exito = exito;
        TipoError = tipoError;
        Mensaje = mensaje;
    }

    public bool Exito { get; }

    public TipoError TipoError { get; }

    /// <summary>Mensaje para mostrar al usuario. Nunca contiene detalles internos (RD-08).</summary>
    public string Mensaje { get; }

    public static Resultado Ok(string mensaje = "") => new(true, TipoError.Ninguno, mensaje);

    public static Resultado Error(TipoError tipoError, string mensaje) => new(false, tipoError, mensaje);
}

/// <summary>
/// Resultado de una operación que, si sale bien, devuelve un valor.
/// </summary>
public class Resultado<T> : Resultado
{
    private Resultado(bool exito, TipoError tipoError, string mensaje, T? valor)
        : base(exito, tipoError, mensaje)
    {
        Valor = valor;
    }

    /// <summary>El valor devuelto. Solo tiene sentido cuando <see cref="Resultado.Exito"/> es true.</summary>
    public T? Valor { get; }

    public static Resultado<T> Ok(T valor, string mensaje = "") => new(true, TipoError.Ninguno, mensaje, valor);

    public static new Resultado<T> Error(TipoError tipoError, string mensaje) => new(false, tipoError, mensaje, default);
}
