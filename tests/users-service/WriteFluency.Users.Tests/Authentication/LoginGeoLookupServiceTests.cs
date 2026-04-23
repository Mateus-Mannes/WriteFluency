using System.Net;
using Azure.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using WriteFluency.Users.WebApi.Authentication;
using WriteFluency.Users.WebApi.Options;

namespace WriteFluency.Users.Tests.Authentication;

public class LoginGeoLookupServiceTests
{
    [Fact]
    public void Lookup_ShouldReturnDisabled_WhenFeatureIsDisabled()
    {
        var dataSource = new TestGeoLocationDataSource(_ => new LoginGeoLocationData("US", "United States", "Seattle"));
        var service = CreateService(
            new LoginLocationOptions { Enabled = false, GeoLite2CityDbPath = "/unused.mmdb" },
            dataSource);

        var result = service.Lookup(IPAddress.Parse("8.8.8.8"));

        result.GeoLookupStatus.ShouldBe("disabled");
        dataSource.LookupInvocations.ShouldBe(0);
    }

    [Fact]
    public void Lookup_ShouldReturnNoIp_WhenIpAddressIsMissing()
    {
        var service = CreateService(
            new LoginLocationOptions { Enabled = true, GeoLite2CityDbPath = "/unused.mmdb" },
            new TestGeoLocationDataSource(_ => null));

        var result = service.Lookup(null);

        result.GeoLookupStatus.ShouldBe("no_ip");
    }

    [Fact]
    public void Lookup_ShouldReturnPrivateIp_WhenIpAddressIsPrivate()
    {
        var dataSource = new TestGeoLocationDataSource(_ => new LoginGeoLocationData("US", "United States", "Seattle"));
        var service = CreateService(
            new LoginLocationOptions { Enabled = true, GeoLite2CityDbPath = "/unused.mmdb" },
            dataSource);

        var result = service.Lookup(IPAddress.Parse("192.168.1.20"));

        result.GeoLookupStatus.ShouldBe("private_ip");
        dataSource.LookupInvocations.ShouldBe(0);
    }

    [Fact]
    public void Lookup_ShouldReturnSuccess_WhenGeoDataExists()
    {
        var service = CreateService(
            new LoginLocationOptions { Enabled = true, GeoLite2CityDbPath = "/unused.mmdb" },
            new TestGeoLocationDataSource(_ => new LoginGeoLocationData("US", "United States", "Seattle")));

        var result = service.Lookup(IPAddress.Parse("8.8.8.8"));

        result.GeoLookupStatus.ShouldBe("success");
        result.CountryIsoCode.ShouldBe("US");
        result.CountryName.ShouldBe("United States");
        result.City.ShouldBe("Seattle");
    }

    [Fact]
    public void Lookup_ShouldReturnNotFound_WhenGeoDataDoesNotExist()
    {
        var service = CreateService(
            new LoginLocationOptions { Enabled = true, GeoLite2CityDbPath = "/unused.mmdb" },
            new TestGeoLocationDataSource(_ => null));

        var result = service.Lookup(IPAddress.Parse("8.8.8.8"));

        result.GeoLookupStatus.ShouldBe("not_found");
        result.CountryIsoCode.ShouldBeNull();
        result.CountryName.ShouldBeNull();
        result.City.ShouldBeNull();
    }

    [Fact]
    public void Lookup_ShouldReturnError_WhenDataSourceThrows()
    {
        var service = CreateService(
            new LoginLocationOptions { Enabled = true, GeoLite2CityDbPath = "/unused.mmdb" },
            new TestGeoLocationDataSource(_ => throw new InvalidOperationException("boom")));

        var result = service.Lookup(IPAddress.Parse("8.8.8.8"));

        result.GeoLookupStatus.ShouldBe("error");
    }

    [Fact]
    public void Lookup_ShouldReturnError_WhenGeoLiteDatabaseFileIsMissing()
    {
        var options = new LoginLocationOptions
        {
            Enabled = true,
            GeoLite2CityDbPath = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.mmdb")
        };

        var service = CreateService(
            options,
            new MaxMindGeoLocationDataSource(Options.Create(options), new FakeTokenCredential()));

        var result = service.Lookup(IPAddress.Parse("8.8.8.8"));

        result.GeoLookupStatus.ShouldBe("error");
    }

    private static LoginGeoLookupService CreateService(
        LoginLocationOptions options,
        ILoginGeoLocationDataSource dataSource)
    {
        return new LoginGeoLookupService(
            Options.Create(options),
            dataSource,
            NullLogger<LoginGeoLookupService>.Instance);
    }

    private sealed class TestGeoLocationDataSource : ILoginGeoLocationDataSource
    {
        private readonly Func<IPAddress, LoginGeoLocationData?> _lookup;

        public int LookupInvocations { get; private set; }

        public TestGeoLocationDataSource(Func<IPAddress, LoginGeoLocationData?> lookup)
        {
            _lookup = lookup;
        }

        public LoginGeoLocationData? Lookup(IPAddress ipAddress)
        {
            LookupInvocations++;
            return _lookup(ipAddress);
        }
    }

    private sealed class FakeTokenCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            return new AccessToken("test-token", DateTimeOffset.UtcNow.AddHours(1));
        }

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(GetToken(requestContext, cancellationToken));
        }
    }
}
