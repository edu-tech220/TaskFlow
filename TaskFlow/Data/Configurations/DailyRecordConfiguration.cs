using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Entities;

namespace TaskFlow.Data.Configurations;

public class DailyRecordConfiguration : IEntityTypeConfiguration<DailyRecord>
{
    public void Configure(EntityTypeBuilder<DailyRecord> builder)
    {
        builder.ToTable("DailyRecords", t =>
        {
            // Solo los valores válidos del enum (1 = Completed, 2 = NotCompleted).
            t.HasCheckConstraint(
                "CK_DailyRecords_Status",
                "[Status] IN (1, 2)");

            // Regla 14 reforzada en la BD: ActualEndTime solo con Completed.
            t.HasCheckConstraint(
                "CK_DailyRecords_ActualEndTime_Only_When_Completed",
                "[ActualEndTime] IS NULL OR [Status] = 1");
        });

        builder.HasKey(r => r.Id);

        builder.Property(r => r.ActualEndTime).HasPrecision(0);

        builder.Property(r => r.Notes).HasMaxLength(500);

        // Un registro por actividad por día. Es lo que permite el upsert de la regla 13
        // y también sirve como índice de la FK ActivityId.
        builder.HasIndex(r => new { r.ActivityId, r.Date })
            .IsUnique();

        // Índice por fecha: el calendario y GET /daily-records?from=&to= filtran por Date.
        builder.HasIndex(r => r.Date);

        // La relación con Activity (Restrict) está configurada en ActivityConfiguration.
    }
}
