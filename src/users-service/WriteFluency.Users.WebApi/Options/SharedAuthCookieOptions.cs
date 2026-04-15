namespace WriteFluency.Users.WebApi.Options;

public sealed class SharedAuthCookieOptions
{
    public const string SectionName = "SharedAuthCookie";

    public string Scheme { get; set; } = "Identity.Application";

    public string CookieName { get; set; } = ".AspNetCore.Identity.Application";

    public string CookieDomain { get; set; } = ".writefluency.com";
}
