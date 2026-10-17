namespace TaskFlow.Entities;

// Entidad = clase normal (POCO). EF Core la mapea a una tabla; las reglas de mapeo
// (longitudes, índices, relaciones) están en Data/Configurations/ActivityConfiguration.cs,
// no aquí, para que la entidad quede limpia.
//
// No hay columna IsActive: una actividad está "vigente" en una fecha si alguna de sus
// versiones de horario (Schedules) cubre esa fecha. Se calcula, no se guarda.
public class Activity : IHasCreatedAt, IHasUpdatedAt
{
    // Convención: propiedad "Id" de tipo int => PK con IDENTITY(1,1).
    public int Id { get; set; }

    // string NO nullable (Nullable está habilitado en el csproj) => columna NOT NULL.
    // Se inicializa en string.Empty para que el compilador no avise de null.
    public string Name { get; set; } = string.Empty;

    // string? => columna NULL.
    public string? Description { get; set; }

    // DateTime => datetime2. Siempre UTC; lo llena TaskFlowDbContext al insertar.
    public DateTime CreatedAt { get; set; }

    // Nullable: null mientras la actividad nunca se haya modificado.
    public DateTime? UpdatedAt { get; set; }

    // Propiedades de NAVEGACIÓN (no son columnas). Le dicen a EF que existe una relación
    // 1:N; con ellas puedes hacer .Include(a => a.Schedules) o proyectar en LINQ y EF
    // genera los JOIN. Se inicializan para no toparte con null al hacer .Add(...).
    public ICollection<ActivitySchedule> Schedules { get; set; } = new List<ActivitySchedule>();
    public ICollection<DailyRecord> DailyRecords { get; set; } = new List<DailyRecord>();
}
