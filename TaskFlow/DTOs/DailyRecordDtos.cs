using System.ComponentModel.DataAnnotations;
using TaskFlow.Enums;

namespace TaskFlow.DTOs;

public sealed record UpsertDailyRecordRequest : IValidatableObject
{
    // "Completed" o "NotCompleted" (también 1 o 2).
    [Required]
    public DailyRecordStatus? Status { get; init; }

    // Hora real de término; solo con Completed (regla 14).
    public TimeOnly? ActualEndTime { get; init; }

    [StringLength(500)]
    public string? Notes { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Status is { } status && !Enum.IsDefined(status))
            yield return new ValidationResult("Status debe ser Completed o NotCompleted.", [nameof(Status)]);

        if (Status == DailyRecordStatus.NotCompleted && ActualEndTime is not null)
            yield return new ValidationResult(
                "ActualEndTime solo se permite cuando Status es Completed.", [nameof(ActualEndTime)]);
    }
}

public sealed record DailyRecordResponse(
    int Id,
    int ActivityId,
    string ActivityName,
    DateOnly Date,
    DailyRecordStatus Status,
    TimeOnly? ActualEndTime,
    string? Notes,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

// Resultado interno del upsert: el controlador decide 201 (creado) o 200 (actualizado).
public sealed record UpsertDailyRecordResult(bool Created, DailyRecordResponse Record);
