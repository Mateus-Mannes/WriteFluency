using Microsoft.Extensions.Options;
using StackExchange.Redis;
using WriteFluency.Users.WebApi.Options;

namespace WriteFluency.Users.WebApi.Support;

public sealed class SupportRequestRateLimiter
{
    private readonly SupportRequestOptions _options;
    private readonly IDatabase _redis;
    private readonly ILogger<SupportRequestRateLimiter> _logger;

    public SupportRequestRateLimiter(
        IConnectionMultiplexer redis,
        IOptions<SupportRequestOptions> options,
        ILogger<SupportRequestRateLimiter> logger)
    {
        _options = options.Value;
        _redis = redis.GetDatabase();
        _logger = logger;
    }

    public async Task<bool> CanSubmitAsync(string ipAddress)
    {
        var requestWindow = TimeSpan.FromMinutes(_options.RequestWindowMinutes);
        var key = BuildWindowCounterKey(ipAddress, requestWindow);
        var count = await _redis.StringIncrementAsync(key);
        if (count == 1)
        {
            await _redis.KeyExpireAsync(key, requestWindow);
        }

        var allowed = count <= _options.MaxRequestsPerWindowPerIp;
        if (!allowed)
        {
            _logger.LogWarning("Support request denied by IP window limit for {IpAddress}", ipAddress);
        }

        return allowed;
    }

    private static string BuildWindowCounterKey(string ipAddress, TimeSpan window)
    {
        var bucketSeconds = (int)window.TotalSeconds;
        var nowBucket = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / bucketSeconds;
        return $"support:req:ip:{ipAddress}:{nowBucket}";
    }
}
