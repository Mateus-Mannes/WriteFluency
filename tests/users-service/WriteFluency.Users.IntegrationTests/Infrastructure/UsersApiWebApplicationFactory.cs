using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;
using WriteFluency.Users.WebApi.Authentication;
using WriteFluency.Users.WebApi.Data;
using WriteFluency.Users.WebApi.Email;

namespace WriteFluency.Users.IntegrationTests.Infrastructure;

public sealed class UsersApiWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _postgresConnectionString;
    private readonly string _redisConnectionString;
    private readonly TestEmailSender _testEmailSender;

    public UsersApiWebApplicationFactory(
        string postgresConnectionString,
        string redisConnectionString,
        TestEmailSender testEmailSender)
    {
        _postgresConnectionString = postgresConnectionString;
        _redisConnectionString = redisConnectionString;
        _testEmailSender = testEmailSender;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Authentication:Google:ClientId", "test-google-client-id");
        builder.UseSetting("Authentication:Google:ClientSecret", "test-google-client-secret");
        builder.UseSetting("Authentication:Microsoft:ClientId", "test-microsoft-client-id");
        builder.UseSetting("Authentication:Microsoft:ClientSecret", "test-microsoft-client-secret");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:wf-users-postgresdb"] = _postgresConnectionString,
                ["ConnectionStrings:wf-infra-redis"] = _redisConnectionString,
                ["Smtp:Host"] = "test-smtp",
                ["Smtp:Port"] = "2525",
                ["Smtp:FromEmail"] = "noreply@writefluency.test",
                ["Smtp:FromName"] = "WriteFluency Test",
                ["PasswordlessOtp:CodeLength"] = "6",
                ["PasswordlessOtp:TtlMinutes"] = "10",
                ["PasswordlessOtp:MaxVerifyAttempts"] = "5",
                ["PasswordlessOtp:MaxRequestsPerWindowPerEmail"] = "3",
                ["PasswordlessOtp:MaxRequestsPerWindowPerIp"] = "20",
                ["PasswordlessOtp:RequestWindowMinutes"] = "15",
                ["PasswordlessOtp:MinimumSecondsBetweenRequestsPerEmail"] = "1",
                ["Authentication:ExternalRedirect:AllowedReturnUrls:0"] = "/users/swagger/index.html",
                ["Authentication:ExternalRedirect:AllowedReturnUrls:1"] = "http://localhost:4200/auth/callback"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IConnectionMultiplexer>();
            services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(_redisConnectionString));

            services.RemoveAll<DbContextOptions<UsersDbContext>>();
            services.AddDbContext<UsersDbContext>(options => options.UseNpgsql(_postgresConnectionString));

            services.RemoveAll<IAppEmailSender>();
            services.AddSingleton<IAppEmailSender>(_testEmailSender);

            services.RemoveAll<IExternalLoginInfoResolver>();
            services.AddScoped<IExternalLoginInfoResolver, TestingExternalLoginInfoResolver>();
        });
    }

    public async Task ResetStateAsync()
    {
        using var scope = Services.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
    }
}
