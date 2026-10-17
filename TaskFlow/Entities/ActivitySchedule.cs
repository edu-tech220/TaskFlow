namespace TaskFlow.Entities;

// Versión de horario de una actividad. Es válida en el rango cerrado
// [EffectiveFrom, EffectiveTo]; EffectiveTo = null significa "abierta" (sin fin).
// Cambiar días u horas NO edita esta fila: se cierra (EffectiveTo = ayer del cambio)
// y se crea una versión nueva. Así el historial de días pasados no se corrompe.
public class ActivitySchedule
{
    public int Id { get; set; }

    // Convención: "<NombreNavegación>Id" junto a la navegación "Activity" => FK a Activities.Id.
    public int ActivityId { get; set; }

    // TimeOnly => columna time. Sin fecha ni zona horaria: es "la hora del reloj de pared".
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }

    // DateOnly => columna date. Soportado nativamente por el proveedor de SQL Server desde EF Core 8.
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }

    // Navegación al "padre". "= null!" le dice al compilador: "no será null, EF la llena
    // cuando la cargue". Nunca la construyes tú; para crear la relación basta ActivityId
    // o agregar el schedule a activity.Schedules.
    public Activity Activity { get; set; } = null!;

    // Días de la semana en que aplica esta versión (al menos uno, validado en el servicio).
    public ICollection<ActivityScheduleDay> Days { get; set; } = new List<ActivityScheduleDay>();
}
