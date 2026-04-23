using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.WebUtilities;
using Shouldly;
using WriteFluency.Users.IntegrationTests.Infrastructure;
using WriteFluency.Users.WebApi.Data;

namespace WriteFluency.Users.IntegrationTests.Authentication;

public class AuthEndpointsIntegrationTests : IClassFixture<UsersApiIntegrationFixture>
{
    private readonly UsersApiIntegrationFixture _fixture;

    public AuthEndpointsIntegrationTests(UsersApiIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task IdentityApi_CoreEndpoints_ShouldBeReachable()
    {
        if (!CanRunIntegration())
        {
            return;
        }

        await _fixture.ResetAsync();
        using var client = _fixture.CreateClient();
        var email = $"reachability-{Guid.NewGuid():N}@writefluency.test";
        const string password = "Passw0rd!123";

        var register = await PostAsJsonWithAllowedOriginAsync(client, "/users/auth/register", new { Email = email, Password = password });
        AssertEndpointIsMapped(register, "POST /users/auth/register");

        var login = await PostAsJsonWithAllowedOriginAsync(client, "/users/auth/login?useCookies=true", new { Email = email, Password = password });
        AssertEndpointIsMapped(login, "POST /users/auth/login");

        var confirmEmail = await client.GetAsync("/users/auth/confirmEmail?userId=invalid&code=invalid");
        AssertEndpointIsMapped(confirmEmail, "GET /users/auth/confirmEmail");

        var resend = await PostAsJsonWithAllowedOriginAsync(client, "/users/auth/resendConfirmationEmail", new { Email = email });
        AssertEndpointIsMapped(resend, "POST /users/auth/resendConfirmationEmail");

        var forgot = await PostAsJsonWithAllowedOriginAsync(client, "/users/auth/forgotPassword", new { Email = email });
        AssertEndpointIsMapped(forgot, "POST /users/auth/forgotPassword");

        var reset = await PostAsJsonWithAllowedOriginAsync(client, "/users/auth/resetPassword", new
        {
            Email = email,
            ResetCode = "invalid",
            NewPassword = "NewPassw0rd!123"
        });
        AssertEndpointIsMapped(reset, "POST /users/auth/resetPassword");

        // /auth/refresh requires bearer token setup; this API currently runs cookie-only auth.
    }

    [Fact]
    public async Task RegisterConfirmLoginSessionLogout_ShouldWorkEndToEnd()
    {
        if (!CanRunIntegration())
        {
            return;
        }

        await _fixture.ResetAsync();

        using var client = _fixture.CreateClient();

        var email = $"integration-{Guid.NewGuid():N}@writefluency.test";
        const string password = "Passw0rd!123";

        var sessionBefore = await client.GetAsync("/users/auth/session");
        IsUnauthenticatedStatus(sessionBefore.StatusCode).ShouldBeTrue();

        var register = await PostAsJsonWithAllowedOriginAsync(client, "/users/auth/register", new
        {
            Email = email,
            Password = password
        });
        register.IsSuccessStatusCode.ShouldBeTrue();

        var loginBeforeConfirm = await PostAsJsonWithAllowedOriginAsync(client, "/users/auth/login?useCookies=true", new
        {
            Email = email,
            Password = password
        });
        loginBeforeConfirm.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var confirmationEmail = _fixture.EmailSender.FindLastBySubjectContains("Confirm your WriteFluency email");
        confirmationEmail.ShouldNotBeNull();
        confirmationEmail!.TextBody.ShouldNotBeNullOrWhiteSpace();

        var confirmUrl = BuildUsersConfirmEmailUrlFromWebappLink(confirmationEmail.HtmlBody);
        var confirm = await client.GetAsync(confirmUrl);
        confirm.IsSuccessStatusCode.ShouldBeTrue();

        var login = await PostAsJsonWithAllowedOriginAsync(client, "/users/auth/login?useCookies=true", new
        {
            Email = email,
            Password = password
        });
        login.IsSuccessStatusCode.ShouldBeTrue();
        login.Headers.TryGetValues("Set-Cookie", out _).ShouldBeTrue();

        var sessionAfterLogin = await client.GetAsync("/users/auth/session");
        sessionAfterLogin.IsSuccessStatusCode.ShouldBeTrue();

        using (var sessionDoc = await JsonDocument.ParseAsync(await sessionAfterLogin.Content.ReadAsStreamAsync()))
        {
            sessionDoc.RootElement.GetProperty("isAuthenticated").GetBoolean().ShouldBeTrue();
            sessionDoc.RootElement.GetProperty("emailConfirmed").GetBoolean().ShouldBeTrue();
            sessionDoc.RootElement.GetProperty("email").GetString().ShouldBe(email);
            sessionDoc.RootElement.TryGetProperty("issuedAtUtc", out var issuedAtUtc).ShouldBeTrue();
            sessionDoc.RootElement.TryGetProperty("expiresAtUtc", out var expiresAtUtc).ShouldBeTrue();
            DateTimeOffset.TryParse(issuedAtUtc.GetString(), out _).ShouldBeTrue();
            DateTimeOffset.TryParse(expiresAtUtc.GetString(), out _).ShouldBeTrue();
        }

        await AssertSingleLoginActivityAsync(email, expectedMethod: "password", expectedProvider: null);

        var logout = await PostAsJsonWithAllowedOriginAsync(client, "/users/auth/logout", new { });
        logout.IsSuccessStatusCode.ShouldBeTrue();

        var sessionAfterLogout = await client.GetAsync("/users/auth/session");
        IsUnauthenticatedStatus(sessionAfterLogout.StatusCode).ShouldBeTrue();
    }

    [Fact]
    public async Task Logout_WithCrossSiteOriginHeader_ShouldBeRejectedWithForbidden()
    {
        if (!CanRunIntegration())
        {
            return;
        }

        await _fixture.ResetAsync();

        using var client = _fixture.CreateClient();

        var email = $"csrf-{Guid.NewGuid():N}@writefluency.test";
        const string password = "Passw0rd!123";
        await RegisterConfirmAndLoginAsync(client, email, password);

        using var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/users/auth/logout")
        {
            Content = JsonContent.Create(new { })
        };
        logoutRequest.Headers.TryAddWithoutValidation("Origin", "https://malicious.example");

        var logout = await client.SendAsync(logoutRequest);
        logout.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Logout_WithoutOriginOrReferer_ShouldBeRejectedWithForbidden()
    {
        if (!CanRunIntegration())
        {
            return;
        }

        await _fixture.ResetAsync();

        using var client = _fixture.CreateClient();

        var email = $"csrf-no-origin-{Guid.NewGuid():N}@writefluency.test";
        const string password = "Passw0rd!123";
        await RegisterConfirmAndLoginAsync(client, email, password);

        var logout = await client.PostAsJsonAsync("/users/auth/logout", new { });
        logout.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Register_WithoutOriginOrReferer_ShouldBeRejectedWithForbidden()
    {
        if (!CanRunIntegration())
        {
            return;
        }

        await _fixture.ResetAsync();

        using var client = _fixture.CreateClient();
        var email = $"csrf-register-{Guid.NewGuid():N}@writefluency.test";

        var register = await client.PostAsJsonAsync("/users/auth/register", new
        {
            Email = email,
            Password = "Passw0rd!123"
        });

        register.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PasswordlessRequestAndVerify_ShouldAuthenticateUser()
    {
        if (!CanRunIntegration())
        {
            return;
        }

        await _fixture.ResetAsync();

        using var client = _fixture.CreateClient();

        var email = $"passwordless-{Guid.NewGuid():N}@writefluency.test";

        var requestOtp = await PostAsJsonWithAllowedOriginAsync(client, "/users/auth/passwordless/request", new
        {
            Email = email
        });
        requestOtp.IsSuccessStatusCode.ShouldBeTrue();

        var otpEmail = _fixture.EmailSender.FindLastBySubjectContains("sign-in code");
        otpEmail.ShouldNotBeNull();
        otpEmail!.TextBody.ShouldNotBeNullOrWhiteSpace();

        var wrongVerify = await PostAsJsonWithAllowedOriginAsync(client, "/users/auth/passwordless/verify", new
        {
            Email = email,
            Code = "000000"
        });
        wrongVerify.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var otpCode = ExtractCode(otpEmail.HtmlBody);
        var verify = await PostAsJsonWithAllowedOriginAsync(client, "/users/auth/passwordless/verify", new
        {
            Email = email,
            Code = otpCode
        });

        verify.IsSuccessStatusCode.ShouldBeTrue();

        var session = await client.GetAsync("/users/auth/session");
        session.IsSuccessStatusCode.ShouldBeTrue();

        using var sessionDoc = await JsonDocument.ParseAsync(await session.Content.ReadAsStreamAsync());
        sessionDoc.RootElement.GetProperty("isAuthenticated").GetBoolean().ShouldBeTrue();
        sessionDoc.RootElement.GetProperty("emailConfirmed").GetBoolean().ShouldBeTrue();
        sessionDoc.RootElement.GetProperty("email").GetString().ShouldBe(email);

        await AssertSingleLoginActivityAsync(email, expectedMethod: "otp", expectedProvider: null);
    }

    [Fact]
    public async Task ManageEndpoints_ShouldBeReachableForAuthenticatedUser()
    {
        if (!CanRunIntegration())
        {
            return;
        }

        await _fixture.ResetAsync();
        using var client = _fixture.CreateClient();

        var email = $"manage-{Guid.NewGuid():N}@writefluency.test";
        const string password = "Passw0rd!123";

        await RegisterConfirmAndLoginAsync(client, email, password);

        var getInfo = await client.GetAsync("/users/auth/manage/info");
        AssertEndpointIsMapped(getInfo, "GET /users/auth/manage/info");

        var postInfo = await PostAsJsonWithAllowedOriginAsync(client, "/users/auth/manage/info", new { });
        AssertEndpointIsMapped(postInfo, "POST /users/auth/manage/info");

        var post2Fa = await PostAsJsonWithAllowedOriginAsync(client, "/users/auth/manage/2fa", new
        {
            Enable = false,
            ResetSharedKey = false,
            ResetRecoveryCodes = false,
            ForgetMachine = false
        });
        AssertEndpointIsMapped(post2Fa, "POST /users/auth/manage/2fa");
    }

    [Fact]
    public async Task ExternalProviders_ShouldListEnabledProviders()
    {
        if (!CanRunIntegration())
        {
            return;
        }

        await _fixture.ResetAsync();
        using var client = _fixture.CreateClient();

        var response = await client.GetAsync("/users/auth/external/providers");
        response.IsSuccessStatusCode.ShouldBeTrue();

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var providerIds = document.RootElement.EnumerateArray()
            .Select(item => item.GetProperty("id").GetString())
            .Where(value => value is not null)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        providerIds.ShouldContain("google");
        providerIds.ShouldContain("microsoft");
    }

    [Theory]
    [InlineData("google")]
    [InlineData("microsoft")]
    public async Task ExternalLoginFlow_ShouldAuthenticateUser_ForConfiguredProvider(string provider)
    {
        if (!CanRunIntegration())
        {
            return;
        }

        await _fixture.ResetAsync();
        using var startClient = _fixture.CreateClient();
        using var callbackClient = _fixture.CreateClient();

        var email = $"{provider}-{Guid.NewGuid():N}@writefluency.test";
        var scheme = ResolveProviderScheme(provider);
        var start = await startClient.GetAsync($"/users/auth/external/{provider}/start?returnUrl=%2Fusers%2Fswagger%2Findex.html");

        start.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        start.Headers.Location.ShouldNotBeNull();

        var callback = await callbackClient.GetAsync(
            $"/users/auth/external/{provider}/callback?returnUrl=%2Fusers%2Fswagger%2Findex.html&test_provider={Uri.EscapeDataString(scheme)}&test_provider_key={Guid.NewGuid():N}&test_email={Uri.EscapeDataString(email)}");
        callback.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        callback.Headers.Location.ShouldNotBeNull();
        GetQueryParam(callback.Headers.Location!, "auth").ShouldBe("success");
        GetQueryParam(callback.Headers.Location!, "provider").ShouldBe(provider);

        var session = await callbackClient.GetAsync("/users/auth/session");
        session.IsSuccessStatusCode.ShouldBeTrue();

        using var sessionDoc = await JsonDocument.ParseAsync(await session.Content.ReadAsStreamAsync());
        sessionDoc.RootElement.GetProperty("isAuthenticated").GetBoolean().ShouldBeTrue();
        sessionDoc.RootElement.GetProperty("email").GetString().ShouldBe(email);
        sessionDoc.RootElement.GetProperty("emailConfirmed").GetBoolean().ShouldBeTrue();

        await AssertSingleLoginActivityAsync(email, expectedMethod: "external", expectedProvider: provider);
    }

    [Fact]
    public async Task PasswordLogin_ShouldSucceed_WhenGeoLookupFails_AndPersistErrorStatus()
    {
        if (!CanRunIntegration())
        {
            return;
        }

        await _fixture.ResetAsync();
        _fixture.LoginGeoLookupService.ReturnError = true;

        using var client = _fixture.CreateClient();
        var email = $"geo-failure-{Guid.NewGuid():N}@writefluency.test";
        const string password = "Passw0rd!123";

        await RegisterConfirmAndLoginAsync(client, email, password);

        var session = await client.GetAsync("/users/auth/session");
        session.IsSuccessStatusCode.ShouldBeTrue();

        await AssertSingleLoginActivityAsync(
            email,
            expectedMethod: "password",
            expectedProvider: null,
            expectedGeoLookupStatus: "error");
    }

    [Fact]
    public async Task ExternalLoginCallback_WithoutExternalCookie_ShouldRedirectWithInvalidState()
    {
        if (!CanRunIntegration())
        {
            return;
        }

        await _fixture.ResetAsync();
        using var client = _fixture.CreateClient();

        var callback = await client.GetAsync("/users/auth/external/google/callback?returnUrl=%2Fusers%2Fswagger%2Findex.html");
        callback.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        callback.Headers.Location.ShouldNotBeNull();
        GetQueryParam(callback.Headers.Location!, "auth").ShouldBe("error");
        GetQueryParam(callback.Headers.Location!, "code").ShouldBe("invalid_state");
    }

    [Fact]
    public async Task ExternalLoginCallback_WithoutReturnUrl_ShouldReturnBadRequest()
    {
        if (!CanRunIntegration())
        {
            return;
        }

        await _fixture.ResetAsync();
        using var client = _fixture.CreateClient();

        var callback = await client.GetAsync("/users/auth/external/google/callback");
        callback.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        using var errorDoc = await JsonDocument.ParseAsync(await callback.Content.ReadAsStreamAsync());
        errorDoc.RootElement.TryGetProperty("error", out var error).ShouldBeTrue();
        error.GetString().ShouldBe("invalid_return_url");
    }

    [Theory]
    [InlineData("google", "Google")]
    public async Task ExternalLoginFlow_ShouldRejectUnverifiedProviderEmail(string provider, string scheme)
    {
        if (!CanRunIntegration())
        {
            return;
        }

        await _fixture.ResetAsync();
        using var startClient = _fixture.CreateClient();
        using var callbackClient = _fixture.CreateClient();

        var email = $"unverified-{Guid.NewGuid():N}@writefluency.test";
        var start = await startClient.GetAsync($"/users/auth/external/{provider}/start?returnUrl=%2Fusers%2Fswagger%2Findex.html");
        start.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        start.Headers.Location.ShouldNotBeNull();

        var callback = await callbackClient.GetAsync(
            $"/users/auth/external/{provider}/callback?returnUrl=%2Fusers%2Fswagger%2Findex.html&test_provider={Uri.EscapeDataString(scheme)}&test_provider_key={Guid.NewGuid():N}&test_email={Uri.EscapeDataString(email)}&test_email_verified=false");
        callback.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        callback.Headers.Location.ShouldNotBeNull();
        GetQueryParam(callback.Headers.Location!, "auth").ShouldBe("error");
        GetQueryParam(callback.Headers.Location!, "code").ShouldBe("provider_email_unverified");
    }

    [Fact]
    public async Task ExternalLoginStart_WithUnknownProvider_ShouldReturnBadRequest()
    {
        if (!CanRunIntegration())
        {
            return;
        }

        await _fixture.ResetAsync();
        using var client = _fixture.CreateClient();

        var response = await client.GetAsync("/users/auth/external/unknown/start?returnUrl=%2Fusers%2Fswagger%2Findex.html");
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task HealthEndpoint_ShouldReturnSuccess()
    {
        if (!CanRunIntegration())
        {
            return;
        }

        await _fixture.ResetAsync();

        using var client = _fixture.CreateClient();
        var response = await client.GetAsync("/health");

        response.IsSuccessStatusCode.ShouldBeTrue();
    }

    private bool CanRunIntegration()
    {
        return _fixture.IsAvailable;
    }

    private static void AssertEndpointIsMapped(HttpResponseMessage response, string endpointName)
    {
        response.StatusCode.ShouldNotBe(HttpStatusCode.NotFound, $"{endpointName} is not mapped");
        response.StatusCode.ShouldNotBe(HttpStatusCode.MethodNotAllowed, $"{endpointName} method mismatch");
    }

    private static bool IsUnauthenticatedStatus(HttpStatusCode statusCode)
    {
        return statusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Found or HttpStatusCode.Redirect;
    }

    private async Task RegisterConfirmAndLoginAsync(HttpClient client, string email, string password)
    {
        var register = await PostAsJsonWithAllowedOriginAsync(client, "/users/auth/register", new
        {
            Email = email,
            Password = password
        });
        register.IsSuccessStatusCode.ShouldBeTrue();

        var confirmationEmail = _fixture.EmailSender.FindLastBySubjectContains("Confirm your WriteFluency email");
        confirmationEmail.ShouldNotBeNull();
        confirmationEmail!.TextBody.ShouldNotBeNullOrWhiteSpace();

        var confirmUrl = BuildUsersConfirmEmailUrlFromWebappLink(confirmationEmail.HtmlBody);
        var confirm = await client.GetAsync(confirmUrl);
        confirm.IsSuccessStatusCode.ShouldBeTrue();

        var login = await PostAsJsonWithAllowedOriginAsync(client, "/users/auth/login?useCookies=true", new
        {
            Email = email,
            Password = password
        });
        login.IsSuccessStatusCode.ShouldBeTrue();
    }

    private static string ExtractHref(string html)
    {
        var escapedHref = Regex.Match(html, "href=\\\\\\\"([^\\\\\\\"]+)\\\\\\\"", RegexOptions.IgnoreCase);
        if (escapedHref.Success)
        {
            return escapedHref.Groups[1].Value;
        }

        var normalHref = Regex.Match(html, "href=\"([^\"]+)\"", RegexOptions.IgnoreCase);
        if (normalHref.Success)
        {
            return normalHref.Groups[1].Value;
        }

        var url = Regex.Match(html, @"https?://[^\s<""]+", RegexOptions.IgnoreCase);
        url.Success.ShouldBeTrue("Expected to find confirmation link in email body");
        return url.Value;
    }

    private static string BuildUsersConfirmEmailUrlFromWebappLink(string html)
    {
        var confirmationUrl = ExtractHref(html);
        var decodedUrl = WebUtility.HtmlDecode(confirmationUrl);
        var confirmationUri = new Uri(decodedUrl, UriKind.Absolute);

        confirmationUri.AbsolutePath.ShouldBe("/auth/confirm-email");

        var query = QueryHelpers.ParseQuery(confirmationUri.Query);
        query.TryGetValue("userId", out var userId).ShouldBeTrue();
        query.TryGetValue("code", out var code).ShouldBeTrue();
        userId.ToString().ShouldNotBeNullOrWhiteSpace();
        code.ToString().ShouldNotBeNullOrWhiteSpace();

        return QueryHelpers.AddQueryString("/users/auth/confirmEmail", new Dictionary<string, string?>
        {
            ["userId"] = userId.ToString(),
            ["code"] = code.ToString()
        });
    }

    private static string ExtractCode(string html)
    {
        var match = Regex.Match(html, "<strong>\\s*([^<]+?)\\s*</strong>", RegexOptions.IgnoreCase);
        match.Success.ShouldBeTrue("Expected to find OTP code in email body");
        return match.Groups[1].Value.Trim();
    }

    private static string? GetQueryParam(Uri location, string key)
    {
        var source = location.IsAbsoluteUri ? location.Query : location.OriginalString;
        var queryStringStart = source.IndexOf('?', StringComparison.Ordinal);
        var queryString = queryStringStart >= 0 ? source[queryStringStart..] : string.Empty;
        var query = QueryHelpers.ParseQuery(queryString);
        return query.TryGetValue(key, out var values) ? values.ToString() : null;
    }

    private static string ResolveProviderScheme(string provider)
    {
        return provider.ToLowerInvariant() switch
        {
            "google" => "Google",
            "microsoft" => "Microsoft",
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unsupported provider")
        };
    }

    private static async Task<HttpResponseMessage> PostAsJsonWithAllowedOriginAsync(HttpClient client, string requestUri, object payload)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.TryAddWithoutValidation("Origin", "http://localhost:4200");

        return await client.SendAsync(request);
    }

    private async Task AssertSingleLoginActivityAsync(
        string email,
        string expectedMethod,
        string? expectedProvider,
        string expectedGeoLookupStatus = "success")
    {
        var activity = await GetSingleLoginActivityByEmailAsync(email);

        activity.AuthMethod.ShouldBe(expectedMethod);
        if (expectedProvider is null)
        {
            activity.AuthProvider.ShouldBeNull();
        }
        else
        {
            activity.AuthProvider.ShouldBe(expectedProvider);
        }

        activity.GeoLookupStatus.ShouldBe(expectedGeoLookupStatus);

        if (expectedGeoLookupStatus == "success")
        {
            activity.CountryIsoCode.ShouldBe("US");
            activity.CountryName.ShouldBe("United States");
            activity.City.ShouldBe("Seattle");
            return;
        }

        activity.CountryIsoCode.ShouldBeNull();
        activity.CountryName.ShouldBeNull();
        activity.City.ShouldBeNull();
    }

    private async Task<UserLoginActivity> GetSingleLoginActivityByEmailAsync(string email)
    {
        _fixture.Factory.ShouldNotBeNull();

        using var scope = _fixture.Factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();

        var user = await db.Users.SingleAsync(u => u.Email == email);
        var activities = await db.UserLoginActivities
            .Where(a => a.UserId == user.Id)
            .OrderBy(a => a.OccurredAtUtc)
            .ToListAsync();

        activities.Count.ShouldBe(1);
        return activities[0];
    }
}
