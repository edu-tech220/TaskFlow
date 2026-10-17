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

        // Una fecha solo puede ser descanso una vez por usuario (regla 8). Además sirve para
        // contar los descansos de un mes de ese usuario con un rango de fechas (index seek).
        builder.HasIndex(r => new { r.UserId, r.Date })
            .IsUnique();

        // FK a Users sin navegación en la entidad: basta con declararla aquí.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
