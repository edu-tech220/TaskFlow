using System.ComponentModel.DataAnnotations;
using TaskFlow.Enums;

namespace TaskFlow.DTOs;

public sealed record RegisterRequest
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; init; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 2)]
    public string DisplayName { get; init; } = string.Empty;

    // Política mínima: 8+ caracteres con al menos una letra y un número.
    [Required, StringLength(128, MinimumLength = 8)]
    [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d).+$",
        ErrorMessage = "La contraseña debe tener al menos una letra y un número.")]
    public string Password { get; init; } = string.Empty;
}

public sealed record LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;
}

public sealed record UpdateProfileRequest
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string DisplayName { get; init; } = string.Empty;
}

public sealed record ChangePasswordRequest
{
    [Required]
    public string CurrentPassword { get; init; } = string.Empty;

    [Required, StringLength(128, MinimumLength = 8)]
    [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d).+$",
        ErrorMessage = "La contraseña debe tener al menos una letra y un número.")]
    public string NewPassword { get; init; } = string.Empty;
}

// Solo administradores: cambiar rol o activar/desactivar a otro usuario.
public sealed record UpdateUserRequest
{
    [Required]
    public UserRole? Role { get; init; }

    [Required]
    public bool? IsActive { get; init; }
}

public sealed record UserResponse(
    int Id,
    string Email,
    string DisplayName,
    UserRole Role,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? LastLoginAt);

// Respuesta de login/registro: el cliente manda Token en "Authorization: Bearer ...".
public sealed record AuthResponse(string Token, DateTime ExpiresAt, UserResponse User);

// Vista de administración: usuario + cuántas actividades tiene.
public sealed record AdminUserResponse(
    int Id,
    string Email,
    string DisplayName,
    UserRole Role,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? LastLoginAt,
    int ActivityCount);
