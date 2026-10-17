using TaskFlow.Enums;

namespace TaskFlow.Services;

// Cálculo puro de estados: sin BD, sin reloj (recibe "today" como parámetro).
// Por eso se prueba con pruebas unitarias simples, una por regla.
public static class DayStatusCalculator
{
    // Estado de una actividad programada en una fecha.
    public static ActivityDayStatus GetActivityStatus(DateOnly date, DateOnly today, DailyRecordStatus? record)
    {
        if (date > today)
            return ActivityDayStatus.Scheduled;

        return record switch
        {
            DailyRecordStatus.Completed => ActivityDayStatus.Completed,
            DailyRecordStatus.NotCompleted => ActivityDayStatus.NotCompleted,
            // Sin registro: hoy todavía se puede hacer; en el pasado ya no se registró.
            _ => date == today ? ActivityDayStatus.Pending : ActivityDayStatus.Unrecorded
        };
    }

    // Estado del día. Gana la PRIMERA regla que se cumpla:
    //   T = programadas, C = completadas, M = programadas sin registro.
    // En días pasados las no registradas cuentan como no completadas (no son C).
    public static DayStatus GetDayStatus(
        DateOnly date, DateOnly today, bool isRestDay, int scheduled, int completed, int missing)
    {
        if (date > today) return DayStatus.Future;                       // 1
        if (isRestDay) return DayStatus.RestDay;                         // 2
        if (scheduled == 0) return DayStatus.NoActivities;               // 3
        if (completed == scheduled) return DayStatus.Completed;          // 4
        if (date == today && missing > 0) return DayStatus.InProgress;   // 5
        if (completed == 0) return DayStatus.Failed;                     // 6
        return DayStatus.Partial;                                        // 7
    }
}
