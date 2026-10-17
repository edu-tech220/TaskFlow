using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Entities;

namespace TaskFlow.Data.Configurations;

public class ActivityScheduleConfiguration : IEntityTypeConfiguration<ActivitySchedule>
{
    public void Configure(EntityTypeBuilder<ActivitySchedule> builder)
    {
        // Los CHECK constraints se declaran dentro de ToTable. El SQL va tal cual a la BD,
        // por eso se usan nombres de columna con corchetes.
        builder.ToTable("ActivitySchedules", t =>
        {
            // Sin cruzar medianoche: la hora final siempre después de la inicial (regla 7).
            t.HasCheckConstraint(
                "CK_ActivitySchedules_EndTime_After_StartTime",
                "[EndTime] > [StartTime]");

            // Rango de vigencia válido. En SQL un CHECK con NULL no falla, así que el
            // "IS NULL OR" solo deja explícita la intención.
            t.HasCheckConstraint(
                "CK_ActivitySchedules_EffectiveTo_After_EffectiveFrom",
                "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
        });

        builder.HasKey(s => s.Id);

        // time(0): precisión de segundos, sin fracciones. Una hora de agenda no necesita
        // los 100 ns de time(7), que es el default.
        builder.Property(s => s.StartTime).HasPrecision(0);
        builder.Property(s => s.EndTime).HasPrecision(0);

        // Índice único: una actividad no puede tener dos versiones que empiecen el mismo día.
        // Como empieza por ActivityId, también sirve de índice para la FK (EF no crea otro)
        // y para buscar "la versión vigente de la actividad X en la fecha D".
        // El NO traslape entre versiones no se puede expresar con un índice: lo valida el servicio.
        builder.HasIndex(s => new { s.ActivityId, s.EffectiveFrom })
            .IsUnique();

        // Relación 1:N ActivitySchedule -> ActivityScheduleDay, con cascade: si se borra
        // una versión, se van sus días.
        builder.HasMany(s => s.Days)
            .WithOne(d => d.ActivitySchedule)
            .HasForeignKey(d => d.ActivityScheduleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
