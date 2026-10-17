namespace TaskFlow.Common;

// Validación y rango de fechas de un mes, compartido por descansos, límites y calendario.
public static class Months
{
    public const int MinYear = 2000;
    public const int MaxYear = 2100;

    public static Error? Validate(int year, int month)
    {
        if (year is < MinYear or > MaxYear)
            return Error.Validation("InvalidYear", $"El año debe estar entre {MinYear} y {MaxYear}.");

        if (month is < 1 or > 12)
            return Error.Validation("InvalidMonth", "El mes debe estar entre 1 y 12.");

        return null;
    }

    // Primer y último día del mes (rango cerrado).
    public static (DateOnly First, DateOnly Last) Range(int year, int month)
    {
        var first = new DateOnly(year, month, 1);
        return (first, first.AddMonths(1).AddDays(-1));
    }
}
