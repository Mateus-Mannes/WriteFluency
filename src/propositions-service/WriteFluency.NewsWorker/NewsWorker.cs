using Cronos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Net;
using WriteFluency.Data;
using WriteFluency.Domain.App;
using WriteFluency.Infrastructure.ExternalApis;
using WriteFluency.Propositions;

namespace WriteFluency.NewsWorker;

/// <summary>
/// Downloads news articles, generates summaries texts and audio with it, and save all to the database.
/// </summary>
public class NewsWorker : BackgroundService
{
    private const string CacheWarmupHttpClientName = "cache-warmup";
    private const int WarmupRecentExercisesLimit = 20;
    private static readonly ActivitySource ActivitySource = new(nameof(NewsWorker));

    private readonly IServiceProvider _serviceProvider;
    private readonly ICloudflareCachePurgeClient _cloudflareCachePurgeClient;
    private readonly IOptionsMonitor<CloudflareOptions> _cloudflareOptions;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<NewsWorker> _logger;
    private readonly IConfiguration _configuration;

    public NewsWorker(
        IServiceProvider serviceProvider,
        ICloudflareCachePurgeClient cloudflareCachePurgeClient,
        IOptionsMonitor<CloudflareOptions> cloudflareOptions,
        IHttpClientFactory httpClientFactory,
        IHostEnvironment environment,
        ILogger<NewsWorker> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _cloudflareCachePurgeClient = cloudflareCachePurgeClient;
        _cloudflareOptions = cloudflareOptions;
        _httpClientFactory = httpClientFactory;
        _environment = environment;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("NewsWorker started.");

        DateTimeOffset? lastExecution = null;
        DateTimeOffset? lastWarmupExecution = null;
        Activity? dayCycleActivity = null;
        Activity? waitingWindowActivity = null;
        DateTime dayCycleStartedAtUtc = DateTime.UtcNow;
        DateTime waitingWindowStartedAtUtc = DateTime.UtcNow;
        var waitingCyclesCount = 0;

        void CloseWaitingWindow()
        {
            if (waitingWindowActivity is null)
            {
                return;
            }

            waitingWindowActivity.SetTag("worker.completed_at_utc", DateTime.UtcNow.ToString("O"));
            waitingWindowActivity.SetTag("worker.duration_ms", (long)(DateTime.UtcNow - waitingWindowStartedAtUtc).TotalMilliseconds);
            waitingWindowActivity.SetTag("worker.waiting_cycles_count", waitingCyclesCount);
            waitingWindowActivity.Dispose();
            waitingWindowActivity = null;
            waitingCyclesCount = 0;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            if (dayCycleActivity is null)
            {
                dayCycleStartedAtUtc = DateTime.UtcNow;
                dayCycleActivity = ActivitySource.StartActivity("news-worker.daily-cycle", ActivityKind.Internal);
                dayCycleActivity?.SetTag("worker.name", nameof(NewsWorker));
                dayCycleActivity?.SetTag("worker.started_at_utc", dayCycleStartedAtUtc.ToString("O"));
            }

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                string cron;
                string isActiveStr;
                var ranScheduledExecution = false;
                var ranPeriodicWarmup = false;

                cron = await db.AppSettings
                        .Where(x => x.Key == AppSettings.NewsWorkerCron.Key)
                        .Select(x => x.Value)
                        .FirstOrDefaultAsync(stoppingToken) ?? AppSettings.NewsWorkerCron.Value;

                isActiveStr = await db.AppSettings
                    .Where(x => x.Key == AppSettings.IsNewsWorkerActive.Key)
                    .Select(x => x.Value)
                    .FirstOrDefaultAsync(stoppingToken) ?? AppSettings.IsNewsWorkerActive.Value;

                var isActive = bool.TryParse(isActiveStr, out var parsed) && parsed && !_environment.IsDevelopment();

                if (_environment.IsDevelopment() && _configuration.GetValue<bool>("RunNewsWorkerOnStartup"))
                {
                    _logger.LogInformation("Running immediately in Development mode.");
                    CloseWaitingWindow();
                    await GenerateDailyPropositionsAsync(stoppingToken);
                    return;
                }

                if (!isActive)
                {
                    _logger.LogWarning("Worker is disabled.");
                }
                else
                {
                    var now = DateTimeOffset.UtcNow;
                    var cronExpr = CronExpression.Parse(cron);
                    var next = cronExpr.GetNextOccurrence(lastExecution ?? now.AddMinutes(-1), TimeZoneInfo.Utc);

                    // Log next scheduled execution time for better observability
                    _logger.LogInformation("Current time: {CurrentTime}. Next scheduled execution: {NextExecution}.", now, next);

                    if (next.HasValue && next.Value <= now)
                    {
                        ranScheduledExecution = true;
                        CloseWaitingWindow();
                        _logger.LogInformation("Triggering scheduled execution (cron matched at {CronTime})", next.Value);
                        await GenerateDailyPropositionsAsync(stoppingToken);

                        dayCycleActivity?.SetTag("worker.completed_at_utc", DateTime.UtcNow.ToString("O"));
                        var dayCycleDurationMs = (long)(DateTime.UtcNow - dayCycleStartedAtUtc).TotalMilliseconds;
                        dayCycleActivity?.SetTag("worker.duration_ms", dayCycleDurationMs);
                        dayCycleActivity?.Dispose();
                        dayCycleActivity = null;

                        dayCycleStartedAtUtc = DateTime.UtcNow;
                        dayCycleActivity = ActivitySource.StartActivity("news-worker.daily-cycle", ActivityKind.Internal);
                        dayCycleActivity?.SetTag("worker.name", nameof(NewsWorker));
                        dayCycleActivity?.SetTag("worker.started_at_utc", dayCycleStartedAtUtc.ToString("O"));

                        lastExecution = now;
                        lastWarmupExecution = now;
                        _logger.LogInformation("Scheduled execution completed at {ExecutionTime}.", DateTime.UtcNow);
                    }

                    var warmupInterval = GetWarmupInterval(_cloudflareOptions.CurrentValue);
                    if (ShouldRunPeriodicWarmup(now, lastWarmupExecution, warmupInterval))
                    {
                        CloseWaitingWindow();
                        using (var activity = ActivitySource.StartActivity("news-worker.warmup", ActivityKind.Internal))
                        {
                            activity?.SetTag("worker.name", nameof(NewsWorker));
                            activity?.SetTag("worker.started_at_utc", DateTime.UtcNow.ToString("O"));

                            _logger.LogInformation(
                                "Triggering periodic cache warm-up (every {IntervalHours} hour(s)).",
                                warmupInterval.TotalHours);

                            await WarmCloudflareCacheAsync(stoppingToken);
                            ranPeriodicWarmup = true;
                            lastWarmupExecution = now;
                        }
                    }
                }

                var isWaitingCycle = !ranScheduledExecution && !ranPeriodicWarmup;
                if (isWaitingCycle)
                {
                    if (waitingWindowActivity is null)
                    {
                        waitingWindowStartedAtUtc = DateTime.UtcNow;
                        waitingCyclesCount = 0;
                        waitingWindowActivity = ActivitySource.StartActivity("news-worker.waiting-window", ActivityKind.Internal);
                        waitingWindowActivity?.SetTag("worker.name", nameof(NewsWorker));
                        waitingWindowActivity?.SetTag("worker.started_at_utc", waitingWindowStartedAtUtc.ToString("O"));
                    }

                    waitingCyclesCount++;
                    waitingWindowActivity?.SetTag("worker.waiting_cycles_count", waitingCyclesCount);
                }
                else if (waitingWindowActivity is not null)
                {
                    CloseWaitingWindow();
                }
            }
            catch (OperationCanceledException ex) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "NewsWorker is stopping due to cancellation request.");
                CloseWaitingWindow();
                dayCycleActivity?.SetTag("worker.completed_at_utc", DateTime.UtcNow.ToString("O"));
                dayCycleActivity?.SetTag("worker.duration_ms", (long)(DateTime.UtcNow - dayCycleStartedAtUtc).TotalMilliseconds);
                dayCycleActivity?.Dispose();
                break;
            }
            catch (OperationCanceledException ex)
            {
                _logger.LogError(ex, "NewsWorker loop received a non-shutdown cancellation and will continue.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in NewsWorker.");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }

        CloseWaitingWindow();
        dayCycleActivity?.SetTag("worker.completed_at_utc", DateTime.UtcNow.ToString("O"));
        dayCycleActivity?.SetTag("worker.duration_ms", (long)(DateTime.UtcNow - dayCycleStartedAtUtc).TotalMilliseconds);
        dayCycleActivity?.Dispose();
    }

    private async Task GenerateDailyPropositionsAsync(CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("news-worker.daily-processing", ActivityKind.Internal);
        activity?.SetTag("worker.name", nameof(NewsWorker));
        activity?.SetTag("worker.started_at_utc", DateTime.UtcNow.ToString("O"));

        using var scope = _serviceProvider.CreateScope();
        var generator = scope.ServiceProvider.GetRequiredService<DailyPropositionGenerator>();
        await generator.GenerateDailyPropositionsAsync(cancellationToken);

        if(!_environment.IsDevelopment())
        {
            var purgeResult = await _cloudflareCachePurgeClient.PurgeConfiguredUrlsAsync(cancellationToken);
            if (purgeResult.IsFailed)
            {
                _logger.LogWarning(
                    "Daily proposition generation finished, but Cloudflare purge failed: {Errors}",
                    string.Join(", ", purgeResult.Errors.Select(x => x.Message)));
            }
        }

        await WarmCloudflareCacheAsync(cancellationToken);
    }

    private async Task WarmCloudflareCacheAsync(CancellationToken cancellationToken)
    {
        var options = _cloudflareOptions.CurrentValue;
        if (!options.WarmupEnabled)
        {
            return;
        }

        var urlsToWarm = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var url in options.PurgeUrls ?? [])
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out _))
            {
                urlsToWarm.Add(url);
            }
        }

        List<int> recentExerciseIds;
        using (var scope = _serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            recentExerciseIds = await db.Propositions
                .AsNoTracking()
                .Where(p => !p.IsDeleted)
                .OrderByDescending(p => p.PublishedOn)
                .Take(WarmupRecentExercisesLimit)
                .Select(p => p.Id)
                .ToListAsync(cancellationToken);
        }

        var siteBaseAddress = TryGetSiteBaseAddress(options.PurgeUrls);

        foreach (var exerciseId in recentExerciseIds)
        {
            if (siteBaseAddress is null)
            {
                continue;
            }

            var propositionUrl = new Uri(siteBaseAddress, $"english-writing-exercise/{exerciseId}");
            urlsToWarm.Add(propositionUrl.ToString());
        }

        if (urlsToWarm.Count == 0)
        {
            _logger.LogInformation("Cloudflare warm-up skipped because no URLs were collected.");
            return;
        }

        var client = _httpClientFactory.CreateClient(CacheWarmupHttpClientName);
        client.Timeout = TimeSpan.FromSeconds(Math.Clamp(options.WarmupTimeoutSeconds, 5, 120));

        var failures = new List<string>();
        var successCount = 0;

        foreach (var url in urlsToWarm)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotModified)
                {
                    successCount++;
                    continue;
                }

                failures.Add($"{url} -> {(int)response.StatusCode}");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                failures.Add($"{url} -> OperationCanceledException (request timeout/canceled)");
            }
            catch (Exception ex)
            {
                failures.Add($"{url} -> {ex.GetType().Name}: {ex.Message}");
            }
        }

        if (failures.Count > 0)
        {
            _logger.LogWarning(
                "Cloudflare warm-up completed with partial failures ({SuccessCount}/{TotalCount} succeeded). Sample failures: {SampleFailures}",
                successCount,
                urlsToWarm.Count,
                string.Join(" | ", failures.Take(10)));
            return;
        }

        _logger.LogInformation(
            "Cloudflare warm-up completed successfully ({SuccessCount}/{TotalCount} URLs).",
            successCount,
            urlsToWarm.Count);
    }

    private static Uri? TryGetSiteBaseAddress(IEnumerable<string>? configuredUrls)
    {
        var firstAbsolute = configuredUrls?
            .FirstOrDefault(url => Uri.TryCreate(url, UriKind.Absolute, out var parsed)
                && (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps));

        if (string.IsNullOrWhiteSpace(firstAbsolute))
        {
            return null;
        }

        var uri = new Uri(firstAbsolute);
        return new Uri(uri.GetLeftPart(UriPartial.Authority).TrimEnd('/') + "/");
    }

    private static TimeSpan GetWarmupInterval(CloudflareOptions options)
    {
        var clampedHours = Math.Clamp(options.WarmupIntervalHours, 1, 24);
        return TimeSpan.FromHours(clampedHours);
    }

    private static bool ShouldRunPeriodicWarmup(DateTimeOffset now, DateTimeOffset? lastWarmupExecution, TimeSpan interval)
    {
        if (interval <= TimeSpan.Zero)
        {
            return false;
        }

        return !lastWarmupExecution.HasValue || now - lastWarmupExecution.Value >= interval;
    }
}
