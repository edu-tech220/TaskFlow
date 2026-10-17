using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Entities;

namespace TaskFlow.Data.Configurations;

public class ActivityScheduleDayConfiguration : IEntityTypeConfiguration<ActivityScheduleDay>
{
    public void Configure(EntityTypeBuilder<ActivityScheduleDay> builder)
    {
        builder.ToTable("ActivityScheduleDays", t =>
        {
            // Mismos valores que System.DayOfWeek: 0 = domingo ... 6 = sábado.
            t.HasCheckConstraint(
                "CK_ActivityScheduleDays_DayOfWeek",
                "[DayOfWeek] BETWEEN 0 AND 6");
        });

        // PK compuesta. Obligatoria aquí: la entidad no tiene "Id" y EF no la deduciría.
        // También impide repetir el mismo día en una versión.
        // La relación con ActivitySchedule ya está configurada del lado del padre.
        builder.HasKey(d => new { d.ActivityScheduleId, d.DayOfWeek });
    }
}
