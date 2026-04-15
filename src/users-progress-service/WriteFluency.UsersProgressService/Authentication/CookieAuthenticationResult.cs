using System.Security.Claims;

namespace WriteFluency.UsersProgressService.Authentication;

public sealed record CookieAuthenticationResult(
    bool IsAuthenticated,
    string? UserId,
    ClaimsPrincipal? Principal)
{
    public static CookieAuthenticationResult Unauthenticated() => new(false, null, null);

    public static CookieAuthenticationResult Authenticated(string userId, ClaimsPrincipal principal) =>
        new(true, userId, principal);
}
