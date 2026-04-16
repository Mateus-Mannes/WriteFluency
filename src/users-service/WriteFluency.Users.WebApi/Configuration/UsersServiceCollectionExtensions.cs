using Azure.Core;
using Azure.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
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
        return AddUsersPersistence(services, configuration, isProduction: false);
    }

    public static IServiceCollection AddUsersPersistence(this IServiceCollection services, IConfiguration configuration, bool isProduction)
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
            .Validate(options => !string.IsNullOrWhiteSpace(options.MessageIdDomain), "SMTP message ID domain is required")
            .ValidateOnStart();

        services.AddOptions<PasswordlessOtpOptions>()
            .Bind(configuration.GetSection(PasswordlessOtpOptions.SectionName))
            .Validate(options => options.CodeLength is >= 4 and <= 9, "OTP code length must be between 4 and 9")
            .Validate(options => options.TtlMinutes > 0, "OTP TTL must be greater than zero")
            .Validate(options => options.MaxVerifyAttempts > 0, "OTP max verify attempts must be greater than zero")
            .ValidateOnStart();

        services.AddOptions<ExternalAuthenticationOptions>()
            .Bind(configuration.GetSection(ExternalAuthenticationOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(options => IsValidConfirmationRedirectUrl(options.ConfirmationRedirectUrl), "Authentication confirmation redirect URL must be an absolute HTTP(S) URL ending in /auth/confirm-email and without a fragment")
            .ValidateOnStart();

        services.AddOptions<SharedAuthCookieOptions>()
            .Bind(configuration.GetSection(SharedAuthCookieOptions.SectionName))
            .Validate(options => string.Equals(options.Scheme, IdentityConstants.ApplicationScheme, StringComparison.Ordinal), $"Shared auth cookie scheme must be {IdentityConstants.ApplicationScheme}")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Scheme), "Shared auth cookie scheme is required")
            .Validate(options => !string.IsNullOrWhiteSpace(options.CookieName), "Shared auth cookie name is required")
            .ValidateOnStart();

        services.AddOptions<SharedDataProtectionOptions>()
            .Bind(configuration.GetSection(SharedDataProtectionOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.ApplicationName), "Shared data protection application name is required")
            .Validate(options => !isProduction || options.IsConfigured, "SharedDataProtection BlobUri and KeyIdentifier are required in production")
            .ValidateOnStart();

        var externalAuthOptions = configuration.GetSection(ExternalAuthenticationOptions.SectionName).Get<ExternalAuthenticationOptions>()
            ?? throw new InvalidOperationException("Authentication configuration section is required.");
        var sharedAuthCookieOptions = configuration.GetSection(SharedAuthCookieOptions.SectionName).Get<SharedAuthCookieOptions>()
            ?? new SharedAuthCookieOptions();
        var tokenCredential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
        {
            ExcludeInteractiveBrowserCredential = true
        });

        var authenticationBuilder = services.AddAuthentication(IdentityConstants.ApplicationScheme);
        authenticationBuilder.AddIdentityCookies();

        AddGoogleProvider(authenticationBuilder, externalAuthOptions.Google);
        AddMicrosoftProvider(authenticationBuilder, externalAuthOptions.Microsoft);

        services.AddAuthorization();

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.HttpOnly = true;
            options.Cookie.Name = sharedAuthCookieOptions.CookieName;
            options.Cookie.Domain = isProduction ? sharedAuthCookieOptions.CookieDomain : null;
            options.Cookie.SameSite = isProduction ? SameSiteMode.Lax : SameSiteMode.None;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.ExpireTimeSpan = TimeSpan.FromDays(7);
            options.SlidingExpiration = true;
            options.Events = new CookieAuthenticationEvents
            {
                OnRedirectToLogin = context =>
                {
                    if (IsApiRequest(context.Request))
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        return Task.CompletedTask;
                    }

                    context.Response.Redirect(context.RedirectUri);
                    return Task.CompletedTask;
                },
                OnRedirectToAccessDenied = context =>
                {
                    if (IsApiRequest(context.Request))
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        return Task.CompletedTask;
                    }

                    context.Response.Redirect(context.RedirectUri);
                    return Task.CompletedTask;
                }
            };
        });

        ConfigureSharedDataProtection(services, configuration, tokenCredential);

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
        services.AddScoped<IExternalLoginInfoResolver, DefaultExternalLoginInfoResolver>();

        services.AddSingleton<IAppEmailSender, SmtpAppEmailSender>();
        services.AddSingleton<IEmailSender<ApplicationUser>, IdentityEmailSender>();
        services.AddSingleton<TokenCredential>(_ => tokenCredential);

        services.AddHealthChecks().AddDbContextCheck<UsersDbContext>("users_db");

        return services;
    }

    private static void AddGoogleProvider(AuthenticationBuilder authenticationBuilder, ProviderOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ClientId) || string.IsNullOrWhiteSpace(options.ClientSecret))
        {
            return;
        }

        authenticationBuilder.AddGoogle(GoogleDefaults.AuthenticationScheme, googleOptions =>
        {
            googleOptions.ClientId = options.ClientId;
            googleOptions.ClientSecret = options.ClientSecret;
            googleOptions.CallbackPath = "/users/signin-google";
            googleOptions.SignInScheme = IdentityConstants.ExternalScheme;
            googleOptions.SaveTokens = true;
            googleOptions.ClaimActions.MapJsonKey("email_verified", "email_verified");
            googleOptions.ClaimActions.MapJsonKey("verified_email", "verified_email");
            googleOptions.Events = BuildOAuthEvents("google");
        });
    }

    private static void AddMicrosoftProvider(AuthenticationBuilder authenticationBuilder, ProviderOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ClientId) || string.IsNullOrWhiteSpace(options.ClientSecret))
        {
            return;
        }

        authenticationBuilder.AddMicrosoftAccount(MicrosoftAccountDefaults.AuthenticationScheme, microsoftOptions =>
        {
            microsoftOptions.ClientId = options.ClientId;
            microsoftOptions.ClientSecret = options.ClientSecret;
            microsoftOptions.CallbackPath = "/users/signin-microsoft";
            microsoftOptions.SignInScheme = IdentityConstants.ExternalScheme;
            microsoftOptions.SaveTokens = true;
            microsoftOptions.Events = BuildOAuthEvents("microsoft");
        });
    }

    private static OAuthEvents BuildOAuthEvents(string providerId)
    {
        return new OAuthEvents
        {
            OnRemoteFailure = context =>
            {
                context.HandleResponse();

                var target = context.Properties?.RedirectUri ?? $"/users/auth/external/{providerId}/callback";
                target = QueryHelpers.AddQueryString(
                    target,
                    ExternalAuthConstants.CallbackErrorCodeQueryName,
                    MapRemoteFailureCode(context));

                if (!target.Contains("returnUrl=", StringComparison.OrdinalIgnoreCase)
                    && context.Properties?.Items.TryGetValue(ExternalAuthConstants.ReturnUrlItemName, out var returnUrl) == true
                    && !string.IsNullOrWhiteSpace(returnUrl))
                {
                    target = QueryHelpers.AddQueryString(target, "returnUrl", returnUrl);
                }

                context.Response.Redirect(target);
                return Task.CompletedTask;
            }
        };
    }

    private static string MapRemoteFailureCode(RemoteFailureContext context)
    {
        var providerError = context.Request.Query["error"].ToString();
        if (string.Equals(providerError, "access_denied", StringComparison.OrdinalIgnoreCase))
        {
            return "access_denied";
        }

        var message = context.Failure?.Message ?? string.Empty;
        if (message.Contains("correlation", StringComparison.OrdinalIgnoreCase)
            || message.Contains("state", StringComparison.OrdinalIgnoreCase))
        {
            return "invalid_state";
        }

        return "callback_error";
    }

    private static bool IsValidConfirmationRedirectUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!(uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(uri.Fragment))
        {
            return false;
        }

        return string.Equals(uri.AbsolutePath, "/auth/confirm-email", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsApiRequest(HttpRequest request)
    {
        return request.Path.StartsWithSegments("/users", StringComparison.OrdinalIgnoreCase);
    }

    private static void ConfigureSharedDataProtection(
        IServiceCollection services,
        IConfiguration configuration,
        TokenCredential tokenCredential)
    {
        var sharedDataProtectionOptions = configuration.GetSection(SharedDataProtectionOptions.SectionName).Get<SharedDataProtectionOptions>()
            ?? new SharedDataProtectionOptions();

        var dataProtectionBuilder = services.AddDataProtection()
            .SetApplicationName(sharedDataProtectionOptions.ApplicationName);

        if (sharedDataProtectionOptions.IsConfigured)
        {
            dataProtectionBuilder.PersistKeysToAzureBlobStorage(new Uri(sharedDataProtectionOptions.BlobUri), tokenCredential)
                .ProtectKeysWithAzureKeyVault(new Uri(sharedDataProtectionOptions.KeyIdentifier), tokenCredential);
        }
    }
}
