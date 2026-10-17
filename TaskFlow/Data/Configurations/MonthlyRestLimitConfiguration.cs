using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Entities;

namespace TaskFlow.Data.Configurations;

public class MonthlyRestLimitConfiguration : IEntityTypeConfiguration<MonthlyRestLimit>
{
    public void Configure(EntityTypeBuilder<MonthlyRestLimit> builder)
    {
        builder.ToTable("MonthlyRestLimits", t =>
        {
            t.HasCheckConstraint(
                "CK_MonthlyRestLimits_Month",
                "[Month] BETWEEN 1 AND 12");

            t.HasCheckConstraint(
                "CK_MonthlyRestLimits_MaxRestDays",
                "[MaxRestDays] BETWEEN 0 AND 10");
        });

        builder.HasKey(l => l.Id);

        // Un solo límite por mes.
        builder.HasIndex(l => new { l.Year, l.Month })
            .IsUnique();
    }
}
