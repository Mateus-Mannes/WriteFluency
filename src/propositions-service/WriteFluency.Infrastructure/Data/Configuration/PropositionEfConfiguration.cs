using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpgsqlTypes;
using WriteFluency.Propositions;

namespace WriteFluency.Data;

public class PropositionEfConfiguration : IEntityTypeConfiguration<Proposition>
{
    private readonly bool _usePostgresFullTextSearch;

    public PropositionEfConfiguration(bool usePostgresFullTextSearch = false)
    {
        _usePostgresFullTextSearch = usePostgresFullTextSearch;
    }

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

        if (_usePostgresFullTextSearch)
        {
            builder.Property<NpgsqlTsVector>("SearchVector")
                .HasColumnType("tsvector")
                .HasComputedColumnSql(
                    "setweight(to_tsvector('english', coalesce(\"Title\", '')), 'A') || setweight(to_tsvector('english', coalesce(\"Text\", '')), 'B')",
                    stored: true);

            builder.HasIndex("SearchVector")
                .HasMethod("GIN");

            builder.HasIndex(x => x.Title)
                .HasMethod("GIN")
                .HasOperators("gin_trgm_ops");

            builder.HasIndex(x => x.Text)
                .HasMethod("GIN")
                .HasOperators("gin_trgm_ops");
        }
        
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
