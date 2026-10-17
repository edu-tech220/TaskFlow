using System.ComponentModel.DataAnnotations;

namespace TaskFlow.DTOs;

// DTOs = el contrato público de la API. Nunca se exponen entidades: así puedes cambiar la BD
// sin romper clientes y no se filtran navegaciones ni ciclos de serialización.
//
// Las peticiones son records con propiedades init y DataAnnotations. [ApiController] las
// valida antes de entrar a la acción. Los tipos de valor obligatorios son nullable
// (TimeOnly?) para que [Required] detecte cuando el campo NO viene en el JSON; si fueran
// no-nullable, un campo ausente llegaría como 00:00 y pasaría la validación.

// Horario que envía el cliente al crear una actividad o cambiar su horario.
public sealed record ScheduleRequest : IValidatableObject
{
    [Required]
    public TimeOnly? StartTime { get; init; }

    [Required]
    public TimeOnly? EndTime { get; init; }

    // Días de la semana: acepta "Monday" o 1 (JsonStringEnumConverter permite ambos).
    [Required, MinLength(1, ErrorMessage = "Selecciona al menos un día.")]
    public IReadOnlyList<DayOfWeek>? Days { get; init; }

    // Al crear: inicio de la primera versión (default hoy, puede ser pasada).
    // Al cambiar horario: inicio de la nueva versión (default mañana).
    public DateOnly? EffectiveFrom { get; init; }

    // IValidatableObject: reglas que involucran varias propiedades. MVC lo llama después
    // de que pasan las validaciones por atributo.
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartTime is { } start && EndTime is { } end && end <= start)
            yield return new ValidationResult(
                "La hora de fin debe ser mayor que la de inicio (sin cruzar medianoche).",
                [nameof(EndTime)]);

        if (Days is not null && Days.Any(d => !Enum.IsDefined(d)))
            yield return new ValidationResult(
                "Los días deben estar entre 0 (domingo) y 6 (sábado).",
                [nameof(Days)]);
    }
}

public sealed record CreateActivityRequest
{
    [Required, StringLength(100)]
    public string Name { get; init; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; init; }

    [Required]
    public ScheduleRequest? Schedule { get; init; }
}

public sealed record UpdateActivityRequest
{
    [Required, StringLength(100)]
    public string Name { get; init; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; init; }

    // Opcional: si viene, se aplica el versionado de horarios (reglas 3 y 4).
    public ScheduleRequest? Schedule { get; init; }
}

public sealed record SetActivityStatusRequest
{
    [Required]
    public bool? IsActive { get; init; }
}

// ---------- Respuestas ----------
// Records posicionales: inmutables y concisos.

public sealed record ScheduleVersionResponse(
    int Id,
    TimeOnly StartTime,
    TimeOnly EndTime,
    IReadOnlyList<DayOfWeek> Days,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo);

public sealed record ActivityResponse(
    int Id,
    string Name,
    string? Description,
    bool IsActive,
    // Versión que aplica hoy o, si no hay, la próxima programada. Null si está desactivada.
    ScheduleVersionResponse? CurrentSchedule,
    // Historial completo de versiones, de la más reciente a la más antigua.
    IReadOnlyList<ScheduleVersionResponse> Schedules,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
