using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using WriteFluency.Users.WebApi.Options;

namespace WriteFluency.Users.WebApi.Authentication;

public class PasswordlessOtpStore
{
    private const string ValidateAndConsumeScript = """
        local key = KEYS[1]
        local candidate = ARGV[1]
        local maxAttempts = tonumber(ARGV[2])

        if redis.call('EXISTS', key) == 0 then
            return 0
        end

        local stored = redis.call('HGET', key, 'codeHash')
        local failedAttempts = tonumber(redis.call('HGET', key, 'failedAttempts') or '0')

        if failedAttempts >= maxAttempts then
            redis.call('DEL', key)
            return 0
        end

        if stored ~= candidate then
            failedAttempts = failedAttempts + 1
            if failedAttempts >= maxAttempts then
                redis.call('DEL', key)
            else
                redis.call('HSET', key, 'failedAttempts', failedAttempts)
            end
            return 0
        end

        redis.call('DEL', key)
        return 1
        """;

    private readonly PasswordlessOtpOptions _options;
    private readonly IDatabase _redis;
    private readonly ILogger<PasswordlessOtpStore> _logger;

    public PasswordlessOtpStore(
        IConnectionMultiplexer redis,
        IOptions<PasswordlessOtpOptions> options,
        ILogger<PasswordlessOtpStore> logger)
    {
        _options = options.Value;
        _redis = redis.GetDatabase();
        _logger = logger;
    }

    public async Task<bool> CanRequestAsync(string normalizedEmail, string ipAddress)
    {
        var requestWindow = TimeSpan.FromMinutes(_options.RequestWindowMinutes);

        var emailWindowKey = BuildWindowCounterKey("otp:req:email", normalizedEmail, requestWindow);
        var ipWindowKey = BuildWindowCounterKey("otp:req:ip", ipAddress, requestWindow);
        var cooldownKey = $"otp:cooldown:email:{normalizedEmail}";

        if (!await IsWithinLimitAsync(emailWindowKey, requestWindow, _options.MaxRequestsPerWindowPerEmail))
        {
            _logger.LogWarning("Passwordless OTP request denied by email window limit for {NormalizedEmail}", normalizedEmail);
            return false;
        }

        if (!await IsWithinLimitAsync(ipWindowKey, requestWindow, _options.MaxRequestsPerWindowPerIp))
        {
            _logger.LogWarning("Passwordless OTP request denied by IP window limit for {IpAddress}", ipAddress);
            return false;
        }

        var cooldownSet = await _redis.StringSetAsync(
            cooldownKey,
            "1",
            TimeSpan.FromSeconds(_options.MinimumSecondsBetweenRequestsPerEmail),
            when: When.NotExists);

        if (!cooldownSet)
        {
            _logger.LogWarning("Passwordless OTP request denied by email cooldown for {NormalizedEmail}", normalizedEmail);
            return false;
        }

        return true;
    }

    public async Task<string> IssueCodeAsync(string normalizedEmail)
    {
        var code = GenerateNumericCode(_options.CodeLength);
        var codeHash = ComputeCodeHash(normalizedEmail, code);

        var otpKey = BuildOtpKey(normalizedEmail);
        var ttl = TimeSpan.FromMinutes(_options.TtlMinutes);

        var entries = new HashEntry[]
        {
            new("codeHash", codeHash),
            new("failedAttempts", 0)
        };

        await _redis.HashSetAsync(otpKey, entries);
        await _redis.KeyExpireAsync(otpKey, ttl);

        return code;
    }

    public async Task<bool> ValidateCodeAsync(string normalizedEmail, string code)
    {
        var otpKey = BuildOtpKey(normalizedEmail);
        var candidateHash = ComputeCodeHash(normalizedEmail, code);

        var result = (int)(long)await _redis.ScriptEvaluateAsync(
            ValidateAndConsumeScript,
            [new RedisKey(otpKey)],
            [new RedisValue(candidateHash), new RedisValue(_options.MaxVerifyAttempts.ToString())]);

        return result == 1;
    }

    private async Task<bool> IsWithinLimitAsync(string key, TimeSpan window, int maxRequests)
    {
        var count = await _redis.StringIncrementAsync(key);
        if (count == 1)
        {
            await _redis.KeyExpireAsync(key, window);
        }

        return count <= maxRequests;
    }

    private static string BuildWindowCounterKey(string prefix, string value, TimeSpan window)
    {
        var bucketSeconds = (int)window.TotalSeconds;
        var nowBucket = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / bucketSeconds;
        return $"{prefix}:{value}:{nowBucket}";
    }

    private static string BuildOtpKey(string normalizedEmail) => $"otp:code:{normalizedEmail}";

    private static string GenerateNumericCode(int codeLength)
    {
        var clampedLength = Math.Clamp(codeLength, 4, 9);
        var maxExclusive = (int)Math.Pow(10, clampedLength);
        var value = RandomNumberGenerator.GetInt32(maxExclusive);
        return value.ToString($"D{clampedLength}");
    }

    private static string ComputeCodeHash(string normalizedEmail, string code)
    {
        var payload = Encoding.UTF8.GetBytes($"{normalizedEmail}:{code}");
        var hash = SHA256.HashData(payload);
        return Convert.ToBase64String(hash);
    }
}
