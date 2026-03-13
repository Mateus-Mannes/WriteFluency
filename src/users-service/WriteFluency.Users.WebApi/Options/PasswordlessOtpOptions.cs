namespace WriteFluency.Users.WebApi.Options;

public class PasswordlessOtpOptions
{
    public const string SectionName = "PasswordlessOtp";

    public int CodeLength { get; init; } = 6;
    public int TtlMinutes { get; init; } = 10;
    public int MaxVerifyAttempts { get; init; } = 5;
    public int MaxRequestsPerWindowPerEmail { get; init; } = 3;
    public int MaxRequestsPerWindowPerIp { get; init; } = 20;
    public int RequestWindowMinutes { get; init; } = 15;
    public int MinimumSecondsBetweenRequestsPerEmail { get; init; } = 30;
}
