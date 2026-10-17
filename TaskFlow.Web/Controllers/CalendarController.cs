using Microsoft.AspNetCore.Mvc;
using TaskFlow.Web.Models;
using TaskFlow.Web.Services;

namespace TaskFlow.Web.Controllers;

// Calendario mensual, detalle del día y acciones del día (registrar, descansos, límite).
// Patrón PRG (Post-Redirect-Get): cada POST termina en un Redirect para que recargar la
// página no reenvíe el formulario. Los mensajes viajan en TempData (sobreviven un redirect).
public sealed class CalendarController(TaskFlowApiClient api) : Controller
{
    public async Task<IActionResult> Index(int? year, int? month, CancellationToken ct)
    {
        var now = DateTime.Now;
        var result = await api.GetCalendarAsync(year ?? now.Year, month ?? now.Month, ct);
        if (!result.IsSuccess)
            return View("ApiError", result.Error);

        return View(new CalendarViewModel { Calendar = result.Value! });
    }

    // /Calendar/Day?date=2026-10-17
    public async Task<IActionResult> Day(DateOnly date, CancellationToken ct)
    {
        // El calendario del mes trae "hoy", el límite de descansos y el día pedido en una llamada.
        var result = await api.GetCalendarAsync(date.Year, date.Month, ct);
        if (!result.IsSuccess)
            return View("ApiError", result.Error);

        var month = result.Value!;
        return View(new DayViewModel
        {
            Day = month.Days.Single(d => d.Date == date),
            Today = month.Today,
            RestDayLimit = month.RestDayLimit,
            RestDaysUsed = month.RestDaysUsed
        });
    }

    [HttpPost]
    public async Task<IActionResult> Record(
        int activityId, DateOnly date, DailyRecordStatus status, TimeOnly? actualEndTime, string? notes, CancellationToken ct)
    {
        var result = await api.UpsertRecordAsync(activityId, date,
            status, status == DailyRecordStatus.Completed ? actualEndTime : null, notes, ct);
        Notify(result.Error, status == DailyRecordStatus.Completed ? "Marcada como completada." : "Marcada como no completada.");
        return RedirectToAction(nameof(Day), new { date = date.ToString("yyyy-MM-dd") });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteRecord(int activityId, DateOnly date, CancellationToken ct)
    {
        var result = await api.DeleteRecordAsync(activityId, date, ct);
        Notify(result.Error, "Registro eliminado.");
        return RedirectToAction(nameof(Day), new { date = date.ToString("yyyy-MM-dd") });
    }

    [HttpPost]
    public async Task<IActionResult> AddRestDay(DateOnly date, string? notes, CancellationToken ct)
    {
        var result = await api.AddRestDayAsync(date, notes, ct);
        Notify(result.Error, "Día marcado como descanso.");
        return RedirectToAction(nameof(Day), new { date = date.ToString("yyyy-MM-dd") });
    }

    [HttpPost]
    public async Task<IActionResult> RemoveRestDay(DateOnly date, CancellationToken ct)
    {
        var result = await api.RemoveRestDayAsync(date, ct);
        Notify(result.Error, "Descanso quitado.");
        return RedirectToAction(nameof(Day), new { date = date.ToString("yyyy-MM-dd") });
    }

    [HttpPost]
    public async Task<IActionResult> SetLimit(int year, int month, int maxRestDays, CancellationToken ct)
    {
        var result = await api.SetRestDayLimitAsync(year, month, maxRestDays, ct);
        Notify(result.Error, $"Límite de descansos actualizado a {maxRestDays}.");
        return RedirectToAction(nameof(Index), new { year, month });
    }

    private void Notify(string? error, string success)
    {
        if (error is null) TempData["Success"] = success;
        else TempData["Error"] = error;
    }
}
