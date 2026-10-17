namespace TaskFlow.Enums;

// Estado PERSISTIDO de un registro diario. EF Core guarda los enums como int por convención.
// Los valores son explícitos (1 y 2) a propósito: lo que vive en la BD es el número, así que
// si algún día reordenas o insertas miembros, los datos existentes no cambian de significado.
// "Pending" y "Unrecorded" NO van aquí: nunca se guardan, se deducen de que NO haya registro.
public enum DailyRecordStatus
{
    Completed = 1,
    NotCompleted = 2
}
