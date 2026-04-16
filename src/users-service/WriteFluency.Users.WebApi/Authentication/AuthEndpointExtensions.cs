using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using WriteFluency.Users.WebApi.Data;
using WriteFluency.Users.WebApi.Options;

namespace WriteFluency.Users.WebApi.Authentication;

public static class AuthEndpointExtensions
{
    private const string UsersAuthBasePath = "/users/auth";

    private static readonly Dictionary<string, ExternalProviderDefinition> ExternalProviders = new(StringComparer.OrdinalIgnoreCase)
    {
        ["google"] = new("google", GoogleDefaults.AuthenticationScheme, "Google"),
        ["microsoft"] = new("microsoft", MicrosoftAccountDefaults.AuthenticationScheme, "Microsoft")
    };

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var authGroup = app.MapGroup("/auth").WithTags("Authentication");

        authGroup.MapIdentityApi<ApplicationUser>();

        authGroup.MapPost("/logout", LogoutAsync)
            .RequireAuthorization();

        authGroup.MapGet("/session", GetSession)
            .RequireAuthorization();

        authGroup.MapPost("/passwordless/request", RequestPasswordlessOtpAsync);

        authGroup.MapPost("/passwordless/verify", VerifyPasswordlessOtpAsync);

        authGroup.MapGet("/external/providers", GetExternalProvidersAsync)
            .WithSummary("List available social login providers")
            .WithDescription("Returns enabled external providers with start endpoints for social login.");

        authGroup.MapGet("/external/{provider}/start", StartExternalLoginAsync)
            .WithSummary("Start social login challenge")
            .WithDescription("Starts Google or Microsoft login and redirects to provider. Use returnUrl=/users/swagger/index.html for Swagger local flow.");

        authGroup.MapGet("/external/{provider}/callback", CompleteExternalLoginAsync)
            .WithSummary("Complete social login callback")
            .WithDescription("Completes external auth, applies account linking rules, issues cookie session, and redirects with auth status query params.");

