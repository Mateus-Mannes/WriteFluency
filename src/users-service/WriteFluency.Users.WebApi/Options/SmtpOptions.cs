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
    public bool EnableSsl { get; init; }
}
