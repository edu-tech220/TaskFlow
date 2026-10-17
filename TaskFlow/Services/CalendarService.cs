using Microsoft.EntityFrameworkCore;
using TaskFlow.Common;
using TaskFlow.Data;
using TaskFlow.DTOs;
using TaskFlow.Enums;

namespace TaskFlow.Services;

public interface ICalendarService
{
    Task<Result<CalendarMonthResponse>> GetMonthAsync(int year, int month, CancellationToken ct);
    Task<CalendarDayResponse> GetDayAsync(DateOnly date, CancellationToken ct);
}

// Calendario sin N+1: el número de consultas es FIJO sin importar cuántos días o actividades
// haya (3 para el rango de días + 1 del límite). Todo lo demás se calcula en memoria.
// N+1 sería consultar las actividades y luego hacer una consulta por cada día o actividad.
public sealed class CalendarService(
    TaskFlowDbContext db,
    IAppClock clock,
    IRestDayService restDays,
    ICurrentUser currentUser) : ICalendarService
{
    public async Task<Result<CalendarMonthResponse>> GetMonthAsync(int year, int month, CancellationToken ct)
    {
        if (Months.Validate(year, month) is { } error)
            return error;

        var (first, last) = Months.Range(year, month);
        var today = clock.Today;

        var days = await BuildDaysAsync(first, last, today, ct);              // 3 consultas
        var (limit, _) = await restDays.GetEffectiveLimitAsync(year, month, ct); // 1 consulta

        return new CalendarMonthResponse(year, month, today, limit, days.Count(d => d.IsRestDay), days);
    }

    public async Task<CalendarDayResponse> GetDayAsync(DateOnly date, CancellationToken ct)
    {
        var days = await BuildDaysAsync(date, date, clock.Today, ct);
        return days[0];
    }

    private async Task<IReadOnlyList<CalendarDayResponse>> BuildDaysAsync(
        DateOnly from, DateOnly to, DateOnly today, CancellationToken ct)
    {
        var userId = currentUser.UserId;

        // Consulta 1: versiones de horario que se traslapan con el rango, con nombre y días.
        // Proyección a tipo anónimo: EF genera un solo SELECT con JOIN a Activities y
        // LEFT JOIN a ActivityScheduleDays, y arma la lista Days en memoria.
        var schedules = await db.ActivitySchedules
            .AsNoTracking()
            .Where(s => s.Activity.UserId == userId)
            .Where(s => s.EffectiveFrom <= to && (s.EffectiveTo == null || s.EffectiveTo >= from))
            .Select(s => new
            {
                s.ActivityId,
                ActivityName = s.Activity.Name,
                s.StartTime,
                s.EndTime,
                s.EffectiveFrom,
                s.EffectiveTo,
                Days = s.Days.Select(d => d.DayOfWeek).ToList()
            })
            .ToListAsync(ct);

        // Consulta 2: registros del rango, indexados por (actividad, fecha) para búsqueda O(1).
        var records = await db.DailyRecords
            .AsNoTracking()
            .Where(r => r.Activity.UserId == userId && r.Date >= from && r.Date <= to)
            .Select(r => new { r.ActivityId, r.Date, r.Status, r.ActualEndTime, r.Notes })
            .ToDictionaryAsync(r => (r.ActivityId, r.Date), ct);

        // Consulta 3: descansos del rango.
        var restDates = (await db.RestDays
            .AsNoTracking()
            .Where(r => r.UserId == userId && r.Date >= from && r.Date <= to)
            .Select(r => r.Date)
            .ToListAsync(ct))
            .ToHashSet();

        var result = new List<CalendarDayResponse>(to.DayNumber - from.DayNumber + 1);

        for (var date = from; date <= to; date = date.AddDays(1))
        {
            var activities = new List<CalendarActivityResponse>();
            int completed = 0, notCompleted = 0, missing = 0;

            // Versiones vigentes ese día que incluyen ese día de la semana.
            // Como las versiones de una actividad no se traslapan, hay como máximo una por actividad.
            foreach (var s in schedules)
            {
                if (!ScheduleRules.Covers(s.EffectiveFrom, s.EffectiveTo, date) || !s.Days.Contains(date.DayOfWeek))
                    continue;

                records.TryGetValue((s.ActivityId, date), out var record);
                var status = DayStatusCalculator.GetActivityStatus(date, today, record?.Status);

                switch (status)
                {
                    case ActivityDayStatus.Completed: completed++; break;
                    case ActivityDayStatus.NotCompleted: notCompleted++; break;
                    case ActivityDayStatus.Pending or ActivityDayStatus.Unrecorded: missing++; break;
                }

                activities.Add(new CalendarActivityResponse(
                    s.ActivityId, s.ActivityName, s.StartTime, s.EndTime, status,
                    record?.ActualEndTime, record?.Notes));
            }

            var isRestDay = restDates.Contains(date);
            var scheduled = activities.Count;
            var isPast = date < today;

            result.Add(new CalendarDayResponse(
                date,
                DayStatusCalculator.GetDayStatus(date, today, isRestDay, scheduled, completed, missing),
                isRestDay,
                scheduled,
                completed,
                notCompleted,
                PendingCount: date == today ? missing : 0,
                UnrecordedCount: isPast ? missing : 0,
                activities.OrderBy(a => a.StartTime).ThenBy(a => a.Name).ToList()));
        }

        return result;
    }
}
