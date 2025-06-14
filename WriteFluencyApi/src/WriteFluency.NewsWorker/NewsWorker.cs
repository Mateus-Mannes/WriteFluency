using Cronos;
using Microsoft.Extensions.Options;
using WriteFluency.Infrastructure.ExternalApis;
using WriteFluency.Propositions;

namespace WriteFluency.NewsWorker;

/// <summary>
/// Downloads news articles, generates summaries texts and audio with it, and save all to the database.
/// </summary>
public class NewsWorker : BackgroundService
{
    private readonly PropositionOptions _propositionOptions;
    private readonly IServiceProvider _serviceProvider;
    private readonly IHostEnvironment _environment;

    public NewsWorker(
        IOptionsMonitor<PropositionOptions> propositionOptions,
        IServiceProvider serviceProvider,
        IHostEnvironment environment,
        IOptionsMonitor<OpenAIOptions> openAIOptions)
    {
        _propositionOptions = propositionOptions.CurrentValue;
        _serviceProvider = serviceProvider;
        _environment = environment;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_environment.IsDevelopment())
        {
            // In development, run the task immediately to test it
            await GenerateDailyPropositionsAsync(stoppingToken);
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var next = CronExpression.Parse(_propositionOptions.DailyRunCron)
                .GetNextOccurrence(DateTimeOffset.Now, TimeZoneInfo.Utc);
            if (next.HasValue)
            {
                var delay = next.Value - DateTimeOffset.Now;
                if (delay > TimeSpan.Zero) await Task.Delay(delay, stoppingToken);
                await GenerateDailyPropositionsAsync(stoppingToken);
            }
        }
    }

    private async Task GenerateDailyPropositionsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var generator = scope.ServiceProvider.GetRequiredService<DailyPropositionGenerator>();
        await generator.GenerateDailyPropositionsAsync(cancellationToken);
    }
}
