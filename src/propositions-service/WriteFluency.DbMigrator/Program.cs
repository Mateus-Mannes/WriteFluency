using Microsoft.EntityFrameworkCore;
using WriteFluency.Data;
using WriteFluency.DbMigrator;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

builder.AddServiceDefaults();

builder.Services.AddDbContext<AppDbContext>(opts => opts.UseNpgsql(builder.Configuration.GetConnectionString("wf-postgresdb")));

builder.EnrichNpgsqlDbContext<AppDbContext>(
    configureSettings: settings =>
    {
        settings.DisableRetry = false;
        settings.CommandTimeout = 30;
    });

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();