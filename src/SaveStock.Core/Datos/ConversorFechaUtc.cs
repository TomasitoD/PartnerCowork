using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace SaveStock.Core.Datos;

/// <summary>
/// Al leer una fecha de SQLite la marca como UTC, porque todas se guardan en UTC (RD-11).
/// </summary>
public class ConversorFechaUtc : ValueConverter<DateTime, DateTime>
{
    public ConversorFechaUtc()
        : base(
            fecha => fecha,
            fecha => DateTime.SpecifyKind(fecha, DateTimeKind.Utc))
    {
    }
}
