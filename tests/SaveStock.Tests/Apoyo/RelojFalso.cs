using SaveStock.Core.Comun;

namespace SaveStock.Tests.Apoyo;

/// <summary>
/// Reloj controlado por la prueba: permite simular el paso del tiempo (vencimientos, bloqueos).
/// </summary>
public class RelojFalso : IReloj
{
    public DateTime AhoraUtc { get; set; } = new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);

    public void Avanzar(TimeSpan tiempo) => AhoraUtc += tiempo;
}
