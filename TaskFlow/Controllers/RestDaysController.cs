using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.DTOs;
using TaskFlow.Services;

namespace TaskFlow.Controllers;

[Route("api/rest-days")]
public sealed class RestDaysController(IRestDayService restDays) : ApiControllerBase
{
    // GET /api/rest-days?year=2026&month=10
    // [FromQuery] + [Required]: si falta el parámetro, [ApiController] responde 400 automáticamente.
    [HttpGet]
    [ProducesResponseType<RestDayMonthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RestDayMonthResponse>> GetMonth(
        [FromQuery, Required] int? year,
        [FromQuery, Required] int? month,
        CancellationToken ct)
    {
        var result = await restDays.GetMonthAsync(year!.Value, month!.Value, ct);
        return result.IsSuccess ? Ok(result.Value) : ErrorResult(result.Error!);
    }

    // DateOnly se enlaza desde la ruta en formato ISO: /api/rest-days/2026-10-20
    [HttpGet("{date}", Name = nameof(GetRestDayByDate))]
    [ProducesResponseType<RestDayResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RestDayResponse>> GetRestDayByDate(DateOnly date, CancellationToken ct)
    {
        var result = await restDays.GetByDateAsync(date, ct);
        return result.IsSuccess ? Ok(result.Value) : ErrorResult(result.Error!);
    }

    [HttpPost]
    [ProducesResponseType<RestDayResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RestDayResponse>> Create(CreateRestDayRequest request, CancellationToken ct)
    {
        var result = await restDays.CreateAsync(request, ct);
        if (!result.IsSuccess)
            return ErrorResult(result.Error!);

        return CreatedAtRoute(nameof(GetRestDayByDate),
            new { date = result.Value.Date.ToString("yyyy-MM-dd") }, result.Value);
    }

    [HttpDelete("{date}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(DateOnly date, CancellationToken ct)
    {
        var result = await restDays.DeleteAsync(date, ct);
        return result.IsSuccess ? NoContent() : ErrorResult(result.Error!);
    }
}
