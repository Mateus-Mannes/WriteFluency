using Microsoft.EntityFrameworkCore;
using WriteFluency.Domain.App;
using WriteFluency.Propositions;
using WriteFluency.TextComparisons;

namespace WriteFluency.Data;

public interface IAppDbContext
{
    public DbSet<Subject> Subjects { get; set; }
    public DbSet<Complexity> Complexities { get; set; }
    public DbSet<Proposition> Propositions { get; set; }
    public DbSet<PropositionGenerationLog> PropositionGenerationLogs { get; set; }
    public DbSet<AppSettings> AppSettings { get; set; }
    public DbSet<AiUsageCounter> AiUsageCounters { get; set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
