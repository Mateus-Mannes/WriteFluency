using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WriteFluency.Users.WebApi.Authentication;
using WriteFluency.Users.WebApi.Data;
using WriteFluency.Users.WebApi.Email;
using WriteFluency.Users.WebApi.Options;

namespace WriteFluency.Users.WebApi.Configuration;

public static class UsersServiceCollectionExtensions
{
    private const string UsersConnectionStringName = "wf-users-postgresdb";
    private const string RedisConnectionStringName = "wf-infra-redis";

    public static IServiceCollection AddUsersPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(UsersConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException($"Connection string '{UsersConnectionStringName}' was not found.");
        }

        var redisConnectionString = configuration.GetConnectionString(RedisConnectionStringName);
        if (string.IsNullOrWhiteSpace(redisConnectionString))
        {
            throw new InvalidOperationException($"Connection string '{RedisConnectionStringName}' was not found.");
        }

        services.AddDbContext<UsersDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddOptions<SmtpOptions>()
            .Bind(configuration.GetSection(SmtpOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Host), "SMTP host is required")
            .Validate(options => options.Port > 0, "SMTP port must be greater than zero")
            .Validate(options => !string.IsNullOrWhiteSpace(options.FromEmail), "SMTP from email is required")
            .ValidateOnStart();

        services.AddOptions<PasswordlessOtpOptions>()
            .Bind(configuration.GetSection(PasswordlessOtpOptions.SectionName))
            .Validate(options => options.CodeLength is >= 4 and <= 9, "OTP code length must be between 4 and 9")
            .Validate(options => options.TtlMinutes > 0, "OTP TTL must be greater than zero")
            .Validate(options => options.MaxVerifyAttempts > 0, "OTP max verify attempts must be greater than zero")
            .ValidateOnStart();

        services.AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddIdentityCookies();

        services.AddAuthorization();

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.ExpireTimeSpan = TimeSpan.FromDays(7);
            options.SlidingExpiration = true;
        });

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedEmail = true;
            })
            .AddSignInManager()
            .AddApiEndpoints()
            .AddEntityFrameworkStores<UsersDbContext>();

        services.AddScoped<PasswordlessOtpStore>();
        services.AddScoped<PasswordlessOtpService>();

        services.AddSingleton<IAppEmailSender, SmtpAppEmailSender>();
        services.AddSingleton<IEmailSender<ApplicationUser>, IdentityEmailSender>();

        services.AddHealthChecks().AddDbContextCheck<UsersDbContext>("users_db");

        return services;
    }
}
