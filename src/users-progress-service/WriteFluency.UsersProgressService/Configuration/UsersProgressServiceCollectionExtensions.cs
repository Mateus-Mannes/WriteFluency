using Azure.Core;
using Azure.Identity;
using Microsoft.AspNetCore.DataProtection;
using WriteFluency.UsersProgressService.Authentication;
using WriteFluency.UsersProgressService.Options;
using WriteFluency.UsersProgressService.Progress;

namespace WriteFluency.UsersProgressService.Configuration;

public static class UsersProgressServiceCollectionExtensions
{
    public static IServiceCollection AddUsersProgressService(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<CosmosProgressOptions>()
            .Bind(configuration.GetSection(CosmosProgressOptions.SectionName))
            .Validate(options => options.IsConfigured, "Cosmos progress settings are required and must use a supported namespace")
            .ValidateOnStart();

        services.AddOptions<SharedAuthCookieOptions>()
            .Bind(configuration.GetSection(SharedAuthCookieOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Scheme), "Shared auth cookie scheme is required")
            .Validate(options => !string.IsNullOrWhiteSpace(options.CookieName), "Shared auth cookie name is required");

        services.AddOptions<SharedDataProtectionOptions>()
            .Bind(configuration.GetSection(SharedDataProtectionOptions.SectionName));

        services.AddOptions<CorsOptions>()
            .Bind(configuration.GetSection(CorsOptions.SectionName));

        var credential = CreateTokenCredential();
        services.AddSingleton<TokenCredential>(credential);

        ConfigureDataProtection(services, configuration, credential);

        services.AddSingleton<IUserProgressRepository, CosmosUserProgressRepository>();
        services.AddScoped<IUserProgressTrackingService, UserProgressTrackingService>();
        services.AddSingleton<ISharedCookieAuthenticationService, SharedCookieAuthenticationService>();

        return services;
    }

    private static void ConfigureDataProtection(
        IServiceCollection services,
        IConfiguration configuration,
        TokenCredential credential)
    {
        var options = configuration.GetSection(SharedDataProtectionOptions.SectionName).Get<SharedDataProtectionOptions>()
            ?? new SharedDataProtectionOptions();

        if (!options.IsConfigured)
        {
            throw new InvalidOperationException(
                "SharedDataProtection configuration is required for users-progress-service. Configure ApplicationName, BlobUri, and KeyIdentifier.");
        }

        var builder = services.AddDataProtection()
            .SetApplicationName(options.ApplicationName);

        builder.PersistKeysToAzureBlobStorage(new Uri(options.BlobUri), credential)
            .ProtectKeysWithAzureKeyVault(new Uri(options.KeyIdentifier), credential);
    }

    private static TokenCredential CreateTokenCredential()
    {
        return new DefaultAzureCredential(new DefaultAzureCredentialOptions
        {
            ExcludeInteractiveBrowserCredential = true
        });
    }
}
