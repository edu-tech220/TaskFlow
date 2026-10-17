using Microsoft.EntityFrameworkCore;
using TaskFlow.Common;
using TaskFlow.Data;
using TaskFlow.DTOs;
using TaskFlow.Entities;

namespace TaskFlow.Services;

public interface IActivityService
{
    Task<IReadOnlyList<ActivityResponse>> GetAllAsync(CancellationToken ct);
    Task<Result<ActivityResponse>> GetByIdAsync(int id, CancellationToken ct);
    Task<Result<ActivityResponse>> CreateAsync(CreateActivityRequest request, CancellationToken ct);
    Task<Result<ActivityResponse>> UpdateAsync(int id, UpdateActivityRequest request, CancellationToken ct);
    Task<Result<ActivityResponse>> SetStatusAsync(int id, bool isActive, CancellationToken ct);
    Task<Result> DeleteAsync(int id, CancellationToken ct);
}

// Reglas de negocio de actividades y horarios versionados (reglas 1 a 7).
// Usa el DbContext directamente: DbSet ya es el repositorio y SaveChanges la unidad de trabajo.
public sealed class ActivityService(TaskFlowDbContext db, IAppClock clock) : IActivityService
{
    public async Task<IReadOnlyList<ActivityResponse>> GetAllAsync(CancellationToken ct)
    {
        // AsNoTracking: solo lectura, EF no guarda copias para detectar cambios (más rápido).
        // Include + ThenInclude: carga versiones y sus días en la misma consulta (JOIN).
        var activities = await db.Activities
            .AsNoTracking()
            .Include(a => a.Schedules).ThenInclude(s => s.Days)
            .OrderBy(a => a.Name)
            .ToListAsync(ct);

        var today = clock.Today;
        return activities.Select(a => ToResponse(a, today)).ToList();
    }

    public async Task<Result<ActivityResponse>> GetByIdAsync(int id, CancellationToken ct)
    {
        var activity = await db.Activities
            .AsNoTracking()
            .Include(a => a.Schedules).ThenInclude(s => s.Days)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        return activity is null ? NotFound(id) : ToResponse(activity, clock.Today);
    }

    // Regla 1: crear la actividad crea su primera versión (la fecha de inicio puede ser pasada).
    public async Task<Result<ActivityResponse>> CreateAsync(CreateActivityRequest request, CancellationToken ct)
    {
        var today = clock.Today;
        var schedule = request.Schedule!;

        var activity = new Activity
        {
            Name = request.Name.Trim(),
            Description = Normalize(request.Description),
            Schedules =
            {
                NewVersion(schedule.StartTime!.Value, schedule.EndTime!.Value, schedule.Days!,
                    schedule.EffectiveFrom ?? today)
            }
        };

        // Add marca el grafo completo (actividad + versión + días) como Added; SaveChanges
        // inserta todo en una transacción y EF propaga los Id generados a las FKs.
        db.Activities.Add(activity);
        await db.SaveChangesAsync(ct);

        return ToResponse(activity, today);
    }

    // Regla 2: nombre/descripción se editan sin versión. Reglas 3 y 4: el horario se versiona.
    public async Task<Result<ActivityResponse>> UpdateAsync(int id, UpdateActivityRequest request, CancellationToken ct)
    {
        // Sin AsNoTracking: vamos a modificar, EF necesita rastrear los cambios.
        var activity = await LoadTrackedAsync(id, ct);
        if (activity is null)
            return NotFound(id);

        activity.Name = request.Name.Trim();
        activity.Description = Normalize(request.Description);

        if (request.Schedule is not null)
        {
            var error = await ApplyScheduleChangeAsync(activity, request.Schedule, ct);
            if (error is not null)
                return error;
        }

        // EF compara con los valores originales y solo genera UPDATE de lo que cambió.
        await db.SaveChangesAsync(ct);
        return ToResponse(activity, clock.Today);
    }

