using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WriteFluency.Users.WebApi.Data;

namespace WriteFluency.Users.WebApi.Configuration;

public static class UsersServiceCollectionExtensions
{
    private const string UsersConnectionStringName = "wf-users-postgresdb";

    public static IServiceCollection AddUsersPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(UsersConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException($"Connection string '{UsersConnectionStringName}' was not found.");
        }

        services.AddDbContext<UsersDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<UsersDbContext>();

        services.AddHealthChecks().AddDbContextCheck<UsersDbContext>("users_db");

        return services;
    }
}
