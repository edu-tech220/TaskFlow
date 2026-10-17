using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Common;
using TaskFlow.Data;
using TaskFlow.DTOs;
using TaskFlow.Entities;
using TaskFlow.Enums;

namespace TaskFlow.Services;

public interface IDailyRecordService
{
    Task<IReadOnlyList<DailyRecordResponse>> GetByDateAsync(DateOnly date, CancellationToken ct);
    Task<Result<IReadOnlyList<DailyRecordResponse>>> GetRangeAsync(DateOnly from, DateOnly to, CancellationToken ct);
    Task<Result<DailyRecordResponse>> GetAsync(int activityId, DateOnly date, CancellationToken ct);
    Task<Result<UpsertDailyRecordResult>> UpsertAsync(int activityId, DateOnly date, UpsertDailyRecordRequest request, CancellationToken ct);
    Task<Result> DeleteAsync(int activityId, DateOnly date, CancellationToken ct);
}

// Reglas 12 a 16: registros diarios.
public sealed class DailyRecordService(TaskFlowDbContext db, IAppClock clock) : IDailyRecordService
{
    public const int MaxRangeDays = 366;

    public async Task<IReadOnlyList<DailyRecordResponse>> GetByDateAsync(DateOnly date, CancellationToken ct) =>
        await Query(r => r.Date == date).ToListAsync(ct);

    public async Task<Result<IReadOnlyList<DailyRecordResponse>>> GetRangeAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        if (to < from)
            return Error.Validation("InvalidRange", "'to' debe ser mayor o igual que 'from'.");

        // DayNumber = días desde 0001-01-01: la resta da la longitud del rango.
        if (to.DayNumber - from.DayNumber + 1 > MaxRangeDays)
            return Error.Validation("RangeTooLarge", $"El rango no puede exceder {MaxRangeDays} días.");

        var records = await Query(r => r.Date >= from && r.Date <= to).ToListAsync(ct);
        return records;
    }

    public async Task<Result<DailyRecordResponse>> GetAsync(int activityId, DateOnly date, CancellationToken ct)
    {
        var record = await Query(r => r.ActivityId == activityId && r.Date == date)
            .FirstOrDefaultAsync(ct);
        return record is null ? NotFound(activityId, date) : record;
    }

    // Regla 13: upsert por (ActivityId, Date).
    public async Task<Result<UpsertDailyRecordResult>> UpsertAsync(
        int activityId, DateOnly date, UpsertDailyRecordRequest request, CancellationToken ct)
    {
        // Regla 12: solo hoy o fechas pasadas.
        if (date > clock.Today)
            return Error.Validation("FutureDate", "No se pueden registrar fechas futuras.");

        var activity = await db.Activities
            .AsNoTracking()
            .Where(a => a.Id == activityId)
            .Select(a => new { a.Id, a.Name })
            .FirstOrDefaultAsync(ct);
        if (activity is null)
            return Error.NotFound("ActivityNotFound", $"No existe la actividad {activityId}.");

        // Regla 12: debe estar programada ese día según la versión vigente EN ESA FECHA.
        // Toda la condición se traduce a SQL (EXISTS), sin traer versiones a memoria.
        var dayOfWeek = date.DayOfWeek;
        var isScheduled = await db.ActivitySchedules.AnyAsync(s =>
            s.ActivityId == activityId &&
            s.EffectiveFrom <= date &&
            (s.EffectiveTo == null || s.EffectiveTo >= date) &&
            s.Days.Any(d => d.DayOfWeek == dayOfWeek), ct);
        if (!isScheduled)
            return Error.Conflict("ActivityNotScheduled",
                $"La actividad {activityId} no está programada el {date:yyyy-MM-dd} ({dayOfWeek}).");

        var record = await db.DailyRecords
            .FirstOrDefaultAsync(r => r.ActivityId == activityId && r.Date == date, ct);
        var created = record is null;

        if (record is null)
        {
            record = new DailyRecord { ActivityId = activityId, Date = date };
            db.DailyRecords.Add(record);
        }

        var status = request.Status!.Value;
        record.Status = status;
        // Regla 14: ActualEndTime solo con Completed; al pasar a NotCompleted se borra.
        record.ActualEndTime = status == DailyRecordStatus.Completed ? request.ActualEndTime : null;
        record.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

        // Si dos peticiones crean el mismo registro a la vez, el índice único lo impide (=> 409).
        await db.SaveChangesAsync(ct);

        return new UpsertDailyRecordResult(created, ToResponse(record, activity.Name));
    }

    // Regla 15: se puede eliminar para corregir (vuelve a Pending o Unrecorded).
    public async Task<Result> DeleteAsync(int activityId, DateOnly date, CancellationToken ct)
    {
        var deleted = await db.DailyRecords
            .Where(r => r.ActivityId == activityId && r.Date == date)
            .ExecuteDeleteAsync(ct);

        return deleted == 0 ? NotFound(activityId, date) : Result.Success();
    }

    // Consulta base con proyección: SELECT solo de las columnas del DTO + JOIN al nombre.
    // El filtro va ANTES del Select: EF no puede traducir un Where sobre propiedades de un
    // record creado con constructor. Expression<Func<...>> (no Func) para que EF lo lea como
    // árbol de expresión y lo convierta a SQL, en lugar de filtrar en memoria.
    // Con Select no hace falta AsNoTracking (los DTOs no se rastrean), pero se deja explícito.
    private IQueryable<DailyRecordResponse> Query(Expression<Func<DailyRecord, bool>> filter) =>
        db.DailyRecords
            .AsNoTracking()
            .Where(filter)
            .OrderBy(r => r.Date).ThenBy(r => r.ActivityId)
            .Select(r => new DailyRecordResponse(
                r.Id, r.ActivityId, r.Activity.Name, r.Date, r.Status,
                r.ActualEndTime, r.Notes, r.CreatedAt, r.UpdatedAt));

    private static DailyRecordResponse ToResponse(DailyRecord r, string activityName) =>
        new(r.Id, r.ActivityId, activityName, r.Date, r.Status, r.ActualEndTime, r.Notes, r.CreatedAt, r.UpdatedAt);

    private static Error NotFound(int activityId, DateOnly date) =>
        Error.NotFound("DailyRecordNotFound",
            $"No hay registro de la actividad {activityId} el {date:yyyy-MM-dd}.");
}
