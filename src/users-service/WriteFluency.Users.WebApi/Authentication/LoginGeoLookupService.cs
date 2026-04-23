using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Options;
using WriteFluency.Users.WebApi.Options;

namespace WriteFluency.Users.WebApi.Authentication;

internal sealed class LoginGeoLookupService : ILoginGeoLookupService
{
    private readonly LoginLocationOptions _options;
    private readonly ILoginGeoLocationDataSource _dataSource;
    private readonly ILogger<LoginGeoLookupService> _logger;

    public LoginGeoLookupService(
        IOptions<LoginLocationOptions> options,
        ILoginGeoLocationDataSource dataSource,
        ILogger<LoginGeoLookupService> logger)
    {
        _options = options.Value;
        _dataSource = dataSource;
        _logger = logger;
    }

    public LoginGeoLookupResult Lookup(IPAddress? ipAddress)
    {
        if (!_options.Enabled)
        {
            return LoginGeoLookupResult.Disabled();
        }

        if (ipAddress is null)
        {
            return LoginGeoLookupResult.NoIp();
        }

        if (IsPrivateOrLocalIpAddress(ipAddress))
        {
            return LoginGeoLookupResult.PrivateIp();
        }

        try
        {
            var result = _dataSource.Lookup(ipAddress);
            if (result is null)
            {
                return LoginGeoLookupResult.NotFound();
            }

            return LoginGeoLookupResult.Success(
                result.CountryIsoCode,
                result.CountryName,
                result.City);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve geo location from IP address {IpAddress}.", ipAddress);
            return LoginGeoLookupResult.Error();
        }
    }

    private static bool IsPrivateOrLocalIpAddress(IPAddress ipAddress)
    {
        if (IPAddress.IsLoopback(ipAddress)
            || ipAddress.Equals(IPAddress.Any)
            || ipAddress.Equals(IPAddress.None)
            || ipAddress.Equals(IPAddress.IPv6Any)
            || ipAddress.Equals(IPAddress.IPv6None))
        {
            return true;
        }

        if (ipAddress.IsIPv4MappedToIPv6)
        {
            ipAddress = ipAddress.MapToIPv4();
        }

        return ipAddress.AddressFamily switch
        {
            AddressFamily.InterNetwork => IsPrivateIpv4(ipAddress),
            AddressFamily.InterNetworkV6 => IsPrivateIpv6(ipAddress),
            _ => true,
        };
    }

    private static bool IsPrivateIpv4(IPAddress ipAddress)
    {
        var bytes = ipAddress.GetAddressBytes();
        return bytes[0] == 10
            || bytes[0] == 127
            || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
            || (bytes[0] == 192 && bytes[1] == 168)
            || (bytes[0] == 169 && bytes[1] == 254)
            || (bytes[0] == 100 && bytes[1] is >= 64 and <= 127);
    }

    private static bool IsPrivateIpv6(IPAddress ipAddress)
    {
        if (ipAddress.IsIPv6LinkLocal || ipAddress.IsIPv6SiteLocal || ipAddress.IsIPv6Multicast)
        {
            return true;
        }

        var bytes = ipAddress.GetAddressBytes();
        return (bytes[0] & 0xFE) == 0xFC // fc00::/7
            || (bytes[0] == 0xFE && (bytes[1] & 0xC0) == 0x80); // fe80::/10
    }
}
