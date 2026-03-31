using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using Microsoft.Extensions.Options;
using WriteFluency.Users.WebApi.Options;

namespace WriteFluency.Users.WebApi.Email;

public class SmtpAppEmailSender : IAppEmailSender
{
    private static readonly Meter EmailMeter = new("WriteFluency.Users.Email", "1.0.0");
    private static readonly Counter<long> SendAttemptsCounter = EmailMeter.CreateCounter<long>("wf_email_send_attempts_total");
    private static readonly Counter<long> SendSucceededCounter = EmailMeter.CreateCounter<long>("wf_email_send_succeeded_total");
    private static readonly Counter<long> SendFailedCounter = EmailMeter.CreateCounter<long>("wf_email_send_failed_total");

    private readonly SmtpOptions _smtpOptions;
    private readonly ILogger<SmtpAppEmailSender> _logger;

    public SmtpAppEmailSender(IOptions<SmtpOptions> smtpOptions, ILogger<SmtpAppEmailSender> logger)
    {
        _smtpOptions = smtpOptions.Value;
        _logger = logger;
    }

    public async Task SendAsync(string toEmail, string subject, string htmlBody, string textBody, CancellationToken cancellationToken = default)
    {
        var emailType = ResolveEmailType(subject);
        var tags = new TagList
        {
            { "email_type", emailType }
        };

        SendAttemptsCounter.Add(1, tags);

        using var client = new SmtpClient(_smtpOptions.Host, _smtpOptions.Port)
        {
            EnableSsl = _smtpOptions.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = string.IsNullOrWhiteSpace(_smtpOptions.Username)
        };

        if (!string.IsNullOrWhiteSpace(_smtpOptions.Username))
        {
            client.Credentials = new NetworkCredential(_smtpOptions.Username, _smtpOptions.Password);
        }

        using var message = BuildMailMessage(toEmail, subject, htmlBody, textBody);

        _logger.LogInformation(
            "Sending {EmailType} email via SMTP host {Host}:{Port} to {ToEmail}",
            emailType,
            _smtpOptions.Host,
            _smtpOptions.Port,
            toEmail);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await client.SendMailAsync(message, cancellationToken);
            SendSucceededCounter.Add(1, tags);
        }
        catch (SmtpException ex)
        {
            var failureType = ResolveFailureType(ex.StatusCode);
            SendFailedCounter.Add(1, new TagList
            {
                { "email_type", emailType },
                { "failure_type", failureType }
            });

            _logger.LogWarning(
                ex,
                "SMTP send failed for {EmailType} email to {ToEmail}. StatusCode={StatusCode}, FailureType={FailureType}",
                emailType,
                toEmail,
                ex.StatusCode,
                failureType);

            throw;
        }
        catch (Exception ex)
        {
            SendFailedCounter.Add(1, new TagList
            {
                { "email_type", emailType },
                { "failure_type", "unknown" }
            });

            _logger.LogError(ex, "Email send failed unexpectedly for {EmailType} email to {ToEmail}", emailType, toEmail);
            throw;
        }
    }

    internal MailMessage BuildMailMessage(string toEmail, string subject, string htmlBody, string textBody)
    {
        var plainTextBody = string.IsNullOrWhiteSpace(textBody)
            ? BuildFallbackTextBody(htmlBody)
            : textBody;

        var message = new MailMessage
        {
            From = new MailAddress(_smtpOptions.FromEmail, _smtpOptions.FromName),
            Subject = subject,
            SubjectEncoding = Encoding.UTF8,
            BodyEncoding = Encoding.UTF8,
            HeadersEncoding = Encoding.UTF8,
            IsBodyHtml = false,
            Body = plainTextBody
        };

        message.To.Add(toEmail);

        if (!string.IsNullOrWhiteSpace(_smtpOptions.ReplyToEmail))
        {
            message.ReplyToList.Add(new MailAddress(_smtpOptions.ReplyToEmail));
        }

        if (!string.IsNullOrWhiteSpace(_smtpOptions.EnvelopeFrom))
        {
            message.Sender = new MailAddress(_smtpOptions.EnvelopeFrom, _smtpOptions.FromName);
        }

        var messageIdDomain = _smtpOptions.MessageIdDomain.Trim();
        if (!string.IsNullOrWhiteSpace(messageIdDomain))
        {
            message.Headers.Add("Message-ID", $"<{Guid.NewGuid():N}@{messageIdDomain}>");
        }

        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(plainTextBody, Encoding.UTF8, MediaTypeNames.Text.Plain));
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(htmlBody, Encoding.UTF8, MediaTypeNames.Text.Html));

        return message;
    }

    private static string ResolveEmailType(string subject)
    {
        if (subject.Contains("sign-in code", StringComparison.OrdinalIgnoreCase))
        {
            return "otp";
        }

        if (subject.Contains("confirm", StringComparison.OrdinalIgnoreCase))
        {
            return "confirmation";
        }

        if (subject.Contains("reset", StringComparison.OrdinalIgnoreCase))
        {
            return "password_reset";
        }

        return "other";
    }

    private static string ResolveFailureType(SmtpStatusCode statusCode)
    {
        var numericCode = (int)statusCode;

        if (numericCode is >= 400 and < 500)
        {
            return "temporary";
        }

        if (numericCode is >= 500 and < 600)
        {
            return "permanent";
        }

        return "unknown";
    }

    private static string BuildFallbackTextBody(string htmlBody)
    {
        return string.IsNullOrWhiteSpace(htmlBody)
            ? string.Empty
            : System.Text.RegularExpressions.Regex.Replace(htmlBody, "<[^>]+>", " ").Trim();
    }
}
