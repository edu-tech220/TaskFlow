using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.DTOs;
using TaskFlow.Services;

namespace TaskFlow.Controllers;

[Route("api/calendar")]
public sealed class CalendarController(ICalendarService calendar) : ApiControllerBase
{
    // GET /api/calendar?year=2026&month=10
    [HttpGet]
    [ProducesResponseType<CalendarMonthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CalendarMonthResponse>> GetMonth(
        [FromQuery, Required] int? year,
        [FromQuery, Required] int? month,
        CancellationToken ct)
    {
        var result = await calendar.GetMonthAsync(year!.Value, month!.Value, ct);
        return result.IsSuccess ? Ok(result.Value) : ErrorResult(result.Error!);
    }

    // GET /api/calendar/days/2026-10-17 — detalle de un solo día (mismo cálculo que el mes).
    [HttpGet("days/{date}")]
    [ProducesResponseType<CalendarDayResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CalendarDayResponse>> GetDay(DateOnly date, CancellationToken ct) =>
        Ok(await calendar.GetDayAsync(date, ct));
}
