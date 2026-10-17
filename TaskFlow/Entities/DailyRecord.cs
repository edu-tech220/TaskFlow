using TaskFlow.Enums;

namespace TaskFlow.Entities;

// Lo que realmente pasó con una actividad en un día concreto.
// Solo existe si el usuario registró algo: "pendiente" = no hay fila.
// Máximo un registro por (ActivityId, Date): índice único en la configuración.
public class DailyRecord : IHasCreatedAt, IHasUpdatedAt
{
    public int Id { get; set; }

    // FK a Activity con borrado RESTRICT (ver configuración): no se puede borrar una
    // actividad con historial; hay que desactivarla.
    public int ActivityId { get; set; }

    public DateOnly Date { get; set; }

    // Enum => int en la BD (1 = Completed, 2 = NotCompleted).
    public DailyRecordStatus Status { get; set; }

    // Solo tiene valor con Completed (regla reforzada con un CHECK en la BD).
    public TimeOnly? ActualEndTime { get; set; }

    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Activity Activity { get; set; } = null!;
}
