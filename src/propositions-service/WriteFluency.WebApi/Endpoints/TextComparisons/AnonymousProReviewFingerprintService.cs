using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using WriteFluency.WebApi.Endpoints;

namespace WriteFluency.TextComparisons;

public sealed class AnonymousProReviewFingerprintService
{
    private readonly ProReviewTeaserOptions _options;

    public AnonymousProReviewFingerprintService(
        IOptions<ProReviewTeaserOptions> options)
    {
        _options = options.Value;
    }

    public string? CreateFingerprintHash(HttpRequest request)
    {
        return CreateFingerprint(request)?.Hash;
    }

    public AnonymousClientFingerprint? CreateFingerprint(HttpRequest request)
    {
        if (string.IsNullOrWhiteSpace(_options.AnonymousFingerprintSalt))
        {
            return null;
        }

        var ipAddress = AnonymousClientIpResolver.Resolve(request);
        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            return null;
        }

        var fingerprintMaterial = string.Join(
            "|",
            _options.AnonymousFingerprintSalt,
            ipAddress);
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(fingerprintMaterial));

        return new AnonymousClientFingerprint(
            Convert.ToHexString(hashBytes).ToLowerInvariant(),
            ipAddress);
    }
}
