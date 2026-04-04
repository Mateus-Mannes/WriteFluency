using Microsoft.Extensions.Options;
using Shouldly;
using WriteFluency.Users.WebApi.Data;
using WriteFluency.Users.WebApi.Email;
using WriteFluency.Users.WebApi.Options;

namespace WriteFluency.Users.Tests.Email;

public class IdentityEmailSenderTests
{
    [Fact]
    public async Task SendConfirmationLinkAsync_ShouldRewriteLinkToWebappRoute()
    {
        var appEmailSender = new CapturingAppEmailSender();
        var sender = BuildSender(appEmailSender, "https://writefluency.com/auth/confirm-email");
        var originalLink = "https://api.writefluency.com/users/auth/confirmEmail?userId=user-123&code=abc%2Bdef";

        await sender.SendConfirmationLinkAsync(new ApplicationUser(), "user@writefluency.test", originalLink);

        appEmailSender.LastMessage.ShouldNotBeNull();
        appEmailSender.LastMessage!.HtmlBody.ShouldContain("https://writefluency.com/auth/confirm-email?userId=user-123&code=abc%2Bdef");
        appEmailSender.LastMessage.TextBody.ShouldContain("https://writefluency.com/auth/confirm-email?userId=user-123&code=abc%2Bdef");
    }

    [Fact]
    public async Task SendConfirmationLinkAsync_WhenLinkCannotBeParsed_ShouldKeepOriginalLink()
    {
        var appEmailSender = new CapturingAppEmailSender();
        var sender = BuildSender(appEmailSender, "https://writefluency.com/auth/confirm-email");
        const string originalLink = "not-a-valid-absolute-url";

        await sender.SendConfirmationLinkAsync(new ApplicationUser(), "user@writefluency.test", originalLink);

        appEmailSender.LastMessage.ShouldNotBeNull();
        appEmailSender.LastMessage!.HtmlBody.ShouldContain(originalLink);
        appEmailSender.LastMessage.TextBody.ShouldContain(originalLink);
    }

    [Fact]
    public async Task SendConfirmationLinkAsync_WhenLinkContainsHtmlEncodedAmpersand_ShouldRewriteLink()
    {
        var appEmailSender = new CapturingAppEmailSender();
        var sender = BuildSender(appEmailSender, "https://writefluency.com/auth/confirm-email");
        const string originalLink = "https://localhost:5101/users/auth/confirmEmail?userId=user-123&amp;code=abc%2Bdef";

        await sender.SendConfirmationLinkAsync(new ApplicationUser(), "user@writefluency.test", originalLink);

        appEmailSender.LastMessage.ShouldNotBeNull();
        appEmailSender.LastMessage!.HtmlBody.ShouldContain("https://writefluency.com/auth/confirm-email?userId=user-123&code=abc%2Bdef");
        appEmailSender.LastMessage.TextBody.ShouldContain("https://writefluency.com/auth/confirm-email?userId=user-123&code=abc%2Bdef");
    }

    [Fact]
    public async Task SendConfirmationLinkAsync_WhenLinkIsRelative_ShouldRewriteLink()
    {
        var appEmailSender = new CapturingAppEmailSender();
        var sender = BuildSender(appEmailSender, "https://writefluency.com/auth/confirm-email");
        const string originalLink = "/users/auth/confirmEmail?userId=user-123&code=abc%2Bdef";

        await sender.SendConfirmationLinkAsync(new ApplicationUser(), "user@writefluency.test", originalLink);

        appEmailSender.LastMessage.ShouldNotBeNull();
        appEmailSender.LastMessage!.HtmlBody.ShouldContain("https://writefluency.com/auth/confirm-email?userId=user-123&code=abc%2Bdef");
        appEmailSender.LastMessage.TextBody.ShouldContain("https://writefluency.com/auth/confirm-email?userId=user-123&code=abc%2Bdef");
    }

    private static IdentityEmailSender BuildSender(CapturingAppEmailSender appEmailSender, string confirmationRedirectUrl)
    {
        var options = Options.Create(new ExternalAuthenticationOptions
        {
            Google = new ProviderOptions
            {
                ClientId = "google-client-id",
                ClientSecret = "google-client-secret"
            },
            Microsoft = new ProviderOptions
            {
                ClientId = "microsoft-client-id",
                ClientSecret = "microsoft-client-secret"
            },
            ConfirmationRedirectUrl = confirmationRedirectUrl,
            ExternalRedirect = new RedirectOptions
            {
                AllowedReturnUrls = ["/users/swagger/index.html"]
            }
        });

        return new IdentityEmailSender(appEmailSender, options);
    }

    private sealed class CapturingAppEmailSender : IAppEmailSender
    {
        public CapturedMessage? LastMessage { get; private set; }

        public Task SendAsync(
            string toEmail,
            string subject,
            string htmlBody,
            string textBody,
            CancellationToken cancellationToken = default)
        {
            LastMessage = new CapturedMessage(toEmail, subject, htmlBody, textBody);
            return Task.CompletedTask;
        }
    }

    private sealed record CapturedMessage(string ToEmail, string Subject, string HtmlBody, string TextBody);
}
