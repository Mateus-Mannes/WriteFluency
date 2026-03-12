namespace WriteFluency.Users.DbMigrator;

public class Worker(
    IUsersMigrationExecutor migrationExecutor,
    IHostApplicationLifetime hostApplicationLifetime,
    ILogger<Worker> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => RunOnceAsync(stoppingToken);

    public async Task RunOnceAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Users DB migrator started.");

        try
        {
            await migrationExecutor.MigrateAsync(stoppingToken);
            logger.LogInformation("Users DB migrations applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Users DB migration failed.");
            throw;
        }
        finally
        {
            hostApplicationLifetime.StopApplication();
        }
    }
}
