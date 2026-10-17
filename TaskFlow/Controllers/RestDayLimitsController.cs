using Microsoft.AspNetCore.Mvc;
using TaskFlow.DTOs;
using TaskFlow.Services;

namespace TaskFlow.Controllers;

[Route("api/rest-day-limits")]
public sealed class RestDayLimitsController(IRestDayService restDays) : ApiControllerBase
{
    [HttpGet("{year:int}/{month:int}")]
    [ProducesResponseType<RestDayLimitResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RestDayLimitResponse>> Get(int year, int month, CancellationToken ct)
    {
        var result = await restDays.GetLimitAsync(year, month, ct);
        return result.IsSuccess ? Ok(result.Value) : ErrorResult(result.Error!);
    }

    // PUT idempotente: fija el límite del mes (crea o actualiza la fila).
    [HttpPut("{year:int}/{month:int}")]
    [ProducesResponseType<RestDayLimitResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RestDayLimitResponse>> Put(
        int year, int month, SetRestDayLimitRequest request, CancellationToken ct)
    {
        var result = await restDays.SetLimitAsync(year, month, request.MaxRestDays!.Value, ct);
        return result.IsSuccess ? Ok(result.Value) : ErrorResult(result.Error!);
    }
}
