using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using WriteFluency.Users.WebApi.Authentication;
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

        var cookieOptionsMonitor = scope.ServiceProvider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>();
        var cookieOptions = cookieOptionsMonitor.Get(IdentityConstants.ApplicationScheme);
        cookieOptions.Cookie.SameSite.ShouldBe(SameSiteMode.None);
        cookieOptions.Cookie.Name.ShouldBe(".AspNetCore.Identity.Application");
        cookieOptions.Cookie.Domain.ShouldBeNull();
        cookieOptions.Cookie.SecurePolicy.ShouldBe(CookieSecurePolicy.Always);

        var smtpOptions = scope.ServiceProvider.GetRequiredService<IOptions<SmtpOptions>>().Value;
        smtpOptions.Host.ShouldBe("smtp.local");
        smtpOptions.Port.ShouldBe(2525);
        smtpOptions.ReplyToEmail.ShouldBe("support@writefluency.local");
        smtpOptions.EnvelopeFrom.ShouldBe("bounce@writefluency.local");
        smtpOptions.MessageIdDomain.ShouldBe("writefluency.local");

        var externalAuthOptions = scope.ServiceProvider.GetRequiredService<IOptions<ExternalAuthenticationOptions>>().Value;
        externalAuthOptions.ConfirmationRedirectUrl.ShouldBe("https://writefluency.local/auth/confirm-email");

        var loginLocationOptions = scope.ServiceProvider.GetRequiredService<IOptions<LoginLocationOptions>>().Value;
        loginLocationOptions.Enabled.ShouldBeTrue();
        loginLocationOptions.GeoLite2CityBlobUri.ShouldBe("https://wfusersdpprod01.blob.core.windows.net/geolite/GeoLite2-City.mmdb");
        loginLocationOptions.BlobMetadataRefreshMinutes.ShouldBe(60);
        loginLocationOptions.GeoLite2CityDbPath.ShouldBe("/tmp/GeoLite2-City.mmdb");
    }

    [Fact]
    public void AddUsersPersistence_WhenProduction_ShouldUseSameSiteLax()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddUsersPersistence(BuildConfiguration(), isProduction: true);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var cookieOptionsMonitor = scope.ServiceProvider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>();
        var cookieOptions = cookieOptionsMonitor.Get(IdentityConstants.ApplicationScheme);

        cookieOptions.Cookie.SameSite.ShouldBe(SameSiteMode.Lax);
        cookieOptions.Cookie.Name.ShouldBe(".AspNetCore.Identity.Application");
        cookieOptions.Cookie.Domain.ShouldBe(".writefluency.com");
        cookieOptions.Cookie.SecurePolicy.ShouldBe(CookieSecurePolicy.Always);
    }

    [Fact]
    public async Task AddUsersPersistence_ShouldRegisterExternalProviders_WhenCredentialsAreConfigured()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddUsersPersistence(BuildConfiguration(enableExternalProviders: true));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var schemeProvider = scope.ServiceProvider.GetRequiredService<IAuthenticationSchemeProvider>();
        (await schemeProvider.GetSchemeAsync(GoogleDefaults.AuthenticationScheme)).ShouldNotBeNull();
        (await schemeProvider.GetSchemeAsync(MicrosoftAccountDefaults.AuthenticationScheme)).ShouldNotBeNull();
    }

    [Fact]
    public void AddUsersPersistence_ShouldRegisterLoginLocationServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddUsersPersistence(BuildConfiguration());

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<ILoginGeoLookupService>().ShouldNotBeNull();
        scope.ServiceProvider.GetRequiredService<ILoginActivityRecorder>().ShouldNotBeNull();
    }

    private static IConfiguration BuildConfiguration(
        string? connectionString = null,
        bool enableExternalProviders = true)
    {
        var config = new Dictionary<string, string?>
        {
            ["ConnectionStrings:wf-users-postgresdb"] = connectionString
                ?? "Host=localhost;Port=5432;Database=wf-users-postgresdb;Username=postgres;Password=postgres",
            ["ConnectionStrings:wf-infra-redis"] = "localhost:6379",
            ["Smtp:Host"] = "smtp.local",
            ["Smtp:Port"] = "2525",
            ["Smtp:FromEmail"] = "noreply@writefluency.local",
            ["Smtp:FromName"] = "WriteFluency",
            ["Smtp:ReplyToEmail"] = "support@writefluency.local",
            ["Smtp:EnvelopeFrom"] = "bounce@writefluency.local",
            ["Smtp:MessageIdDomain"] = "writefluency.local",
            ["PasswordlessOtp:CodeLength"] = "6",
            ["PasswordlessOtp:TtlMinutes"] = "10",
            ["PasswordlessOtp:MaxVerifyAttempts"] = "5",
            ["PasswordlessOtp:MaxRequestsPerWindowPerEmail"] = "3",
            ["PasswordlessOtp:MaxRequestsPerWindowPerIp"] = "20",
            ["PasswordlessOtp:RequestWindowMinutes"] = "15",
            ["PasswordlessOtp:MinimumSecondsBetweenRequestsPerEmail"] = "30",
            ["LoginLocation:Enabled"] = "true",
            ["LoginLocation:GeoLite2CityBlobUri"] = "https://wfusersdpprod01.blob.core.windows.net/geolite/GeoLite2-City.mmdb",
            ["LoginLocation:BlobMetadataRefreshMinutes"] = "60",
            ["LoginLocation:GeoLite2CityDbPath"] = "/tmp/GeoLite2-City.mmdb",
            ["SharedAuthCookie:Scheme"] = "Identity.Application",
            ["SharedAuthCookie:CookieName"] = ".AspNetCore.Identity.Application",
            ["SharedAuthCookie:CookieDomain"] = ".writefluency.com",
            ["SharedDataProtection:ApplicationName"] = "WriteFluency.SharedAuth",
            ["Authentication:ConfirmationRedirectUrl"] = "https://writefluency.local/auth/confirm-email",
            ["Authentication:ExternalRedirect:AllowedReturnUrls:0"] = "/users/swagger/index.html"
        };

        if (enableExternalProviders)
        {
            config["Authentication:Google:ClientId"] = "google-client-id";
            config["Authentication:Google:ClientSecret"] = "google-secret";
            config["Authentication:Microsoft:ClientId"] = "microsoft-client-id";
            config["Authentication:Microsoft:ClientSecret"] = "microsoft-secret";
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(config)
            .Build();
    }
}
