using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using WriteFluency.Users.WebApi.Options;

namespace WriteFluency.Users.WebApi.Email;

public class SmtpAppEmailSender : IAppEmailSender
{
    private readonly SmtpOptions _smtpOptions;
    private readonly ILogger<SmtpAppEmailSender> _logger;

    public SmtpAppEmailSender(IOptions<SmtpOptions> smtpOptions, ILogger<SmtpAppEmailSender> logger)
    {
        _smtpOptions = smtpOptions.Value;
        _logger = logger;
    }

    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
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

        using var message = new MailMessage
        {
            From = new MailAddress(_smtpOptions.FromEmail, _smtpOptions.FromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };

        message.To.Add(toEmail);

        _logger.LogInformation("Sending email via SMTP host {Host}:{Port} to {ToEmail}",
            _smtpOptions.Host,
            _smtpOptions.Port,
            toEmail);

        cancellationToken.ThrowIfCancellationRequested();
        await client.SendMailAsync(message, cancellationToken);
    }
}
