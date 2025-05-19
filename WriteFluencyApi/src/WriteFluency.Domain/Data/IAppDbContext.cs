using Microsoft.EntityFrameworkCore;
using WriteFluency.Propositions;

namespace WriteFluency.Data;

public interface IAppDbContext
{
    public DbSet<Subject> Subjects { get; set; }
    public DbSet<Complexity> Complexities { get; set; }
    public DbSet<Proposition> Propositions { get; set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
