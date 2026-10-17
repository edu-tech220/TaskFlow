using TaskFlow.Entities;

namespace TaskFlow.Services;

// Reglas puras sobre versiones de horario. Sin BD ni estado: fáciles de probar y
// reutilizadas por actividades, registros diarios y calendario.
public static class ScheduleRules
{
    // ¿La versión [from, to] está vigente en la fecha? to = null => abierta.
    public static bool Covers(DateOnly effectiveFrom, DateOnly? effectiveTo, DateOnly date) =>
        effectiveFrom <= date && (effectiveTo is null || effectiveTo >= date);

    public static bool Covers(ActivitySchedule schedule, DateOnly date) =>
        Covers(schedule.EffectiveFrom, schedule.EffectiveTo, date);

    // ¿La actividad está programada en esa fecha según esta versión?
    public static bool IsScheduledOn(ActivitySchedule schedule, DateOnly date) =>
        Covers(schedule, date) && schedule.Days.Any(d => d.DayOfWeek == date.DayOfWeek);

    // Activa = tiene alguna versión que sigue aplicando después de hoy.
    // Al desactivar, la vigente se cierra con EffectiveTo = hoy, así que deja de contar.
    public static bool IsActive(IEnumerable<ActivitySchedule> schedules, DateOnly today) =>
        schedules.Any(s => s.EffectiveTo is null || s.EffectiveTo > today);
}
