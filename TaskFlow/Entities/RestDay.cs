namespace TaskFlow.Entities;

// Día marcado como descanso. Date es única (no puede haber dos descansos el mismo día).
// No tiene relación con Activity: un descanso aplica al día completo.
// Solo CreatedAt: un descanso no se edita, se crea o se borra.
public class RestDay : IHasCreatedAt
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}
