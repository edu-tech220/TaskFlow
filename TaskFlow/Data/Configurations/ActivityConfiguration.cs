using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Entities;

namespace TaskFlow.Data.Configurations;

// IEntityTypeConfiguration<T>: una clase por entidad con su mapeo Fluent API.
// TaskFlowDbContext las descubre todas con ApplyConfigurationsFromAssembly, así que
// basta con crear la clase; no hay que registrarla en ningún lado.
public class ActivityConfiguration : IEntityTypeConfiguration<Activity>
{
    public void Configure(EntityTypeBuilder<Activity> builder)
    {
        // Nombre explícito de la tabla (por convención sería el nombre del DbSet).
        builder.ToTable("Activities");

        // PK. Por convención ya lo sería; explícito por claridad.
        builder.HasKey(a => a.Id);

        // IsRequired + HasMaxLength(100) => nvarchar(100) NOT NULL.
        // Sin HasMaxLength, EF usaría nvarchar(max).
        builder.Property(a => a.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.Description)
            .HasMaxLength(500);

        // Relación 1:N Activity -> ActivitySchedule.
        // Cascade: borrar una actividad borra sus versiones de horario (y estas, sus días).
        builder.HasMany(a => a.Schedules)
            .WithOne(s => s.Activity)
            .HasForeignKey(s => s.ActivityId)
            .OnDelete(DeleteBehavior.Cascade);

        // Relación 1:N Activity -> DailyRecord.
        // Restrict: SQL Server rechaza el DELETE de una actividad que tenga registros
        // (FK con ON DELETE NO ACTION). Es la red de seguridad de la regla 6; el servicio
        // lo valida antes para devolver un 409 claro.
        builder.HasMany(a => a.DailyRecords)
            .WithOne(r => r.Activity)
            .HasForeignKey(r => r.ActivityId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
