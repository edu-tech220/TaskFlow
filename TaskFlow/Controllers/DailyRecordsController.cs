using Microsoft.AspNetCore.Mvc;
using TaskFlow.Common;
using TaskFlow.DTOs;
using TaskFlow.Services;

namespace TaskFlow.Controllers;

[Route("api/daily-records")]
public sealed class DailyRecordsController(IDailyRecordService records) : ApiControllerBase
{
    // Un solo GET con dos formas de uso:
    //   /api/daily-records?date=2026-10-17
    //   /api/daily-records?from=2026-10-01&to=2026-10-31
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<DailyRecordResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<DailyRecordResponse>>> Get(
        [FromQuery] DateOnly? date,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct)
    {
        if (date is { } day && from is null && to is null)
            return Ok(await records.GetByDateAsync(day, ct));

        if (date is null && from is { } start && to is { } end)
        {
            var result = await records.GetRangeAsync(start, end, ct);
            return result.IsSuccess ? Ok(result.Value) : ErrorResult(result.Error!);
        }

        return ErrorResult(Error.Validation("InvalidQuery",
            "Usa '?date=AAAA-MM-DD' o '?from=AAAA-MM-DD&to=AAAA-MM-DD'."));
    }

    [HttpGet("{activityId:int}/{date}", Name = nameof(GetDailyRecord))]
    [ProducesResponseType<DailyRecordResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DailyRecordResponse>> GetDailyRecord(int activityId, DateOnly date, CancellationToken ct)
    {
        var result = await records.GetAsync(activityId, date, ct);
        return result.IsSuccess ? Ok(result.Value) : ErrorResult(result.Error!);
    }

    // PUT es el verbo natural de un upsert: la URL identifica el recurso (actividad + fecha).
    // Crear => 201 con Location; actualizar => 200.
    [HttpPut("{activityId:int}/{date}")]
    [ProducesResponseType<DailyRecordResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<DailyRecordResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DailyRecordResponse>> Upsert(
        int activityId, DateOnly date, UpsertDailyRecordRequest request, CancellationToken ct)
    {
        var result = await records.UpsertAsync(activityId, date, request, ct);
        if (!result.IsSuccess)
            return ErrorResult(result.Error!);

        var (created, record) = result.Value;
        return created
            ? CreatedAtRoute(nameof(GetDailyRecord),
                new { activityId, date = date.ToString("yyyy-MM-dd") }, record)
            : Ok(record);
    }

    [HttpDelete("{activityId:int}/{date}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int activityId, DateOnly date, CancellationToken ct)
    {
        var result = await records.DeleteAsync(activityId, date, ct);
        return result.IsSuccess ? NoContent() : ErrorResult(result.Error!);
    }
}
