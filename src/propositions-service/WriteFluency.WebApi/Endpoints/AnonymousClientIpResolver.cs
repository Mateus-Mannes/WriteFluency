using System.Net;
using Microsoft.Extensions.Primitives;

namespace WriteFluency.WebApi.Endpoints;

internal static class AnonymousClientIpResolver
{
    private const string XForwardedForHeaderName = "X-Forwarded-For";
    private const string ForwardedHeaderName = "Forwarded";
    private const string ForwardedForPrefix = "for=";

    public static string? Resolve(HttpRequest request)
    {
        if (TryParseForwardedForHeader(request.Headers[XForwardedForHeaderName], out var ipAddress)
            || TryParseForwardedHeader(request.Headers[ForwardedHeaderName], out ipAddress))
        {
            return ipAddress?.ToString();
        }

        return NormalizeIpAddress(request.HttpContext.Connection.RemoteIpAddress)?.ToString();
    }

    private static bool TryParseForwardedForHeader(StringValues values, out IPAddress? ipAddress)
    {
        ipAddress = null;
        foreach (var headerValue in values)
        {
            if (string.IsNullOrWhiteSpace(headerValue))
            {
                continue;
            }

            foreach (var entry in headerValue.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                if (TryParseIpAddress(entry, out ipAddress))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryParseForwardedHeader(StringValues values, out IPAddress? ipAddress)
    {
        ipAddress = null;
        foreach (var headerValue in values)
        {
            if (string.IsNullOrWhiteSpace(headerValue))
            {
                continue;
            }

            foreach (var forwardedElement in headerValue.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                foreach (var parameter in forwardedElement.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                {
                    if (parameter.StartsWith(ForwardedForPrefix, StringComparison.OrdinalIgnoreCase)
                        && TryParseIpAddress(parameter, out ipAddress))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool TryParseIpAddress(string rawValue, out IPAddress? ipAddress)
    {
        ipAddress = null;
        var candidate = rawValue.Trim();
        if (candidate.StartsWith(ForwardedForPrefix, StringComparison.OrdinalIgnoreCase))
        {
            candidate = candidate[ForwardedForPrefix.Length..].Trim();
        }

        candidate = candidate.Trim('"');
        if (candidate.StartsWith("[", StringComparison.Ordinal) && candidate.Contains(']', StringComparison.Ordinal))
        {
            candidate = candidate[1..candidate.IndexOf(']', StringComparison.Ordinal)];
        }

        if (IPAddress.TryParse(candidate, out var parsedAddress))
        {
            ipAddress = NormalizeIpAddress(parsedAddress);
            return ipAddress is not null;
        }

        if (IPEndPoint.TryParse(candidate, out var parsedEndpoint))
        {
            ipAddress = NormalizeIpAddress(parsedEndpoint.Address);
            return ipAddress is not null;
        }

        var lastColonIndex = candidate.LastIndexOf(':');
        if (lastColonIndex > 0
            && candidate.IndexOf(':') == lastColonIndex
            && int.TryParse(candidate[(lastColonIndex + 1)..], out _)
            && IPAddress.TryParse(candidate[..lastColonIndex], out parsedAddress))
        {
            ipAddress = NormalizeIpAddress(parsedAddress);
            return ipAddress is not null;
        }

        return false;
    }

    private static IPAddress? NormalizeIpAddress(IPAddress? ipAddress)
    {
        if (ipAddress is null)
        {
            return null;
        }

        return ipAddress.IsIPv4MappedToIPv6
            ? ipAddress.MapToIPv4()
            : ipAddress;
    }
}

public sealed record AnonymousClientFingerprint(
    string Hash,
    string IpAddress);
