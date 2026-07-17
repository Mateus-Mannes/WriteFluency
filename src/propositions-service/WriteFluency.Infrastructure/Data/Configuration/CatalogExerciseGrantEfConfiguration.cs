using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WriteFluency.Propositions;

namespace WriteFluency.Data;

public sealed class CatalogExerciseGrantEfConfiguration : IEntityTypeConfiguration<CatalogExerciseGrant>
{
    public void Configure(EntityTypeBuilder<CatalogExerciseGrant> builder)
    {
        builder.ToTable("CatalogExerciseGrants");

        builder.Property(grant => grant.SubjectType)
            .IsRequired()
            .HasMaxLength(32);
        builder.Property(grant => grant.SubjectKey)
            .IsRequired()
            .HasMaxLength(128);
        builder.Property(grant => grant.AnonymousClientIpAddress)
            .HasMaxLength(45);
        builder.Property(grant => grant.Source)
            .IsRequired()
            .HasMaxLength(80);

        builder.HasIndex(grant => new
            {
                grant.SubjectType,
                grant.SubjectKey,
                grant.PropositionId
            })
            .IsUnique();
    }
}
