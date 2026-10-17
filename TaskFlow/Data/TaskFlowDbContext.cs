using Microsoft.EntityFrameworkCore;
using TaskFlow.Entities;

namespace TaskFlow.Data;

// El DbContext es la "unidad de trabajo": representa una sesión con la BD.
// - Cada DbSet<T> es una tabla consultable con LINQ (EF lo traduce a SQL).
// - Lleva un ChangeTracker: recuerda qué entidades cargaste, agregaste o modificaste,
//   y al llamar SaveChangesAsync genera los INSERT/UPDATE/DELETE en UNA transacción.
// - Se registra como Scoped: una instancia por request HTTP.
//
// Constructor primario (C# 12): recibe las opciones (cadena de conexión, proveedor) que
// arma Program.cs y el TimeProvider para estampar fechas de forma testeable.
public class TaskFlowDbContext(
    DbContextOptions<TaskFlowDbContext> options,
    TimeProvider timeProvider) : DbContext(options)
{
    // "=> Set<T>()" en lugar de { get; set; }: evita el warning de nullable y
    // deja claro que EF administra el DbSet.
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<ActivitySchedule> ActivitySchedules => Set<ActivitySchedule>();
    public DbSet<ActivityScheduleDay> ActivityScheduleDays => Set<ActivityScheduleDay>();
    public DbSet<DailyRecord> DailyRecords => Set<DailyRecord>();
    public DbSet<RestDay> RestDays => Set<RestDay>();
    public DbSet<MonthlyRestLimit> MonthlyRestLimits => Set<MonthlyRestLimit>();

    // Se ejecuta UNA vez por proceso para construir el modelo (luego se cachea).
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Busca por reflexión todas las clases IEntityTypeConfiguration<T> de este
        // ensamblado (Data/Configurations) y llama a su Configure.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TaskFlowDbContext).Assembly);
    }

    // Todas las sobrecargas de SaveChanges terminan en estas dos; con eso basta para
    // que cualquier guardado pase por StampAuditFields.
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampAuditFields();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        StampAuditFields();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    // Recorre las entidades pendientes de guardar y asigna CreatedAt/UpdatedAt en UTC.
    // Usar TimeProvider en vez de DateTime.UtcNow permite fijar el reloj en las pruebas.
    private void StampAuditFields()
    {
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;

        foreach (var entry in ChangeTracker.Entries<IHasCreatedAt>())
        {
            if (entry.State == EntityState.Added)
                entry.Entity.CreatedAt = utcNow;
        }

        foreach (var entry in ChangeTracker.Entries<IHasUpdatedAt>())
        {
            if (entry.State == EntityState.Modified)
                entry.Entity.UpdatedAt = utcNow;
        }
    }
}
