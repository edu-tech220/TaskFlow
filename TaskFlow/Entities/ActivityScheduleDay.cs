namespace TaskFlow.Entities;

// Tabla puente: qué días de la semana aplica una versión de horario.
// No tiene "Id": su PK es compuesta (ActivityScheduleId, DayOfWeek). Como EF no puede
// deducir una PK compuesta por convención, se declara con HasKey en la configuración;
// sin eso, EF lanza un error al construir el modelo.
public class ActivityScheduleDay
{
    public int ActivityScheduleId { get; set; }

    // System.DayOfWeek (0 = domingo ... 6 = sábado). Es un enum, así que se guarda como int.
    // Que la propiedad se llame igual que su tipo es válido en C#.
    public DayOfWeek DayOfWeek { get; set; }

    public ActivitySchedule ActivitySchedule { get; set; } = null!;
}
