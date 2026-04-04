using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using System.Net;
using WriteFluency.Users.WebApi.Data;
using WriteFluency.Users.WebApi.Options;

namespace WriteFluency.Users.WebApi.Email;

public class IdentityEmailSender : IEmailSender<ApplicationUser>
{
    private readonly IAppEmailSender _emailSender;
    private readonly string _confirmationRedirectUrl;

    public IdentityEmailSender(IAppEmailSender emailSender, IOptions<ExternalAuthenticationOptions> authenticationOptions)
    {
        _emailSender = emailSender;
        _confirmationRedirectUrl = authenticationOptions.Value.ConfirmationRedirectUrl;
    }

    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink)
    {
        var redirectLink = BuildConfirmationRedirectLink(confirmationLink);
        var content = EmailTemplateBuilder.BuildConfirmationEmail(redirectLink);

        return _emailSender.SendAsync(email, "Confirm your WriteFluency email", content.HtmlBody, content.TextBody);
    }

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink)
    {
        var content = EmailTemplateBuilder.BuildPasswordResetLinkEmail(resetLink);

        return _emailSender.SendAsync(email, "Reset your WriteFluency password", content.HtmlBody, content.TextBody);
    }

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode)
    {
        var content = EmailTemplateBuilder.BuildPasswordResetCodeEmail(resetCode);

        return _emailSender.SendAsync(email, "Your WriteFluency reset code", content.HtmlBody, content.TextBody);
    }

    private string BuildConfirmationRedirectLink(string confirmationLink)
    {
        var decodedLink = WebUtility.HtmlDecode(confirmationLink).Trim();
        var query = ExtractQueryString(decodedLink);
        if (string.IsNullOrWhiteSpace(query))
        {
            return confirmationLink;
        }

        var parsedQuery = QueryHelpers.ParseQuery(query);
        var userId = GetQueryValue(parsedQuery, "userId");
        var code = GetQueryValue(parsedQuery, "code");
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(code))
        {
            return confirmationLink;
        }

        var redirectLink = QueryHelpers.AddQueryString(_confirmationRedirectUrl, "userId", userId);
        return QueryHelpers.AddQueryString(redirectLink, "code", code);
    }

    private static string? ExtractQueryString(string link)
    {
        if (Uri.TryCreate(link, UriKind.Absolute, out var absoluteUri)
            && (string.Equals(absoluteUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                || string.Equals(absoluteUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            return absoluteUri.Query;
        }

        var queryIndex = link.IndexOf('?', StringComparison.Ordinal);
        if (queryIndex < 0)
        {
            return null;
        }

        return link[queryIndex..];
    }

    private static string? GetQueryValue(IDictionary<string, Microsoft.Extensions.Primitives.StringValues> query, string key)
    {
        foreach (var entry in query)
        {
            var normalizedKey = NormalizeQueryKey(entry.Key);
            if (!string.Equals(normalizedKey, key, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = entry.Value.ToString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string NormalizeQueryKey(string key)
    {
        var normalized = key;
        while (normalized.StartsWith("amp;", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[4..];
        }

        return normalized;
    }
}