    // Regla 5: desactivar cierra la vigente hoy; reactivar crea versión desde mañana.
    public async Task<Result<ActivityResponse>> SetStatusAsync(int id, bool isActive, CancellationToken ct)
    {
        var activity = await LoadTrackedAsync(id, ct);
        if (activity is null)
            return NotFound(id);

        var today = clock.Today;
        var currentlyActive = ScheduleRules.IsActive(activity.Schedules, today);

        // Idempotente: pedir el estado que ya tiene no es error.
        if (isActive == currentlyActive)
            return ToResponse(activity, today);

        if (isActive)
        {
            var error = Reactivate(activity, today);
            if (error is not null)
                return error;
        }
        else
        {
            Deactivate(activity, today);
        }

        await db.SaveChangesAsync(ct);
        return ToResponse(activity, today);
    }

    // Regla 6: solo se elimina si no tiene registros diarios.
    public async Task<Result> DeleteAsync(int id, CancellationToken ct)
    {
        if (!await db.Activities.AnyAsync(a => a.Id == id, ct))
            return NotFound(id);

        if (await db.DailyRecords.AnyAsync(r => r.ActivityId == id, ct))
            return Error.Conflict("ActivityHasRecords",
                "La actividad tiene registros diarios y no se puede eliminar. Desactívala para conservar su historial.");

        // ExecuteDeleteAsync (EF 7+): DELETE directo en SQL, sin cargar la entidad.
        // Las versiones y sus días se van por el ON DELETE CASCADE de la BD.
        await db.Activities.Where(a => a.Id == id).ExecuteDeleteAsync(ct);
        return Result.Success();
    }

    // ---------- Versionado ----------

    private async Task<Error?> ApplyScheduleChangeAsync(Activity activity, ScheduleRequest request, CancellationToken ct)
    {
        var today = clock.Today;
        var tomorrow = today.AddDays(1);
        var start = request.StartTime!.Value;
        var end = request.EndTime!.Value;
        var days = request.Days!;

        // Versiones sin traslape => la de mayor EffectiveFrom es la última.
        var last = activity.Schedules.MaxBy(s => s.EffectiveFrom);
        var isActive = ScheduleRules.IsActive(activity.Schedules, today);

        // Actividad desactivada (o sin versiones): el nuevo horario la reactiva desde mañana
        // o desde la fecha indicada. La última versión terminó hoy o antes: no hay traslape.
        if (!isActive)
        {
            var from = request.EffectiveFrom ?? tomorrow;
            if (from < tomorrow)
                return FutureOnlyError();

            activity.Schedules.Add(NewVersion(start, end, days, from));
            return null;
        }

        // Regla 4: la última versión empieza hoy o después y no tiene registros => se edita
        // en su lugar (no tiene historia que proteger). Solo si no piden otra fecha de inicio.
        var sameStart = request.EffectiveFrom is null || request.EffectiveFrom == last!.EffectiveFrom;
        if (last!.EffectiveFrom >= today && sameStart &&
            !await db.DailyRecords.AnyAsync(r => r.ActivityId == activity.Id && r.Date >= last.EffectiveFrom, ct))
        {
            last.StartTime = start;
            last.EndTime = end;
            ReplaceDays(last, days);
            return null;
        }

        // Regla 3: nueva versión desde mañana (o fecha futura) y la anterior se cierra el día previo.
        var effectiveFrom = request.EffectiveFrom ?? tomorrow;
        if (effectiveFrom < tomorrow)
            return FutureOnlyError();

        if (effectiveFrom <= last.EffectiveFrom)
            return Error.Conflict("ScheduleVersionConflict",
                $"Ya existe una versión de horario que inicia el {last.EffectiveFrom:yyyy-MM-dd}. " +
                "El nuevo horario debe iniciar después de esa fecha.");

        last.EffectiveTo = effectiveFrom.AddDays(-1);
        activity.Schedules.Add(NewVersion(start, end, days, effectiveFrom));
        return null;
    }

