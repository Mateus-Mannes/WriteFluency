using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WriteFluency.Users.WebApi.Data;
using WriteFluency.Users.WebApi.Email;
using WriteFluency.Users.WebApi.Options;

namespace WriteFluency.Users.WebApi.Authentication;

public static class AuthEndpointExtensions
{
    private const string UsersAuthBasePath = "/users/auth";
    private const int FeedbackSubmittedCooldownDays = 60;
    private const int FeedbackDismissedCooldownDays = 21;
    private const string FreePlan = "free";
    private const string ProPlan = "pro";

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

        authGroup.MapPost("/tutorial/listen-write/completed", MarkListenWriteTutorialCompletedAsync)
            .RequireAuthorization();

        authGroup.MapGet("/feedback-prompts/{campaignKey}/status", GetFeedbackPromptStatusAsync)
            .RequireAuthorization();

        authGroup.MapPost("/feedback-prompts/{campaignKey}/shown", MarkFeedbackPromptShownAsync)
            .RequireAuthorization();

        authGroup.MapPost("/feedback-prompts/{campaignKey}/dismissed", MarkFeedbackPromptDismissedAsync)
            .RequireAuthorization();

        authGroup.MapPost("/feedback-prompts/{campaignKey}/submitted", MarkFeedbackPromptSubmittedAsync)
            .RequireAuthorization();

        authGroup.MapPost("/passwordless/request", RequestPasswordlessOtpAsync);

        authGroup.MapPost("/passwordless/verify", VerifyPasswordlessOtpAsync);

        authGroup.MapPost("/password/continue", ContinueWithPasswordAsync);

        authGroup.MapPost("/password/setup/confirm", ConfirmPasswordSetupAsync);

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
        var entitlement = BuildSubscriptionEntitlement(user, DateTimeOffset.UtcNow);

