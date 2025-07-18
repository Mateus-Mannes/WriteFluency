using Cronos;
using Microsoft.EntityFrameworkCore;
using WriteFluency.Data;
using WriteFluency.Domain.App;
using WriteFluency.Propositions;

namespace WriteFluency.NewsWorker;

/// <summary>
/// Downloads news articles, generates summaries texts and audio with it, and save all to the database.
/// </summary>
public class NewsWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<NewsWorker> _logger;

    public NewsWorker(
        IServiceProvider serviceProvider,
        IHostEnvironment environment,
        ILogger<NewsWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _environment = environment;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("NewsWorker started.");

        DateTimeOffset? lastExecution = null;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var cron = await db.AppSettings
                    .Where(x => x.Key == AppSettings.NewsWorkerCron.Key)
                    .Select(x => x.Value)
                    .FirstOrDefaultAsync(stoppingToken) ?? AppSettings.NewsWorkerCron.Value;

                var isActiveStr = await db.AppSettings
                    .Where(x => x.Key == AppSettings.IsNewsWorkerActive.Key)
                    .Select(x => x.Value)
                    .FirstOrDefaultAsync(stoppingToken) ?? AppSettings.IsNewsWorkerActive.Value;

                var isActive = bool.TryParse(isActiveStr, out var parsed) && parsed;

                if (_environment.IsDevelopment() && isActive)
                {
                    _logger.LogInformation("Running immediately in Development mode.");
                    await GenerateDailyPropositionsAsync(stoppingToken);
                    return;
                }

                if (!isActive)
                {
                    _logger.LogInformation("Worker is disabled.");
                }
                else
                {
                    var now = DateTimeOffset.UtcNow;
                    var cronExpr = CronExpression.Parse(cron);
                    var next = cronExpr.GetNextOccurrence(lastExecution ?? now.AddMinutes(-1), TimeZoneInfo.Utc);

                    if (next.HasValue && next.Value <= now)
                    {
                        _logger.LogInformation("Triggering scheduled execution (cron matched at {CronTime})", next.Value);
                        await GenerateDailyPropositionsAsync(stoppingToken);
                        lastExecution = now;
                    }
                    else
                    {
                        _logger.LogDebug("Not time yet. Next execution expected at {Next}", next);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in NewsWorker.");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task GenerateDailyPropositionsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var generator = scope.ServiceProvider.GetRequiredService<DailyPropositionGenerator>();
        await generator.GenerateDailyPropositionsAsync(cancellationToken);
    }
}
