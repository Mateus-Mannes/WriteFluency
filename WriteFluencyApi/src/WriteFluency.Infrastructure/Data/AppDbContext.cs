using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WriteFluency.Propositions;

namespace WriteFluency.Data;

public class AppDbContext : IdentityDbContext<IdentityUser>, IAppDbContext
{
    public DbSet<Subject> Subjects { get; set; }
    public DbSet<Complexity> Complexities { get; set; }
    public DbSet<Proposition> Propositions { get; set; }
    public AppDbContext(DbContextOptions opts) : base(opts) { }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfiguration(new PropositionEfConfiguration());
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);

        optionsBuilder.UseSeeding((context, _) =>
        {
            Task.Run(async () =>
            {
                await SeedEnumClassAsync<Subject, SubjectEnum>(
                    context.Set<Subject>(), x => new Subject { Id = x, Description = x.ToString() });

                await SeedEnumClassAsync<Complexity, ComplexityEnum>(
                    context.Set<Complexity>(), x => new Complexity { Id = x, Description = x.ToString() });

            }).GetAwaiter().GetResult();
        });
    }

    private async Task SeedEnumClassAsync<TModel, TEnum>(DbSet<TModel> dbSet, Func<TEnum, TModel> createFunc)
        where TEnum : Enum
        where TModel : class
    {
        foreach (TEnum enumValue in Enum.GetValues(typeof(TEnum)))
        {
            if (!await dbSet.AnyAsync(x => EF.Property<TEnum>(x, "Id").Equals(enumValue)))
            {
                await dbSet.AddAsync(createFunc(enumValue));
            }
        }

        await SaveChangesAsync();
    }
}
