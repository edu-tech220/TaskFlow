namespace TaskFlow.Entities;

// Límite de descansos para un mes específico. Si no existe fila para (Year, Month),
// se usa el valor por defecto de appsettings.json (RestDays:DefaultMonthlyLimit).
// Solo se guardan los meses en que el usuario cambió el límite.
public class MonthlyRestLimit
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }        // CHECK 1-12
    public int MaxRestDays { get; set; }  // CHECK 0-10
}
