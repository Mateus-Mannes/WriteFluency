using System.Net;
using WriteFluency.Users.WebApi.Email;

namespace WriteFluency.Users.WebApi.Support;

public static class SupportRequestEmailBuilder
{
    public static EmailContent Build(SupportRequestEmailModel model)
    {
        var textBody = string.Join(
            Environment.NewLine,
            [
                "WriteFluency support request",
                "",
                $"Submitted at UTC: {model.SubmittedAtUtc:O}",
                $"Reply email: {ValueOrNotProvided(model.ReplyEmail)}",
                $"Authenticated user ID: {ValueOrNotProvided(model.UserId)}",
                $"Authenticated user email: {ValueOrNotProvided(model.UserEmail)}",
                $"Source URL: {ValueOrNotProvided(model.SourceUrl)}",
                $"Client IP: {ValueOrNotProvided(model.ClientIp)}",
                "",
                "Message:",
                model.Message
            ]);

        var htmlBody = $$"""
            <!doctype html>
            <html lang="en">
              <body style="font-family: Arial, sans-serif; color: #1A1A1A; line-height: 1.5;">
                <h1 style="font-size: 20px; margin: 0 0 16px;">WriteFluency support request</h1>
                <table style="border-collapse: collapse; margin-bottom: 18px;">
                  <tr><th align="left" style="padding: 4px 12px 4px 0;">Submitted at UTC</th><td>{{HtmlEncode(model.SubmittedAtUtc.ToString("O"))}}</td></tr>
                  <tr><th align="left" style="padding: 4px 12px 4px 0;">Reply email</th><td>{{HtmlEncode(ValueOrNotProvided(model.ReplyEmail))}}</td></tr>
                  <tr><th align="left" style="padding: 4px 12px 4px 0;">Authenticated user ID</th><td>{{HtmlEncode(ValueOrNotProvided(model.UserId))}}</td></tr>
                  <tr><th align="left" style="padding: 4px 12px 4px 0;">Authenticated user email</th><td>{{HtmlEncode(ValueOrNotProvided(model.UserEmail))}}</td></tr>
                  <tr><th align="left" style="padding: 4px 12px 4px 0;">Source URL</th><td>{{HtmlEncode(ValueOrNotProvided(model.SourceUrl))}}</td></tr>
                  <tr><th align="left" style="padding: 4px 12px 4px 0;">Client IP</th><td>{{HtmlEncode(ValueOrNotProvided(model.ClientIp))}}</td></tr>
                </table>
                <h2 style="font-size: 16px; margin: 0 0 8px;">Message</h2>
                <pre style="white-space: pre-wrap; font-family: Arial, sans-serif; background: #F7FAFC; border: 1px solid #D9E2EC; border-radius: 8px; padding: 12px;">{{HtmlEncode(model.Message)}}</pre>
              </body>
            </html>
            """;

        return new EmailContent(htmlBody, textBody);
    }

    private static string ValueOrNotProvided(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "Not provided" : value;
    }

    private static string HtmlEncode(string value)
    {
        return WebUtility.HtmlEncode(value);
    }
}

public sealed record SupportRequestEmailModel(
    string Message,
    string? ReplyEmail,
    string? UserId,
    string? UserEmail,
    string? SourceUrl,
    string? ClientIp,
    DateTimeOffset SubmittedAtUtc);
