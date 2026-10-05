namespace SaveStock.Core.Comun;

/// <summary>
/// Única fuente de la hora actual en todo el sistema (RD-11). Siempre en UTC.
/// En las pruebas se reemplaza por un reloj falso para controlar el tiempo.
/// </summary>
public interface IReloj
{
    DateTime AhoraUtc { get; }
}
