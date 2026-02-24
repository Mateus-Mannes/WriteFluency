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
    private static readonly TimeSpan PollingInterval = TimeSpan.FromMinutes(1);
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
        DateTimeOffset? lastExecution = null;
        DateTimeOffset? lastWarmupExecution = null;
        bool? lastKnownIsActive = null;
        string? lastKnownCron = null;
        string? lastInvalidCron = null;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var settings = await GetWorkerRuntimeSettingsAsync(stoppingToken);
                var isActive = bool.TryParse(settings.IsActiveRawValue, out var parsed) && parsed && !_environment.IsDevelopment();

                if (_environment.IsDevelopment() && _configuration.GetValue<bool>("RunNewsWorkerOnStartup"))
                {
                    await RunDailyGenerationAsync(
                        trigger: "development-startup",
                        cronExpression: settings.CronExpression,
                        scheduledAtUtc: DateTimeOffset.UtcNow,
                        cancellationToken: stoppingToken);
                    return;
                }

                if (lastKnownIsActive != isActive || !string.Equals(lastKnownCron, settings.CronExpression, StringComparison.Ordinal))
                {
                    _logger.LogInformation(
                        "NewsWorker schedule updated. IsActive={IsActive} Cron={Cron}",
                        isActive,
                        settings.CronExpression);
                    lastKnownIsActive = isActive;
                    lastKnownCron = settings.CronExpression;
                }

                if (!isActive)
                {
                    await Task.Delay(PollingInterval, stoppingToken);
                    continue;
                }

                CronExpression cronExpression;
                try
                {
                    cronExpression = CronExpression.Parse(settings.CronExpression);
                    lastInvalidCron = null;
                }
                catch (CronFormatException ex)
                {
                    if (!string.Equals(lastInvalidCron, settings.CronExpression, StringComparison.Ordinal))
                    {
                        _logger.LogError(ex, "NewsWorker cron is invalid. Cron={Cron}", settings.CronExpression);
                        lastInvalidCron = settings.CronExpression;
                    }

                    await Task.Delay(PollingInterval, stoppingToken);
                    continue;
                }

                var now = DateTimeOffset.UtcNow;
                var next = cronExpression.GetNextOccurrence(lastExecution ?? now.AddMinutes(-1), TimeZoneInfo.Utc);

                if (next.HasValue && next.Value <= now)
                {
                    var dailyRunCompleted = await RunDailyGenerationAsync(
                        trigger: "scheduled",
                        cronExpression: settings.CronExpression,
                        scheduledAtUtc: next.Value,
                        cancellationToken: stoppingToken);

                    if (dailyRunCompleted)
                    {
                        lastExecution = now;
                        lastWarmupExecution = now;
                    }
                }
                else
                {
                    var warmupOptions = _cloudflareOptions.CurrentValue;
                    var warmupInterval = GetWarmupInterval(warmupOptions);
                    if (warmupOptions.WarmupEnabled && ShouldRunPeriodicWarmup(now, lastWarmupExecution, warmupInterval))
                    {
                        var warmupCompleted = await RunWarmupAsync(
                            trigger: "periodic",
                            cancellationToken: stoppingToken);

                        if (warmupCompleted)
                        {
                            lastWarmupExecution = now;
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in NewsWorker loop.");
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }
    }

    private async Task<WorkerRuntimeSettings> GetWorkerRuntimeSettingsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settingsByKey = await db.AppSettings
            .AsNoTracking()
            .Where(x => x.Key == AppSettings.NewsWorkerCron.Key || x.Key == AppSettings.IsNewsWorkerActive.Key)
            .ToDictionaryAsync(x => x.Key, x => x.Value, cancellationToken);

        var cronExpression = settingsByKey.GetValueOrDefault(AppSettings.NewsWorkerCron.Key) ?? AppSettings.NewsWorkerCron.Value;
        var isActiveRawValue = settingsByKey.GetValueOrDefault(AppSettings.IsNewsWorkerActive.Key) ?? AppSettings.IsNewsWorkerActive.Value;
        return new WorkerRuntimeSettings(cronExpression, isActiveRawValue);
    }

    private async Task<bool> RunDailyGenerationAsync(
        string trigger,
        string cronExpression,
        DateTimeOffset scheduledAtUtc,
        CancellationToken cancellationToken)
    {
        var runId = Guid.NewGuid().ToString("N");
        var startedAtUtc = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        using var activity = ActivitySource.StartActivity("news-worker.daily-generation", ActivityKind.Internal);
        activity?.SetTag("worker.name", nameof(NewsWorker));
        activity?.SetTag("worker.event", "daily-generation");
        activity?.SetTag("worker.run_id", runId);
        activity?.SetTag("worker.trigger", trigger);
        activity?.SetTag("worker.cron", cronExpression);
        activity?.SetTag("worker.scheduled_at_utc", scheduledAtUtc.ToString("O"));
        activity?.SetTag("worker.started_at_utc", startedAtUtc.ToString("O"));

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var generator = scope.ServiceProvider.GetRequiredService<DailyPropositionGenerator>();
            await generator.GenerateDailyPropositionsAsync(cancellationToken);

            var purgeStatus = "skipped-development";
            var purgeErrors = string.Empty;

            if (!_environment.IsDevelopment())
            {
                var purgeResult = await _cloudflareCachePurgeClient.PurgeConfiguredUrlsAsync(cancellationToken);
                if (purgeResult.IsFailed)
                {
                    purgeStatus = "failed";
                    purgeErrors = string.Join(" | ", purgeResult.Errors.Select(x => x.Message).Take(5));
                }
                else
                {
                    purgeStatus = "succeeded";
                }
            }

            if (_cloudflareOptions.CurrentValue.WarmupEnabled)
            {
                await RunWarmupAsync(
                    trigger: "daily-generation",
                    cancellationToken: cancellationToken);
            }

            stopwatch.Stop();
            activity?.SetStatus(ActivityStatusCode.Ok);
            activity?.SetTag("worker.completed_at_utc", DateTimeOffset.UtcNow.ToString("O"));
            activity?.SetTag("worker.duration_ms", stopwatch.ElapsedMilliseconds);
            activity?.SetTag("worker.purge_status", purgeStatus);

            if (string.IsNullOrWhiteSpace(purgeErrors))
            {
                _logger.LogInformation(
                    "NewsWorker run completed. Event=daily-generation RunId={RunId} Trigger={Trigger} Cron={Cron} ScheduledAtUtc={ScheduledAtUtc} DurationMs={DurationMs} PurgeStatus={PurgeStatus}",
                    runId,
                    trigger,
                    cronExpression,
                    scheduledAtUtc,
                    stopwatch.ElapsedMilliseconds,
                    purgeStatus);
            }
            else
            {
                activity?.SetTag("worker.purge_errors", purgeErrors);
                _logger.LogWarning(
                    "NewsWorker run completed with warnings. Event=daily-generation RunId={RunId} Trigger={Trigger} Cron={Cron} ScheduledAtUtc={ScheduledAtUtc} DurationMs={DurationMs} PurgeStatus={PurgeStatus} PurgeErrors={PurgeErrors}",
                    runId,
                    trigger,
                    cronExpression,
                    scheduledAtUtc,
                    stopwatch.ElapsedMilliseconds,
                    purgeStatus,
                    purgeErrors);
            }

            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("worker.completed_at_utc", DateTimeOffset.UtcNow.ToString("O"));
            activity?.SetTag("worker.duration_ms", stopwatch.ElapsedMilliseconds);
            _logger.LogError(
                ex,
                "NewsWorker run failed. Event=daily-generation RunId={RunId} Trigger={Trigger} Cron={Cron} ScheduledAtUtc={ScheduledAtUtc} DurationMs={DurationMs}",
                runId,
                trigger,
                cronExpression,
                scheduledAtUtc,
                stopwatch.ElapsedMilliseconds);

            return false;
        }
    }

    private async Task<bool> RunWarmupAsync(string trigger, CancellationToken cancellationToken)
    {
        var runId = Guid.NewGuid().ToString("N");
        var startedAtUtc = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        using var activity = ActivitySource.StartActivity("news-worker.warmup", ActivityKind.Internal);
        activity?.SetTag("worker.name", nameof(NewsWorker));
        activity?.SetTag("worker.event", "warmup");
        activity?.SetTag("worker.run_id", runId);
        activity?.SetTag("worker.trigger", trigger);
        activity?.SetTag("worker.started_at_utc", startedAtUtc.ToString("O"));

        try
        {
            var result = await WarmCloudflareCacheAsync(cancellationToken);

            stopwatch.Stop();
            activity?.SetStatus(ActivityStatusCode.Ok);
            activity?.SetTag("worker.completed_at_utc", DateTimeOffset.UtcNow.ToString("O"));
            activity?.SetTag("worker.duration_ms", stopwatch.ElapsedMilliseconds);
            activity?.SetTag("worker.warmup_status", result.Status.ToString());
            activity?.SetTag("worker.warmup_total_urls", result.TotalCount);
            activity?.SetTag("worker.warmup_success_count", result.SuccessCount);
            activity?.SetTag("worker.warmup_failure_count", result.Failures.Count);

            if (result.Status == WarmupStatus.CompletedWithFailures)
            {
                var sampleFailures = string.Join(" | ", result.Failures.Take(5));
                activity?.SetTag("worker.warmup_sample_failures", sampleFailures);
                _logger.LogWarning(
                    "NewsWorker run completed with warnings. Event=warmup RunId={RunId} Trigger={Trigger} DurationMs={DurationMs} Status={Status} SuccessCount={SuccessCount} TotalCount={TotalCount} SampleFailures={SampleFailures}",
                    runId,
                    trigger,
                    stopwatch.ElapsedMilliseconds,
                    result.Status,
                    result.SuccessCount,
                    result.TotalCount,
                    sampleFailures);
                return true;
            }

            _logger.LogInformation(
                "NewsWorker run completed. Event=warmup RunId={RunId} Trigger={Trigger} DurationMs={DurationMs} Status={Status} SuccessCount={SuccessCount} TotalCount={TotalCount}",
                runId,
                trigger,
                stopwatch.ElapsedMilliseconds,
                result.Status,
                result.SuccessCount,
                result.TotalCount);

            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("worker.completed_at_utc", DateTimeOffset.UtcNow.ToString("O"));
            activity?.SetTag("worker.duration_ms", stopwatch.ElapsedMilliseconds);
            _logger.LogError(
                ex,
                "NewsWorker run failed. Event=warmup RunId={RunId} Trigger={Trigger} DurationMs={DurationMs}",
                runId,
                trigger,
                stopwatch.ElapsedMilliseconds);

            return false;
        }
    }

    private async Task<WarmupResult> WarmCloudflareCacheAsync(CancellationToken cancellationToken)
    {
        var options = _cloudflareOptions.CurrentValue;
        if (!options.WarmupEnabled)
        {
            return WarmupResult.Disabled;
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
            return WarmupResult.SkippedNoUrls;
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
            return WarmupResult.CompletedWithFailures(urlsToWarm.Count, successCount, failures);
        }

        return WarmupResult.Completed(urlsToWarm.Count, successCount);
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

    private sealed record WorkerRuntimeSettings(string CronExpression, string IsActiveRawValue);

    private enum WarmupStatus
    {
        Disabled = 0,
        SkippedNoUrls = 1,
        Completed = 2,
        CompletedWithFailures = 3
    }

    private sealed record WarmupResult(WarmupStatus Status, int TotalCount, int SuccessCount, IReadOnlyList<string> Failures)
    {
        public static WarmupResult Disabled { get; } = new(WarmupStatus.Disabled, 0, 0, Array.Empty<string>());
        public static WarmupResult SkippedNoUrls { get; } = new(WarmupStatus.SkippedNoUrls, 0, 0, Array.Empty<string>());

        public static WarmupResult Completed(int totalCount, int successCount) =>
            new(WarmupStatus.Completed, totalCount, successCount, Array.Empty<string>());

        public static WarmupResult CompletedWithFailures(int totalCount, int successCount, IReadOnlyList<string> failures) =>
            new(WarmupStatus.CompletedWithFailures, totalCount, successCount, failures);
    }
}
