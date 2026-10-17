using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Common;
using TaskFlow.Data;
using TaskFlow.DTOs;
using TaskFlow.Entities;
using TaskFlow.Enums;

namespace TaskFlow.Services;

public interface IAuthService
{
    Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken ct);
    Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken ct);
    Task<Result<UserResponse>> GetCurrentAsync(CancellationToken ct);
    Task<Result<UserResponse>> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken ct);
    Task<Result> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct);
}

// Registro, login y perfil propio.
// PasswordHasher<User> (de Microsoft.Extensions.Identity.Core) implementa PBKDF2 con
// HMAC-SHA512, salt aleatorio y 100,000 iteraciones: lo mismo que usa ASP.NET Identity,
// sin tener que adoptar todo Identity (tablas AspNet*, UserManager, etc.).
public sealed class AuthService(
    TaskFlowDbContext db,
    IPasswordHasher<User> hasher,
    IJwtTokenService tokens,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IAuthService
{
    public async Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var email = NormalizeEmail(request.Email);

        if (await db.Users.AnyAsync(u => u.Email == email, ct))
            return Error.Conflict("EmailAlreadyRegistered", "Ya existe una cuenta con ese correo.");

        var user = new User
        {
            Email = email,
            DisplayName = request.DisplayName.Trim(),
            // El primer usuario del sistema es el administrador; los demás, usuarios normales.
            Role = await db.Users.AnyAsync(ct) ? UserRole.User : UserRole.Admin,
            LastLoginAt = timeProvider.GetUtcNow().UtcDateTime
        };
        user.PasswordHash = hasher.HashPassword(user, request.Password);

        db.Users.Add(user);
        await db.SaveChangesAsync(ct); // índice único en Email cubre registros simultáneos (=> 409)

        return Issue(user);
    }

    public async Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var email = NormalizeEmail(request.Email);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

        // Mismo mensaje si no existe el correo o si la contraseña es incorrecta: no revelar
        // qué correos están registrados (enumeración de usuarios).
        var invalid = Error.Unauthorized("InvalidCredentials", "Correo o contraseña incorrectos.");
        if (user is null)
            return invalid;

        var verification = hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed)
            return invalid;

        if (!user.IsActive)
            return Error.Unauthorized("UserDisabled", "Tu cuenta está desactivada. Contacta a un administrador.");

        // Si el hash usa parámetros viejos, se regenera con los actuales de forma transparente.
        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
            user.PasswordHash = hasher.HashPassword(user, request.Password);

        user.LastLoginAt = timeProvider.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);

        return Issue(user);
    }

    public async Task<Result<UserResponse>> GetCurrentAsync(CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == currentUser.UserId, ct);
        return user is null ? UserNotFound() : ToResponse(user);
    }

    public async Task<Result<UserResponse>> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == currentUser.UserId, ct);
        if (user is null)
            return UserNotFound();

        user.DisplayName = request.DisplayName.Trim();
        await db.SaveChangesAsync(ct);
        return ToResponse(user);
    }

    public async Task<Result> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == currentUser.UserId, ct);
        if (user is null)
            return UserNotFound();

        if (hasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword) == PasswordVerificationResult.Failed)
            return Error.Validation("InvalidCurrentPassword", "La contraseña actual no es correcta.");

        user.PasswordHash = hasher.HashPassword(user, request.NewPassword);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    private AuthResponse Issue(User user)
    {
        var (token, expiresAt) = tokens.CreateToken(user);
        return new AuthResponse(token, expiresAt, ToResponse(user));
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static Error UserNotFound() => Error.NotFound("UserNotFound", "El usuario no existe.");

    internal static UserResponse ToResponse(User u) =>
        new(u.Id, u.Email, u.DisplayName, u.Role, u.IsActive, u.CreatedAt, u.LastLoginAt);
}
