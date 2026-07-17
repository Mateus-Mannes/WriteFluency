using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WriteFluency.TextComparisons;

namespace WriteFluency.Data;

public sealed class AiUsageCounterEfConfiguration : IEntityTypeConfiguration<AiUsageCounter>
{
    public void Configure(EntityTypeBuilder<AiUsageCounter> builder)
    {
        builder.ToTable("AiUsageCounters");

        builder.Property(counter => counter.UserId)
            .IsRequired()
            .HasMaxLength(128);
        builder.Property(counter => counter.AnonymousClientIpAddress)
            .HasMaxLength(45);
        builder.Property(counter => counter.Feature)
            .IsRequired()
            .HasMaxLength(80);
        builder.Property(counter => counter.PeriodKind)
            .IsRequired()
            .HasMaxLength(16);
        builder.Property(counter => counter.PeriodKey)
            .IsRequired()
            .HasMaxLength(16);
        builder.Property(counter => counter.EstimatedCostUsd)
            .HasPrecision(18, 6);

        builder.HasIndex(counter => new
            {
                counter.UserId,
                counter.Feature,
                counter.PeriodKind,
                counter.PeriodKey
            })
            .IsUnique();
    }
}