        return app;
    }

    private static async Task<IResult> LogoutAsync(SignInManager<ApplicationUser> signInManager, [FromBody] object payload)
    {
        if (payload is null)
        {
            return Results.BadRequest();
        }

        await signInManager.SignOutAsync();
        return Results.Ok();
    }

    private static async Task<IResult> GetSession(
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        HttpContext httpContext)
    {
        var user = await userManager.GetUserAsync(principal);
        var authenticateResult = await httpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        var issuedAtUtc = authenticateResult.Properties?.IssuedUtc;
        var expiresAtUtc = authenticateResult.Properties?.ExpiresUtc;

        return Results.Ok(new
        {
            IsAuthenticated = principal.Identity?.IsAuthenticated ?? false,
            UserId = user?.Id ?? principal.FindFirstValue(ClaimTypes.NameIdentifier),
            Email = user?.Email ?? principal.FindFirstValue(ClaimTypes.Email),
            EmailConfirmed = user?.EmailConfirmed ?? false,
            IssuedAtUtc = issuedAtUtc,
            ExpiresAtUtc = expiresAtUtc
        });
    }

    private static async Task<IResult> RequestPasswordlessOtpAsync(
        PasswordlessRequest request,
        HttpContext httpContext,
        PasswordlessOtpService passwordlessOtpService,
        CancellationToken cancellationToken)
    {
        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        await passwordlessOtpService.RequestOtpAsync(request.Email, ipAddress, cancellationToken);

        return Results.Ok(new
        {
            Message = "If the account is eligible, a verification code was sent."
        });
    }

    private static async Task<IResult> VerifyPasswordlessOtpAsync(
        PasswordlessVerifyRequest request,
        PasswordlessOtpService passwordlessOtpService,
        CancellationToken cancellationToken)
    {
        var verified = await passwordlessOtpService.VerifyOtpAndSignInAsync(request.Email, request.Code, cancellationToken);
        return verified ? Results.Ok() : Results.Unauthorized();
    }

    private static async Task<IResult> GetExternalProvidersAsync(IAuthenticationSchemeProvider authenticationSchemeProvider)
    {
        var providers = new List<ExternalProviderResponse>();

        foreach (var provider in ExternalProviders.Values)
        {
            var scheme = await authenticationSchemeProvider.GetSchemeAsync(provider.SchemeName);
            if (scheme is null)
            {
                continue;
            }

            providers.Add(new ExternalProviderResponse(
                provider.Id,
                provider.DisplayName,
                $"{UsersAuthBasePath}/external/{provider.Id}/start?returnUrl=%2Fusers%2Fswagger%2Findex.html"));
        }

        return Results.Ok(providers);
    }

    private static async Task<IResult> StartExternalLoginAsync(
        string provider,
        [FromQuery] string? returnUrl,
        IAuthenticationSchemeProvider authenticationSchemeProvider,
        SignInManager<ApplicationUser> signInManager,
        IOptions<ExternalAuthenticationOptions> externalAuthenticationOptions)
    {
        if (!TryGetProvider(provider, out var providerDefinition))
        {
            return Results.BadRequest(new { Error = "provider_not_supported" });
        }

        var scheme = await authenticationSchemeProvider.GetSchemeAsync(providerDefinition.SchemeName);
        if (scheme is null)
        {
            return Results.BadRequest(new { Error = "provider_not_enabled" });
        }

        var authOptions = externalAuthenticationOptions.Value;
        if (!TryResolveReturnUrl(returnUrl, authOptions.ExternalRedirect, out var resolvedReturnUrl))
        {
            return Results.BadRequest(new { Error = "invalid_return_url" });
        }

        var callbackUri = QueryHelpers.AddQueryString(
            $"{UsersAuthBasePath}/external/{providerDefinition.Id}/callback",
            "returnUrl",
            resolvedReturnUrl);

        var properties = signInManager.ConfigureExternalAuthenticationProperties(providerDefinition.SchemeName, callbackUri);
        properties.Items[ExternalAuthConstants.ReturnUrlItemName] = resolvedReturnUrl;

        return Results.Challenge(properties, [providerDefinition.SchemeName]);
    }

    private static async Task<IResult> CompleteExternalLoginAsync(
        string provider,
        [FromQuery] string? returnUrl,
        [FromQuery(Name = ExternalAuthConstants.CallbackErrorCodeQueryName)] string? externalErrorCode,
        HttpContext httpContext,
        IAuthenticationSchemeProvider authenticationSchemeProvider,
        SignInManager<ApplicationUser> signInManager,
        IExternalLoginInfoResolver externalLoginInfoResolver,
        UserManager<ApplicationUser> userManager,
        IOptions<ExternalAuthenticationOptions> externalAuthenticationOptions)
    {
        if (!TryGetProvider(provider, out var providerDefinition))
        {
            return Results.BadRequest(new { Error = "provider_not_supported" });
        }

        if (!TryResolveReturnUrl(returnUrl, externalAuthenticationOptions.Value.ExternalRedirect, out var resolvedReturnUrl))
        {
            return Results.BadRequest(new { Error = "invalid_return_url" });
        }

        var scheme = await authenticationSchemeProvider.GetSchemeAsync(providerDefinition.SchemeName);
        if (scheme is null)
        {
            return Results.Redirect(BuildRedirectResult(resolvedReturnUrl, providerDefinition.Id, "provider_not_enabled"));
        }

        if (!string.IsNullOrWhiteSpace(externalErrorCode))
        {
            return Results.Redirect(BuildRedirectResult(resolvedReturnUrl, providerDefinition.Id, externalErrorCode));
        }

        var externalLoginInfo = await externalLoginInfoResolver.GetExternalLoginInfoAsync(signInManager, httpContext);

        if (externalLoginInfo is null)
        {
            return Results.Redirect(BuildRedirectResult(resolvedReturnUrl, providerDefinition.Id, "invalid_state"));
        }

        if (!string.Equals(externalLoginInfo.LoginProvider, providerDefinition.SchemeName, StringComparison.OrdinalIgnoreCase))
        {
            return Results.Redirect(BuildRedirectResult(resolvedReturnUrl, providerDefinition.Id, "provider_mismatch"));
        }

        var existingLoginSignIn = await signInManager.ExternalLoginSignInAsync(
            externalLoginInfo.LoginProvider,
            externalLoginInfo.ProviderKey,
            isPersistent: false,
            bypassTwoFactor: true);

        if (existingLoginSignIn.Succeeded)
        {
            await signInManager.UpdateExternalAuthenticationTokensAsync(externalLoginInfo);
            await httpContext.SignOutAsync(IdentityConstants.ExternalScheme);
            return Results.Redirect(BuildRedirectResult(resolvedReturnUrl, providerDefinition.Id, isSuccess: true));
        }

        if (existingLoginSignIn.IsLockedOut)
        {
            return Results.Redirect(BuildRedirectResult(resolvedReturnUrl, providerDefinition.Id, "account_locked"));
        }

        var email = externalLoginInfo.Principal.FindFirstValue(ClaimTypes.Email)
            ?? externalLoginInfo.Principal.FindFirstValue("email");

        if (string.IsNullOrWhiteSpace(email))
        {
            return Results.Redirect(BuildRedirectResult(resolvedReturnUrl, providerDefinition.Id, "provider_email_missing"));
        }

        if (!IsEmailVerified(providerDefinition.Id, externalLoginInfo.Principal))
        {
            return Results.Redirect(BuildRedirectResult(resolvedReturnUrl, providerDefinition.Id, "provider_email_unverified"));
        }

        var user = await userManager.FindByEmailAsync(email);
        var userCreatedInThisFlow = false;

        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true
            };

            var createResult = await userManager.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                return Results.Redirect(BuildRedirectResult(resolvedReturnUrl, providerDefinition.Id, "account_provisioning_failed"));
            }

            userCreatedInThisFlow = true;
        }
        else if (!user.EmailConfirmed)
        {
            return Results.Redirect(BuildRedirectResult(resolvedReturnUrl, providerDefinition.Id, "linking_denied"));
        }

        var loginOwner = await userManager.FindByLoginAsync(externalLoginInfo.LoginProvider, externalLoginInfo.ProviderKey);
        if (loginOwner is not null && loginOwner.Id != user.Id)
        {
            return Results.Redirect(BuildRedirectResult(resolvedReturnUrl, providerDefinition.Id, "external_login_conflict"));
        }

        if (loginOwner is null)
        {
            var addLoginResult = await userManager.AddLoginAsync(user, externalLoginInfo);
            if (!addLoginResult.Succeeded)
            {
                if (userCreatedInThisFlow)
                {
                    await userManager.DeleteAsync(user);
                }

                return Results.Redirect(BuildRedirectResult(resolvedReturnUrl, providerDefinition.Id, "account_link_failed"));
            }
        }

        await signInManager.SignInAsync(user, isPersistent: false, providerDefinition.SchemeName);
        await signInManager.UpdateExternalAuthenticationTokensAsync(externalLoginInfo);
        await httpContext.SignOutAsync(IdentityConstants.ExternalScheme);

        return Results.Redirect(BuildRedirectResult(resolvedReturnUrl, providerDefinition.Id, isSuccess: true));
    }

    private static bool TryGetProvider(string provider, out ExternalProviderDefinition providerDefinition)
    {
        return ExternalProviders.TryGetValue(provider, out providerDefinition!);
    }

    private static bool TryResolveReturnUrl(string? returnUrl, RedirectOptions options, out string resolvedReturnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            resolvedReturnUrl = string.Empty;
            return false;
        }

        var candidate = returnUrl.Trim();

        if (!IsReturnUrlAllowed(candidate, options))
        {
            resolvedReturnUrl = string.Empty;
            return false;
        }

        resolvedReturnUrl = candidate;
        return true;
    }

    private static bool IsReturnUrlAllowed(string candidate, RedirectOptions options)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        var isRelative = candidate.StartsWith('/') && !candidate.StartsWith("//", StringComparison.Ordinal);
        var isAbsolute = Uri.TryCreate(candidate, UriKind.Absolute, out var absoluteUri)
            && (absoluteUri.Scheme == Uri.UriSchemeHttp || absoluteUri.Scheme == Uri.UriSchemeHttps)
            && string.IsNullOrWhiteSpace(absoluteUri.Fragment);

        if (!isRelative && !isAbsolute)
        {
            return false;
        }

        return options.AllowedReturnUrls.Any(url => string.Equals(url, candidate, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsEmailVerified(string providerId, ClaimsPrincipal principal)
    {
        var emailVerifiedClaim = principal.FindFirstValue("email_verified")
            ?? principal.FindFirstValue("verified_email")
            ?? principal.FindFirstValue("urn:google:email_verified");
        if (bool.TryParse(emailVerifiedClaim, out var emailVerified))
        {
            return emailVerified;
        }

        return !string.Equals(providerId, "google", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildRedirectResult(string returnUrl, string providerId, string errorCode)
    {
        var queryValues = new Dictionary<string, string?>
        {
            ["auth"] = "error",
            ["provider"] = providerId,
            ["code"] = errorCode
        };

        return QueryHelpers.AddQueryString(returnUrl, queryValues!);
    }

    private static string BuildRedirectResult(string returnUrl, string providerId, bool isSuccess)
    {
        var queryValues = new Dictionary<string, string?>
        {
            ["auth"] = isSuccess ? "success" : "error",
            ["provider"] = providerId
        };

        return QueryHelpers.AddQueryString(returnUrl, queryValues!);
    }

    public record PasswordlessRequest([Required, EmailAddress] string Email);

    public record PasswordlessVerifyRequest([Required, EmailAddress] string Email, [Required] string Code);

    private sealed record ExternalProviderDefinition(string Id, string SchemeName, string DisplayName);

    private sealed record ExternalProviderResponse(string Id, string DisplayName, string StartEndpoint);
}
