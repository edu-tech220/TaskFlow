using System.ComponentModel.DataAnnotations;

namespace TaskFlow.DTOs;

public sealed record CreateRestDayRequest
{
    [Required]
    public DateOnly? Date { get; init; }

    [StringLength(200)]
    public string? Notes { get; init; }
}

public sealed record SetRestDayLimitRequest
{
    [Required, Range(0, 10)]
    public int? MaxRestDays { get; init; }
}

public sealed record RestDayResponse(int Id, DateOnly Date, string? Notes, DateTime CreatedAt);

// Resumen del mes: lo que el frontend necesita para mostrar "2 de 4 descansos".
public sealed record RestDayMonthResponse(
    int Year,
    int Month,
    int Limit,
    bool IsDefaultLimit,
    int Used,
    int Remaining,
    IReadOnlyList<RestDayResponse> Days);

public sealed record RestDayLimitResponse(
    int Year,
    int Month,
    int MaxRestDays,
    // true = no hay fila para el mes y se usa RestDays:DefaultMonthlyLimit.
    bool IsDefault,
    int Used);
