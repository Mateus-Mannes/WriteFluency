using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using WriteFluency.Users.WebApi.Email;
using WriteFluency.Users.WebApi.Options;

namespace WriteFluency.Users.Tests.Email;

public class SmtpAppEmailSenderTests
{
    [Fact]
    public void BuildMailMessage_ShouldCreateMultipartMessageWithStableHeaders()
    {
        var sender = CreateSender(new SmtpOptions
        {
            Host = "smtp.local",
            Port = 2525,
            FromEmail = "noreply@writefluency.com",
            FromName = "WriteFluency",
            ReplyToEmail = "support@writefluency.com",
            EnvelopeFrom = "bounce@writefluency.com",
            MessageIdDomain = "writefluency.com"
        });

        using var message = sender.BuildMailMessage(
            "user@writefluency.com",
            "Your WriteFluency sign-in code",
            "<p>Hello <strong>123456</strong></p>",
            "Hello 123456");

        message.AlternateViews.Count.ShouldBe(2);
        message.AlternateViews.Any(v => v.ContentType.MediaType == "text/plain").ShouldBeTrue();
        message.AlternateViews.Any(v => v.ContentType.MediaType == "text/html").ShouldBeTrue();
        message.ReplyToList.Single().Address.ShouldBe("support@writefluency.com");
        message.Sender.ShouldNotBeNull();
        message.Sender.Address.ShouldBe("bounce@writefluency.com");
        message.Headers["Message-ID"].ShouldNotBeNullOrWhiteSpace();
        message.Headers["Message-ID"]!.ShouldContain("@writefluency.com>");
    }

    [Fact]
    public void BuildMailMessage_WhenTextBodyIsEmpty_ShouldFallbackToStrippedHtml()
    {
        var sender = CreateSender(new SmtpOptions
        {
            Host = "smtp.local",
            Port = 2525,
            FromEmail = "noreply@writefluency.com",
            FromName = "WriteFluency",
            MessageIdDomain = "writefluency.com"
        });

        using var message = sender.BuildMailMessage(
            "user@writefluency.com",
            "Confirm your WriteFluency email",
            "<p>Confirm your email now</p>",
            string.Empty);

        message.Body.ShouldContain("Confirm your email now");
        message.AlternateViews.Count.ShouldBe(2);
    }

    private static SmtpAppEmailSender CreateSender(SmtpOptions options)
    {
        return new SmtpAppEmailSender(Options.Create(options), NullLogger<SmtpAppEmailSender>.Instance);
    }
}
