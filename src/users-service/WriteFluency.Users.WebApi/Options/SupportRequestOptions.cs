namespace WriteFluency.Users.WebApi.Options;

public class SupportRequestOptions
{
    public const string SectionName = "SupportRequest";

    public string[] RecipientEmails { get; init; } = [];
    public int MaxRequestsPerWindowPerIp { get; init; } = 3;
    public int RequestWindowMinutes { get; init; } = 15;
}
