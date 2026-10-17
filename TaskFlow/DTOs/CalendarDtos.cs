using TaskFlow.Enums;

namespace TaskFlow.DTOs;

public sealed record CalendarActivityResponse(
    int ActivityId,
    string Name,
    TimeOnly StartTime,
    TimeOnly EndTime,
    ActivityDayStatus Status,
    // Datos del registro, si existe (evita otra llamada al abrir el detalle del día).
    TimeOnly? ActualEndTime,
    string? Notes);

public sealed record CalendarDayResponse(
    DateOnly Date,
    DayStatus Status,
    bool IsRestDay,
    int ScheduledCount,
    int CompletedCount,
    int NotCompletedCount,
    int PendingCount,
    int UnrecordedCount,
    IReadOnlyList<CalendarActivityResponse> Activities);

public sealed record CalendarMonthResponse(
    int Year,
    int Month,
    // "Hoy" según la zona configurada; el cliente no debe calcularlo con su propio reloj.
    DateOnly Today,
    int RestDayLimit,
    int RestDaysUsed,
    IReadOnlyList<CalendarDayResponse> Days);
