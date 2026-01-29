using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WriteFluency.Propositions;

namespace WriteFluency.Data;

public class PropositionEfConfiguration : IEntityTypeConfiguration<Proposition>
{
    public void Configure(EntityTypeBuilder<Proposition> builder)
    {
        builder.HasOne(x => x.Complexity).WithMany().OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Subject).WithMany().OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PropositionGenerationLog)
            .WithMany(x => x.Propositions)
            .HasForeignKey(x => x.PropositionGenerationLogId)
            .OnDelete(DeleteBehavior.SetNull);
        
        // Query filter for soft delete
        builder.HasQueryFilter(x => !x.IsDeleted);
        
        builder.HasIndex(x => x.PublishedOn);
        builder.HasIndex(x => x.IsDeleted);
        builder.HasIndex(x => new { x.SubjectId, x.ComplexityId, x.PublishedOn });
        builder.HasIndex(x => new { x.SubjectId, x.IsDeleted });
        
        builder.OwnsOne(x => x.NewsInfo, news =>
        {
            news.WithOwner();
            news.Property(x => x.Id).HasColumnName("NewsId");
            news.HasIndex(x => x.Id); // Index on NewsId for duplicate checks
            news.Property(x => x.Title).HasColumnName("NewsTitle");
            news.Property(x => x.Description).HasColumnName("NewsDescription");
            news.Property(x => x.Url).HasColumnName("NewsUrl");
            news.Property(x => x.ImageUrl).HasColumnName("NewsImageUrl");
            news.Property(x => x.Text).HasColumnName("NewsText");
            news.Property(x => x.TextLength).HasColumnName("NewsTextLength");
        });
    }
}
