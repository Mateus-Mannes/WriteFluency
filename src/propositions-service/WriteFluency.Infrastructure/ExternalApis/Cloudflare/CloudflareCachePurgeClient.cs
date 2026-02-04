using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentResults;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WriteFluency.Infrastructure.ExternalApis;

public class CloudflareCachePurgeClient : ICloudflareCachePurgeClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly IOptionsMonitor<CloudflareOptions> _options;
    private readonly ILogger<CloudflareCachePurgeClient> _logger;

    public CloudflareCachePurgeClient(
        HttpClient httpClient,
        IOptionsMonitor<CloudflareOptions> options,
        ILogger<CloudflareCachePurgeClient> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    public async Task<Result> PurgeConfiguredUrlsAsync(CancellationToken cancellationToken = default)
    {
        var options = _options.CurrentValue;
        var urls = (options.PurgeUrls ?? [])
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (urls.Length == 0)
        {
            _logger.LogWarning("Cloudflare purge skipped because no purge URLs were configured.");
            return Result.Ok();
        }

        if (string.IsNullOrWhiteSpace(options.ApiToken))
        {
            _logger.LogWarning("Cloudflare purge skipped because API token is missing. Configure {OptionName}.", "ExternalApis:Cloudflare:ApiToken");
            return Result.Ok();
        }

        var invalidUrls = urls
            .Where(url => !Uri.TryCreate(url, UriKind.Absolute, out var parsed) ||
                          (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
            .ToArray();

        if (invalidUrls.Length > 0)
        {
            return Result.Fail(new Error(
                $"Cloudflare purge URLs must be absolute HTTP(S) URLs. Invalid values: {string.Join(", ", invalidUrls)}"));
        }

        var zoneIdResult = await ResolveZoneIdAsync(options, cancellationToken);
        if (zoneIdResult.IsFailed)
        {
            return Result.Fail(zoneIdResult.Errors);
        }

        var payload = JsonSerializer.Serialize(new { files = urls });
        using var request = new HttpRequestMessage(HttpMethod.Post, $"zones/{zoneIdResult.Value}/purge_cache")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Cloudflare purge failed with HTTP {StatusCode}. Response: {ResponseBody}",
                (int)response.StatusCode,
                body);

            return Result.Fail(new Error($"Cloudflare purge failed with HTTP {(int)response.StatusCode}."));
        }

        CloudflareApiResponse? parsedResponse = null;
        if (!string.IsNullOrWhiteSpace(body))
        {
            parsedResponse = JsonSerializer.Deserialize<CloudflareApiResponse>(body, JsonOptions);
        }

        if (parsedResponse?.Success != true)
        {
            var errorMessage = parsedResponse?.Errors?.FirstOrDefault()?.Message ?? "Cloudflare purge response was not successful.";
            _logger.LogWarning("Cloudflare purge was rejected. {ErrorMessage}", errorMessage);
            return Result.Fail(new Error(errorMessage));
        }

        _logger.LogInformation("Cloudflare cache purge completed successfully for {Count} URL(s).", urls.Length);
        return Result.Ok();
    }

    private async Task<Result<string>> ResolveZoneIdAsync(CloudflareOptions options, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(options.ZoneId))
        {
            return Result.Ok(options.ZoneId);
        }

        if (string.IsNullOrWhiteSpace(options.ZoneName))
        {
            return Result.Fail(new Error("Cloudflare zone lookup requires ExternalApis:Cloudflare:ZoneName or ZoneId."));
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"zones?name={Uri.EscapeDataString(options.ZoneName)}&status=active&match=all");

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Cloudflare zone lookup failed with HTTP {StatusCode}. Response: {ResponseBody}",
                (int)response.StatusCode,
                body);

            return Result.Fail(new Error("Cloudflare zone lookup failed. Configure ExternalApis:Cloudflare:ZoneId or grant token permission to read zones."));
        }

        var parsedResponse = JsonSerializer.Deserialize<CloudflareZonesResponse>(body, JsonOptions);
        var zoneId = parsedResponse?.Result?.FirstOrDefault()?.Id;

        if (parsedResponse?.Success != true || string.IsNullOrWhiteSpace(zoneId))
        {
            var errorMessage = parsedResponse?.Errors?.FirstOrDefault()?.Message ?? "Unable to resolve Cloudflare zone ID.";
            _logger.LogWarning("Cloudflare zone lookup returned no active zone. {ErrorMessage}", errorMessage);
            return Result.Fail(new Error($"{errorMessage} Configure ExternalApis:Cloudflare:ZoneId if needed."));
        }

        return Result.Ok(zoneId);
    }

    private sealed class CloudflareApiResponse
    {
        public bool Success { get; set; }
        public List<CloudflareApiError>? Errors { get; set; }
    }

    private sealed class CloudflareZonesResponse
    {
        public bool Success { get; set; }
        public List<CloudflareZone>? Result { get; set; }
        public List<CloudflareApiError>? Errors { get; set; }
    }

    private sealed class CloudflareZone
    {
        public string Id { get; set; } = string.Empty;
    }

    private sealed class CloudflareApiError
    {
        public string Message { get; set; } = string.Empty;
    }
}
