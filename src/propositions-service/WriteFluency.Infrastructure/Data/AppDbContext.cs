using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WriteFluency.Domain.App;
using WriteFluency.Propositions;

namespace WriteFluency.Data;

public class AppDbContext : IdentityDbContext<IdentityUser>, IAppDbContext
{
    public DbSet<Subject> Subjects { get; set; }
    public DbSet<Complexity> Complexities { get; set; }
    public DbSet<Proposition> Propositions { get; set; }
    public DbSet<AppSettings> AppSettings { get; set; }
    public AppDbContext(DbContextOptions opts) : base(opts) { }
    
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfiguration(new PropositionEfConfiguration());
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);

        optionsBuilder.UseAsyncSeeding(async (context, _, cancellationToken) =>
        {
            await SeedEnumClassAsync<Subject, SubjectEnum>(
                context.Set<Subject>(), x => new Subject { Id = x, Description = x.ToString() }, cancellationToken);

            await SeedEnumClassAsync<Complexity, ComplexityEnum>(
                context.Set<Complexity>(), x => new Complexity { Id = x, Description = x.ToString() }, cancellationToken);

            await SeedAppSettingsAsync(context.Set<AppSettings>(), cancellationToken);
        });
    }

    private async Task SeedEnumClassAsync<TModel, TEnum>(DbSet<TModel> dbSet, Func<TEnum, TModel> createFunc, CancellationToken cancellationToken = default)
        where TEnum : Enum
        where TModel : class
    {
        foreach (TEnum enumValue in Enum.GetValues(typeof(TEnum)))
        {
            if (!await dbSet.AnyAsync(x => EF.Property<TEnum>(x, "Id").Equals(enumValue), cancellationToken))
            {
                await dbSet.AddAsync(createFunc(enumValue), cancellationToken);
            }
        }

        await SaveChangesAsync(cancellationToken);
    }

    private async Task SeedAppSettingsAsync(DbSet<AppSettings> dbSet, CancellationToken cancellationToken = default)
    {
        foreach (var (key, value) in GetDefaultAppSettings())
        {
            if (!await dbSet.AnyAsync(x => x.Key == key, cancellationToken))
            {
                await dbSet.AddAsync(new AppSettings
                {
                    Key = key,
                    Value = value
                }, cancellationToken);
            }
        }

        await SaveChangesAsync(cancellationToken);
    }

    public static IEnumerable<(string Key, string Value)> GetDefaultAppSettings()
    {
        return typeof(AppSettings)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.FieldType == typeof((string, string)))
            .Select(f => ((string, string))f.GetValue(null)!);
    }

}
