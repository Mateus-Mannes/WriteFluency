using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace WriteFluency.TextComparisons;

public sealed class AnonymousProReviewFingerprintService
{
    private const string ForwardedForHeaderName = "X-Forwarded-For";
    private const string UserAgentHeaderName = "User-Agent";

    private readonly ProReviewTeaserOptions _options;

    public AnonymousProReviewFingerprintService(
        IOptions<ProReviewTeaserOptions> options)
    {
        _options = options.Value;
    }

    public string? CreateFingerprintHash(HttpRequest request)
    {
        if (string.IsNullOrWhiteSpace(_options.AnonymousFingerprintSalt))
        {
            return null;
        }

        var ipAddress = GetIpAddress(request);
        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            return null;
        }

        var userAgentBucket = GetUserAgentBucket(request);
        var fingerprintMaterial = string.Join(
            "|",
            _options.AnonymousFingerprintSalt,
            ipAddress,
            userAgentBucket);
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(fingerprintMaterial));

        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private static string? GetIpAddress(HttpRequest request)
    {
        if (request.Headers.TryGetValue(ForwardedForHeaderName, out var forwardedFor)
            && forwardedFor.Count > 0)
        {
            var firstForwardedAddress = forwardedFor.ToString()
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(firstForwardedAddress))
            {
                return firstForwardedAddress;
            }
        }

        return request.HttpContext.Connection.RemoteIpAddress?.ToString();
    }

    private static string GetUserAgentBucket(HttpRequest request)
    {
        if (!request.Headers.TryGetValue(UserAgentHeaderName, out var userAgent)
            || userAgent.Count == 0)
        {
            return "unknown";
        }

        var value = userAgent.ToString();
        if (value.Contains("Edg", StringComparison.OrdinalIgnoreCase))
        {
            return "edge";
        }

        if (value.Contains("Chrome", StringComparison.OrdinalIgnoreCase))
        {
            return "chrome";
        }

        if (value.Contains("Firefox", StringComparison.OrdinalIgnoreCase))
        {
            return "firefox";
        }

        if (value.Contains("Safari", StringComparison.OrdinalIgnoreCase))
        {
            return "safari";
        }

        return "other";
    }
}
