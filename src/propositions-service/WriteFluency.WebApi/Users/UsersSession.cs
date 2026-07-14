namespace WriteFluency.WebApi.Users;

public sealed record UsersSession(
    string? UserId,
    bool IsAuthenticated,
    bool IsPro,
    DateTimeOffset? CurrentPeriodEndUtc)
{
    public static UsersSession Anonymous { get; } = new(
        UserId: null,
        IsAuthenticated: false,
        IsPro: false,
        CurrentPeriodEndUtc: null);
}
