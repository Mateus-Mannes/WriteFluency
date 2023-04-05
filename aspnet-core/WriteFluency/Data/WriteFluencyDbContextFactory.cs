using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace WriteFluency.Data;

public class WriteFluencyDbContextFactory : IDesignTimeDbContextFactory<WriteFluencyDbContext>
{
    public WriteFluencyDbContext CreateDbContext(string[] args)
    {
        // https://www.npgsql.org/efcore/release-notes/6.0.html#opting-out-of-the-new-timestamp-mapping-logic
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        var configuration = BuildConfiguration();

        var builder = new DbContextOptionsBuilder<WriteFluencyDbContext>()
            .UseNpgsql(configuration.GetConnectionString("Default"));

        return new WriteFluencyDbContext(builder.Options);
    }

    private static IConfigurationRoot BuildConfiguration()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false);

        return builder.Build();
    }
}