        return Results.Ok(new
        {
            IsAuthenticated = principal.Identity?.IsAuthenticated ?? false,
            UserId = user?.Id ?? principal.FindFirstValue(ClaimTypes.NameIdentifier),
            Email = user?.Email ?? principal.FindFirstValue(ClaimTypes.Email),
            EmailConfirmed = user?.EmailConfirmed ?? false,
            ListenWriteTutorialCompleted = user?.ListenWriteTutorialCompleted ?? false,
            Plan = entitlement.Plan,
            EntitlementStatus = entitlement.Status,
            IsPro = entitlement.IsPro,
            CurrentPeriodEndUtc = entitlement.CurrentPeriodEndUtc,
            CancelAtPeriodEnd = entitlement.CancelAtPeriodEnd,
            IssuedAtUtc = issuedAtUtc,
            ExpiresAtUtc = expiresAtUtc
        });
    }

    private static SubscriptionEntitlement BuildSubscriptionEntitlement(ApplicationUser? user, DateTimeOffset nowUtc)
    {
        var plan = string.Equals(user?.SubscriptionPlan, ProPlan, StringComparison.OrdinalIgnoreCase)
            ? ProPlan
            : FreePlan;
        var currentPeriodEndUtc = user?.SubscriptionCurrentPeriodEndUtc;
        var cancelAtPeriodEnd = user?.SubscriptionCancelAtPeriodEnd ?? false;

        if (plan is not ProPlan)
        {
            return new SubscriptionEntitlement(
                Plan: FreePlan,
                Status: "free",
                IsPro: false,
                CurrentPeriodEndUtc: currentPeriodEndUtc,
                CancelAtPeriodEnd: cancelAtPeriodEnd);
        }

        if (currentPeriodEndUtc is null || currentPeriodEndUtc <= nowUtc)
        {
            return new SubscriptionEntitlement(
                Plan: ProPlan,
                Status: "pro_expired",
                IsPro: false,
                CurrentPeriodEndUtc: currentPeriodEndUtc,
                CancelAtPeriodEnd: cancelAtPeriodEnd);
        }

        return new SubscriptionEntitlement(
            Plan: ProPlan,
            Status: cancelAtPeriodEnd ? "pro_canceling" : "pro_active",
            IsPro: true,
            CurrentPeriodEndUtc: currentPeriodEndUtc,
            CancelAtPeriodEnd: cancelAtPeriodEnd);
    }

    private static async Task<IResult> MarkListenWriteTutorialCompletedAsync(
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        if (!user.ListenWriteTutorialCompleted)
        {
            user.ListenWriteTutorialCompleted = true;
            var updateResult = await userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                return Results.Problem(
                    detail: "Unable to persist tutorial completion.",
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        return Results.Ok(new
        {
            ListenWriteTutorialCompleted = true
        });
    }

    private static async Task<IResult> GetFeedbackPromptStatusAsync(
        string campaignKey,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        UsersDbContext db,
        CancellationToken cancellationToken)
    {
        if (!IsValidFeedbackCampaignKey(campaignKey))
        {
            return Results.BadRequest(new { Error = "invalid_campaign_key" });
        }

        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var state = await db.UserFeedbackPromptStates
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.UserId == user.Id && x.CampaignKey == campaignKey, cancellationToken);

        return Results.Ok(BuildFeedbackPromptStatus(campaignKey, state, DateTimeOffset.UtcNow));
    }

    private static async Task<IResult> MarkFeedbackPromptShownAsync(
        string campaignKey,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        UsersDbContext db,
        CancellationToken cancellationToken)
    {
        var result = await GetOrCreateFeedbackPromptStateAsync(campaignKey, principal, userManager, db, cancellationToken);
        if (result.Result is not null)
        {
            return result.Result;
        }

        var now = DateTimeOffset.UtcNow;
        result.State!.LastShownAtUtc = now;
        result.State.UpdatedAtUtc = now;
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(BuildFeedbackPromptStatus(campaignKey, result.State, now));
    }

    private static async Task<IResult> MarkFeedbackPromptDismissedAsync(
        string campaignKey,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        UsersDbContext db,
        CancellationToken cancellationToken)
    {
        var result = await GetOrCreateFeedbackPromptStateAsync(campaignKey, principal, userManager, db, cancellationToken);
        if (result.Result is not null)
        {
            return result.Result;
        }

        var now = DateTimeOffset.UtcNow;
        result.State!.LastDismissedAtUtc = now;
        result.State.DismissCount++;
        result.State.UpdatedAtUtc = now;
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(BuildFeedbackPromptStatus(campaignKey, result.State, now));
    }

    private static async Task<IResult> MarkFeedbackPromptSubmittedAsync(
        string campaignKey,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        UsersDbContext db,
        CancellationToken cancellationToken)
    {
        var result = await GetOrCreateFeedbackPromptStateAsync(campaignKey, principal, userManager, db, cancellationToken);
        if (result.Result is not null)
        {
            return result.Result;
        }

        var now = DateTimeOffset.UtcNow;
        result.State!.LastSubmittedAtUtc = now;
        result.State.SubmitCount++;
        result.State.UpdatedAtUtc = now;
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(BuildFeedbackPromptStatus(campaignKey, result.State, now));
    }

    private static async Task<FeedbackPromptStateResult> GetOrCreateFeedbackPromptStateAsync(
        string campaignKey,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        UsersDbContext db,
        CancellationToken cancellationToken)
    {
        if (!IsValidFeedbackCampaignKey(campaignKey))
        {
            return new FeedbackPromptStateResult(null, Results.BadRequest(new { Error = "invalid_campaign_key" }));
        }

        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return new FeedbackPromptStateResult(null, Results.Unauthorized());
        }

        var state = await db.UserFeedbackPromptStates
            .SingleOrDefaultAsync(x => x.UserId == user.Id && x.CampaignKey == campaignKey, cancellationToken);

        if (state is not null)
        {
            return new FeedbackPromptStateResult(state, null);
        }

        var now = DateTimeOffset.UtcNow;
        state = new UserFeedbackPromptState
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            CampaignKey = campaignKey,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        await db.UserFeedbackPromptStates.AddAsync(state, cancellationToken);
        return new FeedbackPromptStateResult(state, null);
    }

    private static FeedbackPromptStatusResponse BuildFeedbackPromptStatus(
        string campaignKey,
        UserFeedbackPromptState? state,
        DateTimeOffset now)
    {
        var nextEligibleAtUtc = GetFeedbackPromptNextEligibleAtUtc(state);
        var isEligible = nextEligibleAtUtc is null || nextEligibleAtUtc <= now;

        return new FeedbackPromptStatusResponse(
            campaignKey,
            isEligible,
            nextEligibleAtUtc,
            state?.LastShownAtUtc,
            state?.LastDismissedAtUtc,
            state?.LastSubmittedAtUtc,
            state?.DismissCount ?? 0,
            state?.SubmitCount ?? 0);
    }

    private static DateTimeOffset? GetFeedbackPromptNextEligibleAtUtc(UserFeedbackPromptState? state)
    {
        if (state is null)
        {
            return null;
        }

        DateTimeOffset? nextEligibleAtUtc = null;
        if (state.LastDismissedAtUtc is DateTimeOffset dismissedAtUtc)
        {
            nextEligibleAtUtc = dismissedAtUtc.AddDays(FeedbackDismissedCooldownDays);
        }

        if (state.LastSubmittedAtUtc is DateTimeOffset submittedAtUtc)
        {
            var submittedNextEligibleAtUtc = submittedAtUtc.AddDays(FeedbackSubmittedCooldownDays);
            nextEligibleAtUtc = nextEligibleAtUtc is null || submittedNextEligibleAtUtc > nextEligibleAtUtc
                ? submittedNextEligibleAtUtc
                : nextEligibleAtUtc;
        }

        return nextEligibleAtUtc;
    }

    private static bool IsValidFeedbackCampaignKey(string campaignKey)
    {
        return !string.IsNullOrWhiteSpace(campaignKey)
            && campaignKey.Length <= 100
            && campaignKey.All(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.');
    }

    private static async Task<IResult> RequestPasswordlessOtpAsync(
        PasswordlessRequest request,
        HttpContext httpContext,
        IClientIpResolver clientIpResolver,
        PasswordlessOtpService passwordlessOtpService,
        CancellationToken cancellationToken)
    {
        var ipAddress = clientIpResolver.Resolve(httpContext)?.ToString() ?? "unknown";
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
        var verificationResult = await passwordlessOtpService.VerifyOtpAndSignInAsync(request.Email, request.Code, cancellationToken);
        return verificationResult.Succeeded
            ? Results.Ok(new PasswordlessVerifyResponse(verificationResult.IsNewUser))
            : Results.Unauthorized();
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
            var confirmResult = await ConfirmUserEmailAsync(userManager, user);
            if (!confirmResult.Succeeded)
            {
                return Results.Redirect(BuildRedirectResult(resolvedReturnUrl, providerDefinition.Id, "email_confirmation_failed"));
            }
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

        return Results.Redirect(BuildRedirectResult(
            resolvedReturnUrl,
            providerDefinition.Id,
            isSuccess: true,
            isNewUser: userCreatedInThisFlow));
    }

    private static async Task<IResult> ContinueWithPasswordAsync(
        PasswordContinueRequest request,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IEmailSender<ApplicationUser> emailSender,
        IAppEmailSender appEmailSender,
        PasswordSetupService passwordSetupService,
        IOptions<ExternalAuthenticationOptions> externalAuthenticationOptions)
    {
        if (string.IsNullOrWhiteSpace(request.Email)
            || !new EmailAddressAttribute().IsValid(request.Email)
            || string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.BadRequest(new { Error = "invalid_password_continue_request" });
        }

        var email = request.Email.Trim();
        var password = request.Password;

        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            if (!request.SendEmail)
            {
                return Results.Ok(new PasswordContinueResponse("confirmation_required", false));
            }

            user = new ApplicationUser
            {
                UserName = email,
                Email = email
            };

            var createResult = await userManager.CreateAsync(user, password);
            if (!createResult.Succeeded)
            {
                return Results.ValidationProblem(BuildIdentityErrorDictionary(createResult));
            }

            await SendConfirmationEmailAsync(userManager, emailSender, user, email);
            return Results.Ok(new PasswordContinueResponse("confirmation_required", true));
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            return Results.Ok(new PasswordContinueResponse("account_locked", false));
        }

        if (string.IsNullOrEmpty(user.PasswordHash))
        {
            if (!user.EmailConfirmed)
            {
                if (request.SendEmail)
                {
                    await SendConfirmationEmailAsync(userManager, emailSender, user, email);
                }

                return Results.Ok(new PasswordContinueResponse("confirmation_required", false));
            }

            if (request.SendEmail)
            {
                await SendPasswordSetupEmailAsync(
                    passwordSetupService,
                    appEmailSender,
                    externalAuthenticationOptions.Value,
                    user,
                    email,
                    password);
            }

            return Results.Ok(new PasswordContinueResponse("password_setup_required", false));
        }

        var passwordMatches = await userManager.CheckPasswordAsync(user, password);
        if (!passwordMatches)
        {
            await userManager.AccessFailedAsync(user);
            return Results.Ok(new PasswordContinueResponse("wrong_password", false));
        }

        if (!user.EmailConfirmed)
        {
            if (request.SendEmail)
            {
                await SendConfirmationEmailAsync(userManager, emailSender, user, email);
            }

            return Results.Ok(new PasswordContinueResponse("confirmation_required", false));
        }

        await userManager.ResetAccessFailedCountAsync(user);
        await signInManager.SignInAsync(user, isPersistent: false, authenticationMethod: "password");
        return Results.Ok(new PasswordContinueResponse("signed_in", false));
    }

    private static async Task<IResult> ConfirmPasswordSetupAsync(
        PasswordSetupConfirmRequest request,
        PasswordSetupService passwordSetupService)
    {
        var succeeded = await passwordSetupService.ConfirmSetupAsync(request.Token);
        return succeeded
            ? Results.Ok(new PasswordSetupConfirmResponse("confirmed"))
            : Results.BadRequest(new { Error = "invalid_password_setup_token" });
    }

    private static async Task SendConfirmationEmailAsync(
        UserManager<ApplicationUser> userManager,
        IEmailSender<ApplicationUser> emailSender,
        ApplicationUser user,
        string email)
    {
        var code = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var encodedCode = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
        var confirmationLink = QueryHelpers.AddQueryString(
            "/users/auth/confirmEmail",
            new Dictionary<string, string?>
            {
                ["userId"] = user.Id,
                ["code"] = encodedCode
            });

        await emailSender.SendConfirmationLinkAsync(user, email, confirmationLink);
    }

    private static async Task SendPasswordSetupEmailAsync(
        PasswordSetupService passwordSetupService,
        IAppEmailSender emailSender,
        ExternalAuthenticationOptions externalAuthenticationOptions,
        ApplicationUser user,
        string email,
        string password)
    {
        var setupToken = await passwordSetupService.CreateSetupTokenAsync(user, password);
        var setupLink = QueryHelpers.AddQueryString(
            BuildWebAppAuthUrl(externalAuthenticationOptions.ConfirmationRedirectUrl, "/auth/confirm-email"),
            new Dictionary<string, string?>
            {
                ["passwordSetupToken"] = setupToken
            });

        var content = EmailTemplateBuilder.BuildPasswordSetupLinkEmail(setupLink);
        await emailSender.SendAsync(email, "Set your WriteFluency password", content.HtmlBody, content.TextBody);
    }

    private static async Task<IdentityResult> ConfirmUserEmailAsync(
        UserManager<ApplicationUser> userManager,
        ApplicationUser user)
    {
        var code = await userManager.GenerateEmailConfirmationTokenAsync(user);
        return await userManager.ConfirmEmailAsync(user, code);
    }

    private static Dictionary<string, string[]> BuildIdentityErrorDictionary(IdentityResult result)
    {
        return result.Errors
            .GroupBy(error => error.Code)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.Description).ToArray());
    }

    private static string BuildWebAppAuthUrl(string confirmationRedirectUrl, string path)
    {
        var confirmationUri = new Uri(confirmationRedirectUrl, UriKind.Absolute);
        var builder = new UriBuilder(confirmationUri)
        {
            Path = path,
            Query = string.Empty,
            Fragment = string.Empty
        };

        return builder.Uri.ToString();
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

    private static string BuildRedirectResult(string returnUrl, string providerId, bool isSuccess, bool isNewUser = false)
    {
        var queryValues = new Dictionary<string, string?>
        {
            ["auth"] = isSuccess ? "success" : "error",
            ["provider"] = providerId
        };

        if (isSuccess && isNewUser)
        {
            queryValues["newUser"] = "true";
        }

        return QueryHelpers.AddQueryString(returnUrl, queryValues!);
    }

    public record PasswordlessRequest([Required, EmailAddress] string Email);

    public record PasswordlessVerifyRequest([Required, EmailAddress] string Email, [Required] string Code);

    public record PasswordlessVerifyResponse(bool IsNewUser);

    public sealed record PasswordContinueRequest
    {
        [Required, EmailAddress]
        public required string Email { get; init; }

        [Required]
        public required string Password { get; init; }

        public bool SendEmail { get; init; } = true;
    }

    public record PasswordContinueResponse(string Status, bool IsNewUser);

    public record PasswordSetupConfirmRequest([Required] string Token);

    public record PasswordSetupConfirmResponse(string Status);

    private sealed record ExternalProviderDefinition(string Id, string SchemeName, string DisplayName);

    private sealed record ExternalProviderResponse(string Id, string DisplayName, string StartEndpoint);

    private sealed record FeedbackPromptStatusResponse(
        string CampaignKey,
        bool IsEligible,
        DateTimeOffset? NextEligibleAtUtc,
        DateTimeOffset? LastShownAtUtc,
        DateTimeOffset? LastDismissedAtUtc,
        DateTimeOffset? LastSubmittedAtUtc,
        int DismissCount,
        int SubmitCount);

    private sealed record FeedbackPromptStateResult(UserFeedbackPromptState? State, IResult? Result);

    private sealed record SubscriptionEntitlement(
        string Plan,
        string Status,
        bool IsPro,
        DateTimeOffset? CurrentPeriodEndUtc,
        bool CancelAtPeriodEnd);
}
