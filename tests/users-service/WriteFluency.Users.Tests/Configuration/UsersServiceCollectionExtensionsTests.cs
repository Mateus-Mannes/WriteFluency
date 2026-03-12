using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using WriteFluency.Users.WebApi.Configuration;
using WriteFluency.Users.WebApi.Data;

namespace WriteFluency.Users.Tests.Configuration;

public class UsersServiceCollectionExtensionsTests
{
    [Fact]
    public void AddUsersPersistence_ShouldRegisterIdentityAndDbContext()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var configuration = BuildConfiguration();

        services.AddUsersPersistence(configuration);

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        scope.ServiceProvider.GetRequiredService<UsersDbContext>().ShouldNotBeNull();
        scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().ShouldNotBeNull();
    }

    [Fact]
    public void AddUsersPersistence_ShouldUseNpgsqlProviderAndConfiguredConnectionString()
    {
        const string connectionString = "Host=localhost;Port=5432;Database=wf-users-postgresdb;Username=postgres;Password=postgres";

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddUsersPersistence(BuildConfiguration(connectionString));

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<UsersDbContext>();

        dbContext.Database.ProviderName.ShouldBe("Npgsql.EntityFrameworkCore.PostgreSQL");
        dbContext.Database.GetConnectionString().ShouldBe(connectionString);
    }

    private static IConfiguration BuildConfiguration(string? connectionString = null)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:wf-users-postgresdb"] = connectionString
                    ?? "Host=localhost;Port=5432;Database=wf-users-postgresdb;Username=postgres;Password=postgres"
            })
            .Build();
    }
}
