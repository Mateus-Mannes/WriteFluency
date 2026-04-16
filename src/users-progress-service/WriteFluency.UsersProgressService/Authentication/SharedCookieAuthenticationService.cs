using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Options;
using WriteFluency.UsersProgressService.Options;

namespace WriteFluency.UsersProgressService.Authentication;

public sealed class SharedCookieAuthenticationService : ISharedCookieAuthenticationService
{
    private readonly TicketDataFormat _ticketDataFormat;
    private readonly SharedAuthCookieOptions _cookieOptions;
    private readonly ILogger<SharedCookieAuthenticationService> _logger;

    public SharedCookieAuthenticationService(
        IDataProtectionProvider dataProtectionProvider,
        IOptions<SharedAuthCookieOptions> cookieOptions,
        ILogger<SharedCookieAuthenticationService> logger)
    {
        _cookieOptions = cookieOptions.Value;
        _logger = logger;

        var protector = dataProtectionProvider.CreateProtector(
            "Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationMiddleware",
            _cookieOptions.Scheme,
            "v2");

        _ticketDataFormat = new TicketDataFormat(protector);
    }

    public CookieAuthenticationResult Authenticate(HttpRequestData request)
    {
        var cookieValue = TryReadCookieValue(request, _cookieOptions.CookieName);
        if (string.IsNullOrWhiteSpace(cookieValue))
        {
            _logger.LogDebug(
                "Shared authentication cookie not found. CookieName={CookieName}.",
                _cookieOptions.CookieName);

            return CookieAuthenticationResult.Unauthenticated();
        }

        AuthenticationTicket? ticket;
        try
        {
            ticket = _ticketDataFormat.Unprotect(cookieValue);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Unable to unprotect shared authentication cookie.");
            return CookieAuthenticationResult.Unauthenticated();
        }

        if (ticket?.Principal?.Identity?.IsAuthenticated != true)
        {
            _logger.LogDebug("Shared authentication cookie ticket is not authenticated.");
            return CookieAuthenticationResult.Unauthenticated();
        }

        if (ticket.Properties.ExpiresUtc.HasValue
            && ticket.Properties.ExpiresUtc.Value <= DateTimeOffset.UtcNow)
        {
            _logger.LogDebug(
                "Shared authentication cookie ticket is expired. ExpiresUtc={ExpiresUtc}.",
                ticket.Properties.ExpiresUtc.Value);

            return CookieAuthenticationResult.Unauthenticated();
        }

        var principal = ticket.Principal;
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub");

        if (string.IsNullOrWhiteSpace(userId))
        {
            _logger.LogDebug("Shared authentication cookie ticket is missing user id claim.");
            return CookieAuthenticationResult.Unauthenticated();
        }

        _logger.LogDebug(
            "Shared authentication cookie validated. UserId={UserId}.",
            userId);

        return CookieAuthenticationResult.Authenticated(userId, principal);
    }

    private static string? TryReadCookieValue(HttpRequestData request, string cookieName)
    {
        var parsedCookies = ParseCookies(request);
        if (!parsedCookies.TryGetValue(cookieName, out var cookieValue) || string.IsNullOrWhiteSpace(cookieValue))
        {
            return null;
        }

        if (!cookieValue.StartsWith("chunks-", StringComparison.OrdinalIgnoreCase))
        {
            return cookieValue;
        }

        var suffix = cookieValue["chunks-".Length..];
        if (!int.TryParse(suffix, NumberStyles.Integer, CultureInfo.InvariantCulture, out var chunkCount)
            || chunkCount <= 0)
        {
            return null;
        }

        var chunks = new string[chunkCount];
        for (var index = 1; index <= chunkCount; index++)
        {
            var chunkName = $"{cookieName}C{index}";
            if (!parsedCookies.TryGetValue(chunkName, out var chunkValue) || string.IsNullOrWhiteSpace(chunkValue))
            {
                return null;
            }

            chunks[index - 1] = chunkValue;
        }

        return string.Concat(chunks);
    }

    private static Dictionary<string, string> ParseCookies(HttpRequestData request)
    {
        var cookies = new Dictionary<string, string>(StringComparer.Ordinal);

        if (!request.Headers.TryGetValues("Cookie", out var cookieHeaders))
        {
            return cookies;
        }

        foreach (var headerValue in cookieHeaders)
        {
            if (string.IsNullOrWhiteSpace(headerValue))
            {
                continue;
            }

            var segments = headerValue.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var segment in segments)
            {
                var separatorIndex = segment.IndexOf('=');
                if (separatorIndex <= 0)
                {
                    continue;
                }

                var key = segment[..separatorIndex].Trim();
                var value = segment[(separatorIndex + 1)..].Trim();
                if (key.Length == 0)
                {
                    continue;
                }

                cookies[key] = value;
            }
        }

        return cookies;
    }
}
