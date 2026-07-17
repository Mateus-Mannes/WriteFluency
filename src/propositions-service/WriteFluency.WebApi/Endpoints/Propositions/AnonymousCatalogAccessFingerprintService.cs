using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using WriteFluency.WebApi.Endpoints;

namespace WriteFluency.Propositions;

public sealed class AnonymousCatalogAccessFingerprintService
{
    private readonly CatalogAccessTeaserOptions _options;

    public AnonymousCatalogAccessFingerprintService(
        IOptions<CatalogAccessTeaserOptions> options)
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

        return CreateFingerprint(ipAddress);
    }

    private AnonymousClientFingerprint CreateFingerprint(string ipAddress)
    {
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
