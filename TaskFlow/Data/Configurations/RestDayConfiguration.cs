using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Entities;

namespace TaskFlow.Data.Configurations;

public class RestDayConfiguration : IEntityTypeConfiguration<RestDay>
{
    public void Configure(EntityTypeBuilder<RestDay> builder)
    {
        builder.ToTable("RestDays");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Notes).HasMaxLength(200);

        // Una fecha solo puede ser descanso una vez (regla 8). Además sirve para
        // contar los descansos de un mes con un rango de fechas (index seek).
        builder.HasIndex(r => r.Date)
            .IsUnique();
    }
}
