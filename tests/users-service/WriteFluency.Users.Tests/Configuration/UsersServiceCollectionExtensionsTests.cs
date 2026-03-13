using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using WriteFluency.Users.WebApi.Configuration;
using WriteFluency.Users.WebApi.Data;
using WriteFluency.Users.WebApi.Options;

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
        scope.ServiceProvider.GetRequiredService<SignInManager<ApplicationUser>>().ShouldNotBeNull();
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

    [Fact]
    public async Task AddUsersPersistence_ShouldConfigureIdentityAndCookieOptions()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddUsersPersistence(BuildConfiguration());

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var identityOptions = scope.ServiceProvider.GetRequiredService<IOptions<IdentityOptions>>().Value;
        identityOptions.SignIn.RequireConfirmedEmail.ShouldBeTrue();

        var schemeProvider = scope.ServiceProvider.GetRequiredService<IAuthenticationSchemeProvider>();
        var identityApplicationScheme = await schemeProvider.GetSchemeAsync(IdentityConstants.ApplicationScheme);
        identityApplicationScheme.ShouldNotBeNull();
        identityApplicationScheme.Name.ShouldBe(IdentityConstants.ApplicationScheme);

        var smtpOptions = scope.ServiceProvider.GetRequiredService<IOptions<SmtpOptions>>().Value;
        smtpOptions.Host.ShouldBe("smtp.local");
        smtpOptions.Port.ShouldBe(2525);
    }

    private static IConfiguration BuildConfiguration(string? connectionString = null)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:wf-users-postgresdb"] = connectionString
                    ?? "Host=localhost;Port=5432;Database=wf-users-postgresdb;Username=postgres;Password=postgres",
                ["ConnectionStrings:wf-infra-redis"] = "localhost:6379",
                ["Smtp:Host"] = "smtp.local",
                ["Smtp:Port"] = "2525",
                ["Smtp:FromEmail"] = "noreply@writefluency.local",
                ["Smtp:FromName"] = "WriteFluency",
                ["PasswordlessOtp:CodeLength"] = "6",
                ["PasswordlessOtp:TtlMinutes"] = "10",
                ["PasswordlessOtp:MaxVerifyAttempts"] = "5",
                ["PasswordlessOtp:MaxRequestsPerWindowPerEmail"] = "3",
                ["PasswordlessOtp:MaxRequestsPerWindowPerIp"] = "20",
                ["PasswordlessOtp:RequestWindowMinutes"] = "15",
                ["PasswordlessOtp:MinimumSecondsBetweenRequestsPerEmail"] = "30"
            })
            .Build();
    }
}
