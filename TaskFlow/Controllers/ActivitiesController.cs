using Microsoft.AspNetCore.Mvc;
using TaskFlow.DTOs;
using TaskFlow.Services;

namespace TaskFlow.Controllers;

// Controlador delgado: recibe HTTP, delega al servicio y traduce el Result a una respuesta.
// Ninguna regla de negocio vive aquí.
[Route("api/activities")]
public sealed class ActivitiesController(IActivityService activities) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ActivityResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ActivityResponse>>> GetAll(CancellationToken ct) =>
        Ok(await activities.GetAllAsync(ct));

    // "{id:int}" es una restricción de ruta: "/api/activities/abc" ni siquiera entra aquí (404).
    // Name = ... permite generar la URL desde CreatedAtRoute.
    [HttpGet("{id:int}", Name = nameof(GetActivityById))]
    [ProducesResponseType<ActivityResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ActivityResponse>> GetActivityById(int id, CancellationToken ct)
    {
        var result = await activities.GetByIdAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : ErrorResult(result.Error!);
    }

    [HttpPost]
    [ProducesResponseType<ActivityResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ActivityResponse>> Create(CreateActivityRequest request, CancellationToken ct)
    {
        var result = await activities.CreateAsync(request, ct);
        if (!result.IsSuccess)
            return ErrorResult(result.Error!);

        // 201 + encabezado Location: /api/activities/{id}
        return CreatedAtRoute(nameof(GetActivityById), new { id = result.Value.Id }, result.Value);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType<ActivityResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ActivityResponse>> Update(int id, UpdateActivityRequest request, CancellationToken ct)
    {
        var result = await activities.UpdateAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : ErrorResult(result.Error!);
    }

    [HttpPatch("{id:int}/status")]
    [ProducesResponseType<ActivityResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ActivityResponse>> SetStatus(int id, SetActivityStatusRequest request, CancellationToken ct)
    {
        var result = await activities.SetStatusAsync(id, request.IsActive!.Value, ct);
        return result.IsSuccess ? Ok(result.Value) : ErrorResult(result.Error!);
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await activities.DeleteAsync(id, ct);
        return result.IsSuccess ? NoContent() : ErrorResult(result.Error!);
    }
}
