namespace TaskFlow.Web.Models;

// Copia del contrato JSON de la API (solo lo que el frontend usa).
// El frontend es independiente: no referencia el proyecto de la API, solo habla HTTP.

public enum DailyRecordStatus { Completed = 1, NotCompleted = 2 }

public enum ActivityDayStatus { Scheduled, Pending, Completed, NotCompleted, Unrecorded }

public enum DayStatus { Future, RestDay, NoActivities, Completed, InProgress, Failed, Partial }

public sealed record ScheduleVersionDto(
    int Id, TimeOnly StartTime, TimeOnly EndTime, IReadOnlyList<DayOfWeek> Days,
    DateOnly EffectiveFrom, DateOnly? EffectiveTo);

public sealed record ActivityDto(
    int Id, string Name, string? Description, bool IsActive,
    ScheduleVersionDto? CurrentSchedule, IReadOnlyList<ScheduleVersionDto> Schedules,
    DateTime CreatedAt, DateTime? UpdatedAt);

public sealed record CalendarActivityDto(
    int ActivityId, string Name, TimeOnly StartTime, TimeOnly EndTime,
    ActivityDayStatus Status, TimeOnly? ActualEndTime, string? Notes);

public sealed record CalendarDayDto(
    DateOnly Date, DayStatus Status, bool IsRestDay,
    int ScheduledCount, int CompletedCount, int NotCompletedCount, int PendingCount, int UnrecordedCount,
    IReadOnlyList<CalendarActivityDto> Activities);

public sealed record CalendarMonthDto(
    int Year, int Month, DateOnly Today, int RestDayLimit, int RestDaysUsed,
    IReadOnlyList<CalendarDayDto> Days);

public sealed record DailyRecordDto(
    int Id, int ActivityId, string ActivityName, DateOnly Date, DailyRecordStatus Status,
    TimeOnly? ActualEndTime, string? Notes);

public sealed record RestDayLimitDto(int Year, int Month, int MaxRestDays, bool IsDefault, int Used);

// Resultado de una llamada: éxito con valor, o mensaje de error ya listo para mostrar.
public sealed record ApiResult<T>(T? Value, string? Error, IReadOnlyDictionary<string, string[]>? FieldErrors = null)
{
    public bool IsSuccess => Error is null;
}
