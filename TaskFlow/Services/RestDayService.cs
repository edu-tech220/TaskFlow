using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TaskFlow.Common;
using TaskFlow.Data;
using TaskFlow.DTOs;
using TaskFlow.Entities;

namespace TaskFlow.Services;

public interface IRestDayService
{
    Task<Result<RestDayMonthResponse>> GetMonthAsync(int year, int month, CancellationToken ct);
    Task<Result<RestDayResponse>> GetByDateAsync(DateOnly date, CancellationToken ct);
    Task<Result<RestDayResponse>> CreateAsync(CreateRestDayRequest request, CancellationToken ct);
    Task<Result> DeleteAsync(DateOnly date, CancellationToken ct);
    Task<Result<RestDayLimitResponse>> GetLimitAsync(int year, int month, CancellationToken ct);
    Task<Result<RestDayLimitResponse>> SetLimitAsync(int year, int month, int maxRestDays, CancellationToken ct);

    // Límite vigente del mes: el personalizado o el de appsettings. Lo usa también el calendario.
    Task<(int Limit, bool IsDefault)> GetEffectiveLimitAsync(int year, int month, CancellationToken ct);
}

// Reglas 8 a 11: descansos y límite mensual.
// IOptions<RestDayOptions>: el valor por defecto viene de appsettings (RestDays:DefaultMonthlyLimit).
public sealed class RestDayService(
    TaskFlowDbContext db,
    IAppClock clock,
    IOptions<RestDayOptions> options) : IRestDayService
{
    public async Task<Result<RestDayMonthResponse>> GetMonthAsync(int year, int month, CancellationToken ct)
    {
        if (Months.Validate(year, month) is { } error)
            return error;

        var (first, last) = Months.Range(year, month);

        var days = await db.RestDays
            .AsNoTracking()
            .Where(r => r.Date >= first && r.Date <= last)
            .OrderBy(r => r.Date)
            .Select(r => new RestDayResponse(r.Id, r.Date, r.Notes, r.CreatedAt)) // proyección: solo columnas necesarias
            .ToListAsync(ct);

        var (limit, isDefault) = await GetEffectiveLimitAsync(year, month, ct);

        return new RestDayMonthResponse(year, month, limit, isDefault, days.Count,
            Math.Max(0, limit - days.Count), days);
    }

    public async Task<Result<RestDayResponse>> GetByDateAsync(DateOnly date, CancellationToken ct)
    {
        var restDay = await db.RestDays
            .AsNoTracking()
            .Where(r => r.Date == date)
            .Select(r => new RestDayResponse(r.Id, r.Date, r.Notes, r.CreatedAt))
            .FirstOrDefaultAsync(ct);

        return restDay is null ? NotFound(date) : restDay;
    }

    public async Task<Result<RestDayResponse>> CreateAsync(CreateRestDayRequest request, CancellationToken ct)
    {
        var date = request.Date!.Value;

        // Regla 8: solo hoy o fechas futuras.
        if (date < clock.Today)
            return Error.Validation("RestDayInPast", "Solo se pueden marcar descansos de hoy en adelante.");

        if (Months.Validate(date.Year, date.Month) is { } monthError)
            return monthError;

        // Regla 8: fecha no repetida.
        if (await db.RestDays.AnyAsync(r => r.Date == date, ct))
            return Error.Conflict("RestDayAlreadyExists", $"El {date:yyyy-MM-dd} ya está marcado como descanso.");

        // Regla 9: no superar el límite del mes.
        var used = await CountInMonthAsync(date.Year, date.Month, ct);
        var (limit, _) = await GetEffectiveLimitAsync(date.Year, date.Month, ct);
        if (used >= limit)
            return Error.Conflict("MonthlyRestLimitReached",
                $"Ya marcaste {used} de {limit} descansos permitidos en {date:yyyy-MM}.");

        var restDay = new RestDay
        {
            Date = date,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim()
        };
        db.RestDays.Add(restDay);
        await db.SaveChangesAsync(ct); // el índice único en Date protege contra dos POST simultáneos

        return new RestDayResponse(restDay.Id, restDay.Date, restDay.Notes, restDay.CreatedAt);
    }

    public async Task<Result> DeleteAsync(DateOnly date, CancellationToken ct)
    {
        var restDay = await db.RestDays.FirstOrDefaultAsync(r => r.Date == date, ct);
        if (restDay is null)
            return NotFound(date);

        // Regla 11: los descansos pasados son historia.
        if (date < clock.Today)
            return Error.Conflict("RestDayInPast", "No se pueden quitar descansos de días pasados.");

        db.RestDays.Remove(restDay);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<RestDayLimitResponse>> GetLimitAsync(int year, int month, CancellationToken ct)
    {
        if (Months.Validate(year, month) is { } error)
            return error;

        var (limit, isDefault) = await GetEffectiveLimitAsync(year, month, ct);
        var used = await CountInMonthAsync(year, month, ct);
        return new RestDayLimitResponse(year, month, limit, isDefault, used);
    }

    public async Task<Result<RestDayLimitResponse>> SetLimitAsync(int year, int month, int maxRestDays, CancellationToken ct)
    {
        if (Months.Validate(year, month) is { } error)
            return error;

        // Regla 10: no bajar el límite por debajo de lo ya marcado. Nunca se borran descansos solos.
        var used = await CountInMonthAsync(year, month, ct);
        if (maxRestDays < used)
            return Error.Conflict("LimitBelowUsed",
                $"Ya hay {used} descansos marcados en {year}-{month:00}; el límite no puede ser menor. " +
                "Quita descansos primero.");

        // Upsert: si existe la fila del mes se actualiza; si no, se inserta.
        var entity = await db.MonthlyRestLimits.FirstOrDefaultAsync(l => l.Year == year && l.Month == month, ct);
        if (entity is null)
        {
            entity = new MonthlyRestLimit { Year = year, Month = month };
            db.MonthlyRestLimits.Add(entity);
        }
        entity.MaxRestDays = maxRestDays;

        await db.SaveChangesAsync(ct);
        return new RestDayLimitResponse(year, month, maxRestDays, false, used);
    }

    // ---------- Utilidades ----------

    public async Task<(int Limit, bool IsDefault)> GetEffectiveLimitAsync(int year, int month, CancellationToken ct)
    {
        // Select de un int? => FirstOrDefault devuelve null si no hay fila (en vez de 0).
        var custom = await db.MonthlyRestLimits
            .Where(l => l.Year == year && l.Month == month)
            .Select(l => (int?)l.MaxRestDays)
            .FirstOrDefaultAsync(ct);

        return custom is { } value ? (value, false) : (options.Value.DefaultMonthlyLimit, true);
    }

    private Task<int> CountInMonthAsync(int year, int month, CancellationToken ct)
    {
        var (first, last) = Months.Range(year, month);
        // Rango de fechas (no YEAR()/MONTH() sobre la columna) para que SQL use el índice en Date.
        return db.RestDays.CountAsync(r => r.Date >= first && r.Date <= last, ct);
    }

    private static Error NotFound(DateOnly date) =>
        Error.NotFound("RestDayNotFound", $"El {date:yyyy-MM-dd} no está marcado como descanso.");
}
