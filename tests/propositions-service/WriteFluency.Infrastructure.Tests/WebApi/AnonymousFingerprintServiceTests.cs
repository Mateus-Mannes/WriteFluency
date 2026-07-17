using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Shouldly;
using WriteFluency.Propositions;
using WriteFluency.TextComparisons;

namespace WriteFluency.Infrastructure.Tests.WebApi;

public sealed class AnonymousFingerprintServiceTests
{
    [Fact]
    public void ProReviewFingerprint_ShouldIgnoreForwardedForSourcePort()
    {
        var service = new AnonymousProReviewFingerprintService(
            Options.Create(new ProReviewTeaserOptions
            {
                AnonymousFingerprintSalt = "pro-review-test-salt"
            }));

        var firstHash = service.CreateFingerprintHash(CreateRequest("203.0.113.42:51234"));
        var secondHash = service.CreateFingerprintHash(CreateRequest("203.0.113.42:61234"));

        firstHash.ShouldNotBeNullOrWhiteSpace();
        secondHash.ShouldBe(firstHash);
    }

    [Fact]
    public void CatalogAccessFingerprint_ShouldIgnoreForwardedForSourcePort()
    {
        var service = new AnonymousCatalogAccessFingerprintService(
            Options.Create(new CatalogAccessTeaserOptions
            {
                AnonymousFingerprintSalt = "catalog-access-test-salt"
            }));

        var firstHash = service.CreateFingerprintHash(CreateRequest("203.0.113.42:51234"));
        var secondHash = service.CreateFingerprintHash(CreateRequest("203.0.113.42:61234"));

        firstHash.ShouldNotBeNullOrWhiteSpace();
        secondHash.ShouldBe(firstHash);
    }

    [Fact]
    public void ProReviewFingerprint_ShouldUseFirstForwardedForAddress()
    {
        var service = new AnonymousProReviewFingerprintService(
            Options.Create(new ProReviewTeaserOptions
            {
                AnonymousFingerprintSalt = "pro-review-test-salt"
            }));

        var directHash = service.CreateFingerprintHash(CreateRequest("203.0.113.42"));
        var forwardedChainHash = service.CreateFingerprintHash(CreateRequest("203.0.113.42:51234, 10.0.0.4"));

        forwardedChainHash.ShouldBe(directHash);
    }

    [Fact]
    public void ProReviewFingerprint_ShouldUseOnlyIpAndIgnoreBrowser()
    {
        var service = new AnonymousProReviewFingerprintService(
            Options.Create(new ProReviewTeaserOptions
            {
                AnonymousFingerprintSalt = "pro-review-test-salt"
            }));

        var chromeHash = service.CreateFingerprintHash(CreateRequest("203.0.113.42", "Mozilla/5.0 Chrome/150"));
        var safariHash = service.CreateFingerprintHash(CreateRequest("203.0.113.42", "Mozilla/5.0 Safari/605.1.15"));

        safariHash.ShouldBe(chromeHash);
    }

    [Fact]
    public void ProReviewFingerprint_ShouldReturnNormalizedIpAddress()
    {
        var service = new AnonymousProReviewFingerprintService(
            Options.Create(new ProReviewTeaserOptions
            {
                AnonymousFingerprintSalt = "pro-review-test-salt"
            }));

        var fingerprint = service.CreateFingerprint(CreateRequest("203.0.113.42:51234"));

        fingerprint.ShouldNotBeNull();
        fingerprint.IpAddress.ShouldBe("203.0.113.42");
        fingerprint.Hash.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void CatalogAccessFingerprint_ShouldReturnNormalizedIpAddress()
    {
        var service = new AnonymousCatalogAccessFingerprintService(
            Options.Create(new CatalogAccessTeaserOptions
            {
                AnonymousFingerprintSalt = "catalog-access-test-salt"
            }));

        var fingerprint = service.CreateFingerprint(CreateRequest("203.0.113.42:51234"));

        fingerprint.ShouldNotBeNull();
        fingerprint.IpAddress.ShouldBe("203.0.113.42");
        fingerprint.Hash.ShouldNotBeNullOrWhiteSpace();
    }

    private static HttpRequest CreateRequest(
        string forwardedFor,
        string userAgent = "Mozilla/5.0 Chrome/150")
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["User-Agent"] = userAgent;
        context.Request.Headers["X-Forwarded-For"] = forwardedFor;
        return context.Request;
    }
}
