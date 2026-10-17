namespace TaskFlow.Enums;

// Estados CALCULADOS: nunca se guardan en la BD, se derivan al consultar.
// El backend devuelve estos nombres; el frontend decide colores e íconos.

// Estado de una actividad en una fecha.
public enum ActivityDayStatus
{
    Scheduled,     // fecha futura
    Pending,       // hoy, sin registro
    Completed,     // con registro Completed
    NotCompleted,  // con registro NotCompleted
    Unrecorded     // día pasado, sin registro
}

// Estado de un día completo. El orden de evaluación está en DayStatusCalculator.
public enum DayStatus
{
    Future,
    RestDay,
    NoActivities,
    Completed,
    InProgress,
    Failed,
    Partial
}
