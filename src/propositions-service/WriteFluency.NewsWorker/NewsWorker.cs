using Cronos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using System.Collections.Concurrent;
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
    private static readonly string[] ImageVariantSuffixes = ["w320", "w512", "w640", "w1024"];
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

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                string cron;
                string isActiveStr;
                using (SuppressInstrumentationScope.Begin())
                {
                    cron = await db.AppSettings
                        .Where(x => x.Key == AppSettings.NewsWorkerCron.Key)
                        .Select(x => x.Value)
                        .FirstOrDefaultAsync(stoppingToken) ?? AppSettings.NewsWorkerCron.Value;

                    isActiveStr = await db.AppSettings
                        .Where(x => x.Key == AppSettings.IsNewsWorkerActive.Key)
                        .Select(x => x.Value)
                        .FirstOrDefaultAsync(stoppingToken) ?? AppSettings.IsNewsWorkerActive.Value;
                }

                var isActive = bool.TryParse(isActiveStr, out var parsed) && parsed && !_environment.IsDevelopment();

                if (_environment.IsDevelopment() && _configuration.GetValue<bool>("RunNewsWorkerOnStartup"))
                {
                    _logger.LogInformation("Running immediately in Development mode.");
                    await GenerateDailyPropositionsAsync(stoppingToken);
                    return;
                }

                if (!isActive)
                {
                    _logger.LogDebug("Worker is disabled.");
                }
                else
                {
                    var now = DateTimeOffset.UtcNow;
                    var cronExpr = CronExpression.Parse(cron);
                    var next = cronExpr.GetNextOccurrence(lastExecution ?? now.AddMinutes(-1), TimeZoneInfo.Utc);
                    var generatedNow = false;

                    if (next.HasValue && next.Value <= now)
                    {
                        _logger.LogInformation("Triggering scheduled execution (cron matched at {CronTime})", next.Value);
                        await GenerateDailyPropositionsAsync(stoppingToken);
                        lastExecution = now;
                        lastWarmupExecution = now;
                        generatedNow = true;
                    }

                    var warmupInterval = GetWarmupInterval(_cloudflareOptions.CurrentValue);
                    if (!generatedNow && ShouldRunPeriodicWarmup(now, lastWarmupExecution, warmupInterval))
                    {
                        _logger.LogInformation(
                            "Triggering periodic cache warm-up (every {IntervalHours} hour(s)).",
                            warmupInterval.TotalHours);

                        await WarmCloudflareCacheAsync(null, stoppingToken);
                        lastWarmupExecution = now;
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
        using var activity = ActivitySource.StartActivity("news-worker.daily-processing", ActivityKind.Internal);
        activity?.SetTag("worker.name", nameof(NewsWorker));
        activity?.SetTag("worker.started_at_utc", DateTime.UtcNow.ToString("O"));

        var generationStartedAtUtc = DateTime.UtcNow;

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

        await WarmCloudflareCacheAsync(generationStartedAtUtc, cancellationToken);
    }

    private async Task WarmCloudflareCacheAsync(DateTime? generationStartedAtUtc, CancellationToken cancellationToken)
    {
        var options = _cloudflareOptions.CurrentValue;
        if (!options.WarmupEnabled)
        {
            return;
        }

        if (!Uri.TryCreate(options.AssetsBaseAddress, UriKind.Absolute, out var assetsBaseAddress))
        {
            _logger.LogWarning(
                "Cloudflare warm-up skipped because assets base URL is invalid: {AssetsBaseAddress}",
                options.AssetsBaseAddress);
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

        List<WarmupProposition> generatedPropositions;
        using (var scope = _serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var propositionsQuery = db.Propositions
                .AsNoTracking()
                .Where(p => !p.IsDeleted);

            if (generationStartedAtUtc.HasValue)
            {
                propositionsQuery = propositionsQuery.Where(p => p.CreatedAt >= generationStartedAtUtc.Value);
            }

            var maxPropositionsToWarm = Math.Clamp(options.WarmupRecentPropositionsLimit, 20, 500);
            generatedPropositions = await propositionsQuery
                .OrderByDescending(p => p.PublishedOn)
                .Take(maxPropositionsToWarm)
                .Select(p => new WarmupProposition(p.Id, p.ImageFileId, p.AudioFileId))
                .ToListAsync(cancellationToken);
        }

        var siteBaseAddress = TryGetSiteBaseAddress(options.PurgeUrls);

        foreach (var proposition in generatedPropositions)
        {
            if (siteBaseAddress is not null)
            {
                var propositionUrl = new Uri(siteBaseAddress, $"english-writing-exercise/{proposition.Id}");
                urlsToWarm.Add(propositionUrl.ToString());
            }

            if (!string.IsNullOrWhiteSpace(proposition.AudioFileId))
            {
                var audioUrl = new Uri(assetsBaseAddress, $"propositions/{proposition.AudioFileId}");
                urlsToWarm.Add(audioUrl.ToString());
            }

            if (string.IsNullOrWhiteSpace(proposition.ImageFileId))
            {
                continue;
            }

            var originalImageUrl = new Uri(assetsBaseAddress, $"images/{proposition.ImageFileId}");
            urlsToWarm.Add(originalImageUrl.ToString());

            var imageBaseId = GetImageBaseId(proposition.ImageFileId);
            if (string.IsNullOrWhiteSpace(imageBaseId))
            {
                continue;
            }

            foreach (var suffix in ImageVariantSuffixes)
            {
                var variantUrl = new Uri(assetsBaseAddress, $"images/{imageBaseId}_{suffix}.webp");
                urlsToWarm.Add(variantUrl.ToString());
            }
        }

        if (urlsToWarm.Count == 0)
        {
            _logger.LogInformation("Cloudflare warm-up skipped because no URLs were collected.");
            return;
        }

        var client = _httpClientFactory.CreateClient(CacheWarmupHttpClientName);
        client.Timeout = TimeSpan.FromSeconds(Math.Clamp(options.WarmupTimeoutSeconds, 5, 120));

        var maxConcurrency = Math.Clamp(options.WarmupConcurrency, 1, 20);
        using var semaphore = new SemaphoreSlim(maxConcurrency);

        var failures = new ConcurrentBag<string>();
        var successCount = 0;

        await Task.WhenAll(urlsToWarm.Select(async url =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotModified)
                {
                    Interlocked.Increment(ref successCount);
                    return;
                }

                failures.Add($"{url} -> {(int)response.StatusCode}");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                failures.Add($"{url} -> {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                semaphore.Release();
            }
        }));

        if (!failures.IsEmpty)
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

    private static string? GetImageBaseId(string? imageFileId)
    {
        if (string.IsNullOrWhiteSpace(imageFileId))
        {
            return null;
        }

        var lastDot = imageFileId.LastIndexOf('.');
        return lastDot > 0 ? imageFileId[..lastDot] : imageFileId;
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

    private sealed record WarmupProposition(int Id, string? ImageFileId, string AudioFileId);
}
