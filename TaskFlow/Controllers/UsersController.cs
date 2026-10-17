using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.DTOs;
using TaskFlow.Services;

namespace TaskFlow.Controllers;

// Solo administradores. Si el token no trae role=Admin, el middleware responde 403
// antes de entrar a la acción.
[Route("api/users")]
[Authorize(Roles = "Admin")]
public sealed class UsersController(IUserAdminService users) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AdminUserResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<AdminUserResponse>>> GetAll(CancellationToken ct) =>
        Ok(await users.GetAllAsync(ct));

    [HttpPut("{id:int}")]
    [ProducesResponseType<AdminUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AdminUserResponse>> Update(int id, UpdateUserRequest request, CancellationToken ct)
    {
        var result = await users.UpdateAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : ErrorResult(result.Error!);
    }
}
