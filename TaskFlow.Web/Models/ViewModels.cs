using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace TaskFlow.Web.Models;

// Formulario de alta/edición de actividad. La validación del lado del cliente es solo
// comodidad; la API vuelve a validar todo y es la fuente de verdad.
public sealed class ActivityFormViewModel : IValidatableObject
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(100)]
    [Display(Name = "Nombre")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    [Display(Name = "Descripción")]
    public string? Description { get; set; }

    // En edición, el horario solo se envía si se marca este checkbox (versionado).
    [Display(Name = "Cambiar horario")]
    public bool ChangeSchedule { get; set; } = true;

    [Display(Name = "Hora de inicio")]
    public TimeOnly? StartTime { get; set; }

    [Display(Name = "Hora de fin")]
    public TimeOnly? EndTime { get; set; }

    [Display(Name = "Días")]
    public List<DayOfWeek> Days { get; set; } = [];

    [Display(Name = "Aplica desde")]
    public DateOnly? EffectiveFrom { get; set; }

    // Historial para mostrar en la edición.
    public IReadOnlyList<ScheduleVersionDto> Versions { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!ChangeSchedule)
            yield break;

        if (StartTime is null || EndTime is null)
            yield return new ValidationResult("Indica hora de inicio y fin.", [nameof(StartTime)]);
        else if (EndTime <= StartTime)
            yield return new ValidationResult("La hora de fin debe ser mayor que la de inicio.", [nameof(EndTime)]);

        if (Days.Count == 0)
            yield return new ValidationResult("Selecciona al menos un día.", [nameof(Days)]);
    }

    // Cuerpo JSON para la API.
    public object ToCreateRequest() => new
    {
        name = Name,
        description = Description,
        schedule = new { startTime = StartTime, endTime = EndTime, days = Days, effectiveFrom = EffectiveFrom }
    };

    public object ToUpdateRequest() => new
    {
        name = Name,
        description = Description,
        schedule = ChangeSchedule
            ? new { startTime = StartTime, endTime = EndTime, days = Days, effectiveFrom = EffectiveFrom }
            : null
    };
}

public sealed class CalendarViewModel
{
    public required CalendarMonthDto Calendar { get; init; }

    public DateOnly FirstOfMonth => new(Calendar.Year, Calendar.Month, 1);
    public DateOnly PreviousMonth => FirstOfMonth.AddMonths(-1);
    public DateOnly NextMonth => FirstOfMonth.AddMonths(1);

    // Celdas vacías antes del día 1 para una semana que empieza en lunes.
    public int LeadingBlanks => ((int)FirstOfMonth.DayOfWeek + 6) % 7;
}

public sealed class DayViewModel
{
    public required CalendarDayDto Day { get; init; }
    public required DateOnly Today { get; init; }
    public required int RestDayLimit { get; init; }
    public required int RestDaysUsed { get; init; }

    public bool CanRecord => Day.Date <= Today;      // regla 12
    public bool CanToggleRestDay => Day.Date >= Today; // reglas 8 y 11
}

// Textos y estilos de presentación. El backend solo devuelve estados; aquí se deciden
// etiquetas y colores.
public static class Display
{
    public static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("es-MX");

    public static readonly DayOfWeek[] WeekMondayFirst =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
         DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday];

    public static string DayName(DayOfWeek day) => Culture.DateTimeFormat.GetDayName(day);
    public static string DayShort(DayOfWeek day) => Culture.DateTimeFormat.GetAbbreviatedDayName(day);

    public static string MonthTitle(int year, int month) =>
        Culture.TextInfo.ToTitleCase(new DateOnly(year, month, 1).ToString("MMMM yyyy", Culture));

    public static string LongDate(DateOnly date) =>
        Culture.TextInfo.ToTitleCase(date.ToString("dddd d 'de' MMMM 'de' yyyy", Culture));

    public static string Days(IEnumerable<DayOfWeek> days) =>
        string.Join(", ", WeekMondayFirst.Where(days.Contains).Select(DayShort));

    public static string Time(TimeOnly time) => time.ToString("HH:mm");

    public static (string Label, string Css) Of(DayStatus status) => status switch
    {
        DayStatus.Future => ("Próximo", "st-future"),
        DayStatus.RestDay => ("Descanso", "st-rest"),
        DayStatus.NoActivities => ("Sin actividades", "st-none"),
        DayStatus.Completed => ("Completado", "st-completed"),
        DayStatus.InProgress => ("En progreso", "st-progress"),
        DayStatus.Failed => ("Fallido", "st-failed"),
        DayStatus.Partial => ("Parcial", "st-partial"),
        _ => (status.ToString(), "")
    };

    public static (string Label, string Badge) Of(ActivityDayStatus status) => status switch
    {
        ActivityDayStatus.Scheduled => ("Programada", "text-bg-secondary"),
        ActivityDayStatus.Pending => ("Pendiente", "text-bg-warning"),
        ActivityDayStatus.Completed => ("Completada", "text-bg-success"),
        ActivityDayStatus.NotCompleted => ("No completada", "text-bg-danger"),
        ActivityDayStatus.Unrecorded => ("Sin registro", "text-bg-light border"),
        _ => (status.ToString(), "")
    };
}
