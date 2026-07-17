using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WriteFluency.Propositions;

namespace WriteFluency.Data;

public sealed class CatalogAccessCounterEfConfiguration : IEntityTypeConfiguration<CatalogAccessCounter>
{
    public void Configure(EntityTypeBuilder<CatalogAccessCounter> builder)
    {
        builder.ToTable("CatalogAccessCounters");

        builder.Property(counter => counter.SubjectType)
            .IsRequired()
            .HasMaxLength(32);
        builder.Property(counter => counter.SubjectKey)
            .IsRequired()
            .HasMaxLength(128);
        builder.Property(counter => counter.AnonymousClientIpAddress)
            .HasMaxLength(45);
        builder.Property(counter => counter.Feature)
            .IsRequired()
            .HasMaxLength(80);

        builder.HasIndex(counter => new
            {
                counter.SubjectType,
                counter.SubjectKey,
                counter.Feature
            })
            .IsUnique();
    }
}
