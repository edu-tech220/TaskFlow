using Microsoft.AspNetCore.Mvc;
using TaskFlow.Web.Models;
using TaskFlow.Web.Services;

namespace TaskFlow.Web.Controllers;

public sealed class ActivitiesController(TaskFlowApiClient api) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var result = await api.GetActivitiesAsync(ct);
        return result.IsSuccess ? View(result.Value) : View("ApiError", result.Error);
    }

    public IActionResult Create() =>
        View(new ActivityFormViewModel
        {
            StartTime = new TimeOnly(7, 0),
            EndTime = new TimeOnly(8, 0),
            Days = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday]
        });

    [HttpPost]
    public async Task<IActionResult> Create(ActivityFormViewModel form, CancellationToken ct)
    {
        form.ChangeSchedule = true;
        if (!ModelState.IsValid)
            return View(form);

        var result = await api.CreateActivityAsync(form.ToCreateRequest(), ct);
        if (!result.IsSuccess)
        {
            // El error de la API se muestra arriba del formulario, sin perder lo capturado.
            ModelState.AddModelError(string.Empty, result.Error!);
            return View(form);
        }

        TempData["Success"] = $"Actividad \"{result.Value!.Name}\" creada.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var result = await api.GetActivityAsync(id, ct);
        if (!result.IsSuccess)
            return View("ApiError", result.Error);

        var activity = result.Value!;
        var schedule = activity.CurrentSchedule ?? activity.Schedules.FirstOrDefault();
        return View(new ActivityFormViewModel
        {
            Id = activity.Id,
            Name = activity.Name,
            Description = activity.Description,
            ChangeSchedule = false,
            StartTime = schedule?.StartTime,
            EndTime = schedule?.EndTime,
            Days = schedule?.Days.ToList() ?? [],
            Versions = activity.Schedules
        });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, ActivityFormViewModel form, CancellationToken ct)
    {
        form.Id = id;
        if (!ModelState.IsValid)
            return View(await WithVersionsAsync(form, ct));

        var result = await api.UpdateActivityAsync(id, form.ToUpdateRequest(), ct);
        if (!result.IsSuccess)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View(await WithVersionsAsync(form, ct));
        }

        TempData["Success"] = form.ChangeSchedule
            ? "Actividad actualizada. El nuevo horario aplica a partir de la fecha indicada (por defecto, mañana)."
            : "Actividad actualizada.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> ToggleStatus(int id, bool isActive, CancellationToken ct)
    {
        var result = await api.SetActivityStatusAsync(id, isActive, ct);
        if (result.IsSuccess)
            TempData["Success"] = isActive ? "Actividad reactivada (desde mañana)." : "Actividad desactivada (deja de aplicar después de hoy).";
        else
            TempData["Error"] = result.Error;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await api.DeleteActivityAsync(id, ct);
        if (result.IsSuccess) TempData["Success"] = "Actividad eliminada.";
        else TempData["Error"] = result.Error;
        return RedirectToAction(nameof(Index));
    }

    private async Task<ActivityFormViewModel> WithVersionsAsync(ActivityFormViewModel form, CancellationToken ct)
    {
        var current = await api.GetActivityAsync(form.Id!.Value, ct);
        form.Versions = current.Value?.Schedules ?? [];
        return form;
    }
}
