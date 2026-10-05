namespace SaveStock.Core.Comun;

/// <summary>
/// Reloj real: devuelve la hora del sistema en UTC (RD-11).
/// </summary>
public class RelojSistema : IReloj
{
    public DateTime AhoraUtc => DateTime.UtcNow;
}
