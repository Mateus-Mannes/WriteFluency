namespace WriteFluency.Users.DbMigrator;

public sealed class Worker(
    IHostApplicationLifetime hostApplicationLifetime,
    ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Users DB migrator started.");

        await Task.Yield();

        if (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        logger.LogInformation("Users DB migrator finished (no schema migrations configured yet).");
        hostApplicationLifetime.StopApplication();
    }
}
