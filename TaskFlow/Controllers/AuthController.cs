using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TaskFlow.Common;
using TaskFlow.DTOs;
using TaskFlow.Services;

namespace TaskFlow.Controllers;

[Route("api/auth")]
public sealed class AuthController(IAuthService auth) : ApiControllerBase
{
    // [AllowAnonymous]: excepción a la política global que exige estar autenticado.
    // [EnableRateLimiting]: límite estricto por IP contra fuerza bruta.
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitOptions.AuthPolicy)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken ct)
    {
        var result = await auth.RegisterAsync(request, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(Me), null, result.Value)
            : ErrorResult(result.Error!);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitOptions.AuthPolicy)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var result = await auth.LoginAsync(request, ct);
        return result.IsSuccess ? Ok(result.Value) : ErrorResult(result.Error!);
    }

    [HttpGet("me")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<UserResponse>> Me(CancellationToken ct)
    {
        var result = await auth.GetCurrentAsync(ct);
        return result.IsSuccess ? Ok(result.Value) : ErrorResult(result.Error!);
    }

    [HttpPut("me")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<UserResponse>> UpdateProfile(UpdateProfileRequest request, CancellationToken ct)
    {
        var result = await auth.UpdateProfileAsync(request, ct);
        return result.IsSuccess ? Ok(result.Value) : ErrorResult(result.Error!);
    }

    [HttpPut("me/password")]
    [EnableRateLimiting(RateLimitOptions.AuthPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        var result = await auth.ChangePasswordAsync(request, ct);
        return result.IsSuccess ? NoContent() : ErrorResult(result.Error!);
    }
}
