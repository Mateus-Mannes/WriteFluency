using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using WriteFluency.Users.WebApi.Authentication;
using WriteFluency.Users.WebApi.Data;

namespace WriteFluency.Users.IntegrationTests.Infrastructure;

internal sealed class TestingExternalLoginInfoResolver : IExternalLoginInfoResolver
{
    private const string LoginProviderItemName = "LoginProvider";

    public async Task<ExternalLoginInfo?> GetExternalLoginInfoAsync(
        SignInManager<ApplicationUser> signInManager,
        HttpContext httpContext)
    {
        var externalLoginInfo = await signInManager.GetExternalLoginInfoAsync();
        if (externalLoginInfo is not null)
        {
            return externalLoginInfo;
        }

        var authenticateResult = await httpContext.AuthenticateAsync(IdentityConstants.ExternalScheme);
        if (!authenticateResult.Succeeded || authenticateResult.Principal is null)
        {
            return BuildFromQuery(httpContext.Request.Query);
        }

        string? loginProvider = null;
        if (!(authenticateResult.Properties?.Items.TryGetValue(LoginProviderItemName, out loginProvider) ?? false))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(loginProvider))
        {
            return BuildFromQuery(httpContext.Request.Query);
        }

        var providerKey = authenticateResult.Principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? authenticateResult.Principal.FindFirstValue("sub");

        if (string.IsNullOrWhiteSpace(providerKey))
        {
            return BuildFromQuery(httpContext.Request.Query);
        }

        return new ExternalLoginInfo(
            authenticateResult.Principal,
            loginProvider,
            providerKey,
            loginProvider);
    }

    private static ExternalLoginInfo? BuildFromQuery(IQueryCollection query)
    {
        var testProvider = query["test_provider"].FirstOrDefault();
        var testProviderKey = query["test_provider_key"].FirstOrDefault();
        var testEmail = query["test_email"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(testProvider)
            || string.IsNullOrWhiteSpace(testProviderKey))
        {
            return null;
        }

        var emailVerifiedRaw = query["test_email_verified"].FirstOrDefault();
        var emailVerified = !string.Equals(emailVerifiedRaw, "false", StringComparison.OrdinalIgnoreCase);
        var missingEmail = string.Equals(query["test_missing_email"], "true", StringComparison.OrdinalIgnoreCase);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, testProviderKey, ClaimValueTypes.String, testProvider),
            new("email_verified", emailVerified ? "true" : "false", ClaimValueTypes.String, testProvider)
        };

        if (!missingEmail)
        {
            var email = string.IsNullOrWhiteSpace(testEmail) ? $"{testProviderKey}@writefluency.test" : testEmail;
            claims.Add(new Claim(ClaimTypes.Email, email, ClaimValueTypes.String, testProvider));
            claims.Add(new Claim("email", email, ClaimValueTypes.String, testProvider));
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, testProvider));
        return new ExternalLoginInfo(principal, testProvider, testProviderKey, testProvider);
    }
}