    private static void Deactivate(Activity activity, DateOnly today)
    {
        // Las versiones futuras no pueden tener registros: se eliminan.
        foreach (var future in activity.Schedules.Where(s => s.EffectiveFrom > today).ToList())
            activity.Schedules.Remove(future); // relación requerida => EF la borra (orphan delete)

        // La vigente hoy se cierra hoy: hoy sigue programada (hoy y el pasado no cambian).
        var current = activity.Schedules.FirstOrDefault(s => ScheduleRules.Covers(s, today));
        if (current is not null)
            current.EffectiveTo = today;
    }

    private static Error? Reactivate(Activity activity, DateOnly today)
    {
        var last = activity.Schedules.MaxBy(s => s.EffectiveFrom);
        if (last is null)
            return Error.Conflict("NoScheduleToRestore",
                "La actividad no tiene un horario previo que copiar. Envía un horario nuevo con PUT.");

        // Si se desactivó hoy mismo, basta con reabrir la versión (mismo horario, sin huecos).
        if (last.EffectiveTo == today)
        {
            last.EffectiveTo = null;
            return null;
        }

        activity.Schedules.Add(NewVersion(last.StartTime, last.EndTime,
            last.Days.Select(d => d.DayOfWeek).ToList(), today.AddDays(1)));
        return null;
    }

    // Ajusta los días con diferencias (quitar/agregar) en vez de Clear()+Add(): borrar y
    // volver a insertar la misma PK compuesta en el mismo contexto hace que EF lance excepción.
    private static void ReplaceDays(ActivitySchedule schedule, IReadOnlyList<DayOfWeek> days)
    {
        var wanted = days.ToHashSet();

        foreach (var day in schedule.Days.Where(d => !wanted.Contains(d.DayOfWeek)).ToList())
            schedule.Days.Remove(day);

        var existing = schedule.Days.Select(d => d.DayOfWeek).ToHashSet();
        foreach (var day in wanted.Where(d => !existing.Contains(d)))
            schedule.Days.Add(new ActivityScheduleDay { DayOfWeek = day });
    }

    // ---------- Utilidades ----------

    private Task<Activity?> LoadTrackedAsync(int id, CancellationToken ct) =>
        db.Activities
            .Include(a => a.Schedules).ThenInclude(s => s.Days)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    private static ActivitySchedule NewVersion(TimeOnly start, TimeOnly end, IEnumerable<DayOfWeek> days, DateOnly from) =>
        new()
        {
            StartTime = start,
            EndTime = end,
            EffectiveFrom = from,
            Days = days.Distinct().Select(d => new ActivityScheduleDay { DayOfWeek = d }).ToList()
        };

    private static string? Normalize(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static Error NotFound(int id) =>
        Error.NotFound("ActivityNotFound", $"No existe la actividad {id}.");

    private static Error FutureOnlyError() =>
        Error.Validation("ScheduleMustStartInFuture",
            "Un cambio de horario solo puede aplicar a partir de mañana; hoy y el pasado no cambian.");

    internal static ActivityResponse ToResponse(Activity activity, DateOnly today)
    {
        var versions = activity.Schedules
            .OrderByDescending(s => s.EffectiveFrom)
            .Select(ToResponse)
            .ToList();

        // Vigente hoy o, si no hay, la próxima futura.
        var current = versions.FirstOrDefault(v => ScheduleRules.Covers(v.EffectiveFrom, v.EffectiveTo, today))
            ?? versions.Where(v => v.EffectiveFrom > today).MinBy(v => v.EffectiveFrom);

        var isActive = ScheduleRules.IsActive(activity.Schedules, today);

        return new ActivityResponse(
            activity.Id,
            activity.Name,
            activity.Description,
            isActive,
            isActive ? current : null,
            versions,
            activity.CreatedAt,
            activity.UpdatedAt);
    }

    private static ScheduleVersionResponse ToResponse(ActivitySchedule s) =>
        new(s.Id, s.StartTime, s.EndTime,
            s.Days.Select(d => d.DayOfWeek).Order().ToList(),
            s.EffectiveFrom, s.EffectiveTo);
}
