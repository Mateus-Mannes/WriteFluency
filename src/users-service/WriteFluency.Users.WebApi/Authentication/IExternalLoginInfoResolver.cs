using Microsoft.AspNetCore.Identity;
using WriteFluency.Users.WebApi.Data;

namespace WriteFluency.Users.WebApi.Authentication;

public interface IExternalLoginInfoResolver
{
    Task<ExternalLoginInfo?> GetExternalLoginInfoAsync(
        SignInManager<ApplicationUser> signInManager,
        HttpContext httpContext);
}

internal sealed class DefaultExternalLoginInfoResolver : IExternalLoginInfoResolver
{
    public Task<ExternalLoginInfo?> GetExternalLoginInfoAsync(
        SignInManager<ApplicationUser> signInManager,
        HttpContext httpContext)
    {
        return signInManager.GetExternalLoginInfoAsync();
    }
}
