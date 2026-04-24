using System.Net;
using Microsoft.Extensions.Primitives;

namespace WriteFluency.Users.WebApi.Authentication;

internal sealed class ClientIpResolver : IClientIpResolver
{
    private const string CloudflareConnectingIpHeader = "CF-Connecting-IP";
    private const string TrueClientIpHeader = "True-Client-IP";
    private const string XForwardedForHeader = "X-Forwarded-For";
    private const string XRealIpHeader = "X-Real-IP";
    private const string ForwardedForPrefix = "for=";

    public IPAddress? Resolve(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (TryParseSingleIpHeader(httpContext.Request.Headers[CloudflareConnectingIpHeader], out var ipAddress))
        {
            return ipAddress;
        }

        if (TryParseSingleIpHeader(httpContext.Request.Headers[TrueClientIpHeader], out ipAddress))
        {
            return ipAddress;
        }

        if (TryParseForwardedForHeader(httpContext.Request.Headers[XForwardedForHeader], out ipAddress))
        {
            return ipAddress;
        }

        if (TryParseSingleIpHeader(httpContext.Request.Headers[XRealIpHeader], out ipAddress))
        {
            return ipAddress;
        }

        return NormalizeIpAddress(httpContext.Connection.RemoteIpAddress);
    }

    private static bool TryParseForwardedForHeader(StringValues values, out IPAddress? ipAddress)
    {
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var entries = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            foreach (var entry in entries)
            {
                if (TryParseIpAddress(entry, out ipAddress))
                {
                    return true;
                }
            }
        }

        ipAddress = null;
        return false;
    }

    private static bool TryParseSingleIpHeader(StringValues values, out IPAddress? ipAddress)
    {
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (TryParseIpAddress(value, out ipAddress))
            {
                return true;
            }
        }

        ipAddress = null;
        return false;
    }

    private static bool TryParseIpAddress(string rawValue, out IPAddress? ipAddress)
    {
        ipAddress = null;
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        var candidate = rawValue.Trim().Trim('"');
        if (candidate.StartsWith(ForwardedForPrefix, StringComparison.OrdinalIgnoreCase))
        {
            candidate = candidate[ForwardedForPrefix.Length..].Trim().Trim('"');
        }

        if (IPAddress.TryParse(candidate, out var parsedAddress))
        {
            ipAddress = NormalizeIpAddress(parsedAddress);
            return true;
        }

        if (IPEndPoint.TryParse(candidate, out var parsedEndpoint))
        {
            ipAddress = NormalizeIpAddress(parsedEndpoint.Address);
            return true;
        }

        return false;
    }

    private static IPAddress? NormalizeIpAddress(IPAddress? ipAddress)
    {
        if (ipAddress?.IsIPv4MappedToIPv6 == true)
        {
            return ipAddress.MapToIPv4();
        }

        return ipAddress;
    }
}
