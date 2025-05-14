namespace WriteFluency.NewsWorker;

/// <summary>
/// Downloads news articles, generates summaries texts and audio with it, and save all to the database.
/// </summary>
public class NewsWorker : BackgroundService
{
    private readonly ILogger<NewsWorker> _logger;

    public NewsWorker(ILogger<NewsWorker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            }
            await Task.Delay(1000, stoppingToken);
        }
    }
}
