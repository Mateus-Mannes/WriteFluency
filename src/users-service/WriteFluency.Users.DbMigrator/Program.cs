using Microsoft.EntityFrameworkCore;
using WriteFluency.Users.DbMigrator;
using WriteFluency.Users.WebApi.Data;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

var connectionString = builder.Configuration.GetConnectionString("wf-users-postgresdb")
    ?? throw new InvalidOperationException("Connection string 'wf-users-postgresdb' was not found.");

builder.Services.AddDbContext<UsersDbContext>(options =>
    options.UseNpgsql(connectionString, npgsql =>
        npgsql.MigrationsAssembly("WriteFluency.Users.WebApi")));

builder.EnrichNpgsqlDbContext<UsersDbContext>(configureSettings: settings =>
{
    settings.DisableRetry = false;
    settings.CommandTimeout = 30;
});

builder.Services.AddSingleton<IUsersMigrationExecutor, EfCoreUsersMigrationExecutor>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
