using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WriteFluency.Propositions;

namespace WriteFluency.Data;

public class PropositionGenerationLogEfConfiguration : IEntityTypeConfiguration<PropositionGenerationLog>
{
    public void Configure(EntityTypeBuilder<PropositionGenerationLog> builder)
    {
        builder.HasOne(x => x.Complexity).WithMany().OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Subject).WithMany().OnDelete(DeleteBehavior.Restrict);
        
        // Index for finding combinations with least records
        builder.HasIndex(x => new { x.SubjectId, x.ComplexityId, x.Success });
        
        // Index for finding attempted dates for a specific combination
        builder.HasIndex(x => new { x.SubjectId, x.ComplexityId, x.GenerationDate });
        
        // Index for finding oldest log date for a combination
        builder.HasIndex(x => new { x.SubjectId, x.ComplexityId, x.CreatedAt });
    }
}
