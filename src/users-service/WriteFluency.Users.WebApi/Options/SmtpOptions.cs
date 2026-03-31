namespace WriteFluency.Users.WebApi.Options;

public class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string Host { get; init; } = "wf-infra-smtp";
    public int Port { get; init; } = 25;
    public string? Username { get; init; }
    public string? Password { get; init; }
    public string FromEmail { get; init; } = "noreply@writefluency.com";
    public string FromName { get; init; } = "WriteFluency";
    public string? ReplyToEmail { get; init; }
    public string? EnvelopeFrom { get; init; }
    public string MessageIdDomain { get; init; } = "writefluency.com";
    public bool EnableSsl { get; init; }
}
