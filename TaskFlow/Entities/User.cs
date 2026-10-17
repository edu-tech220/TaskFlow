using TaskFlow.Enums;

namespace TaskFlow.Entities;

// Usuario de la aplicación. Cada actividad, descanso y límite mensual pertenece a uno.
// Los registros diarios y las versiones de horario quedan ligados a través de su actividad.
public class User : IHasCreatedAt, IHasUpdatedAt
{
    public int Id { get; set; }

    // Siempre en minúsculas y sin espacios (normalizado en AuthService): índice único.
    public string Email { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    // Hash PBKDF2 con salt generado por PasswordHasher<User>. Nunca se guarda la contraseña.
    // El formato incluye versión y parámetros, así que el algoritmo puede mejorar sin migrar.
    public string PasswordHash { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.User;

    // Un usuario desactivado no puede iniciar sesión y sus tokens dejan de valer.
    public bool IsActive { get; set; } = true;

    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ICollection<Activity> Activities { get; set; } = new List<Activity>();
}
