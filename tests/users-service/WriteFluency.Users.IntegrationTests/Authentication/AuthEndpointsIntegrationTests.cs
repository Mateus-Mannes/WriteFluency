using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Shouldly;
using WriteFluency.Users.IntegrationTests.Infrastructure;

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

        var register = await client.PostAsJsonAsync("/users/auth/register", new { Email = email, Password = password });
        AssertEndpointIsMapped(register, "POST /users/auth/register");

        var login = await client.PostAsJsonAsync("/users/auth/login?useCookies=true", new { Email = email, Password = password });
        AssertEndpointIsMapped(login, "POST /users/auth/login");

        var confirmEmail = await client.GetAsync("/users/auth/confirmEmail?userId=invalid&code=invalid");
        AssertEndpointIsMapped(confirmEmail, "GET /users/auth/confirmEmail");

        var resend = await client.PostAsJsonAsync("/users/auth/resendConfirmationEmail", new { Email = email });
        AssertEndpointIsMapped(resend, "POST /users/auth/resendConfirmationEmail");

        var forgot = await client.PostAsJsonAsync("/users/auth/forgotPassword", new { Email = email });
        AssertEndpointIsMapped(forgot, "POST /users/auth/forgotPassword");

        var reset = await client.PostAsJsonAsync("/users/auth/resetPassword", new
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

        var register = await client.PostAsJsonAsync("/users/auth/register", new
        {
            Email = email,
            Password = password
        });
        register.IsSuccessStatusCode.ShouldBeTrue();

        var loginBeforeConfirm = await client.PostAsJsonAsync("/users/auth/login?useCookies=true", new
        {
            Email = email,
            Password = password
        });
        loginBeforeConfirm.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var confirmationEmail = _fixture.EmailSender.FindLastBySubjectContains("Confirm your WriteFluency email");
        confirmationEmail.ShouldNotBeNull();

        var confirmationUrl = ExtractHref(confirmationEmail!.HtmlBody);
        var confirmationUri = new Uri(WebUtility.HtmlDecode(confirmationUrl));

        var confirm = await client.GetAsync(confirmationUri.PathAndQuery);
        confirm.IsSuccessStatusCode.ShouldBeTrue();

        var login = await client.PostAsJsonAsync("/users/auth/login?useCookies=true", new
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
        }

        var logout = await client.PostAsJsonAsync("/users/auth/logout", new { });
        logout.IsSuccessStatusCode.ShouldBeTrue();

        var sessionAfterLogout = await client.GetAsync("/users/auth/session");
        IsUnauthenticatedStatus(sessionAfterLogout.StatusCode).ShouldBeTrue();
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

        var requestOtp = await client.PostAsJsonAsync("/users/auth/passwordless/request", new
        {
            Email = email
        });
        requestOtp.IsSuccessStatusCode.ShouldBeTrue();

        var otpEmail = _fixture.EmailSender.FindLastBySubjectContains("sign-in code");
        otpEmail.ShouldNotBeNull();

        var wrongVerify = await client.PostAsJsonAsync("/users/auth/passwordless/verify", new
        {
            Email = email,
            Code = "000000"
        });
        wrongVerify.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var otpCode = ExtractCode(otpEmail!.HtmlBody);
        var verify = await client.PostAsJsonAsync("/users/auth/passwordless/verify", new
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

        var postInfo = await client.PostAsJsonAsync("/users/auth/manage/info", new { });
        AssertEndpointIsMapped(postInfo, "POST /users/auth/manage/info");

        var post2Fa = await client.PostAsJsonAsync("/users/auth/manage/2fa", new
        {
            Enable = false,
            ResetSharedKey = false,
            ResetRecoveryCodes = false,
            ForgetMachine = false
        });
        AssertEndpointIsMapped(post2Fa, "POST /users/auth/manage/2fa");
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
        var response = await client.GetAsync("/users/health");

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
        var register = await client.PostAsJsonAsync("/users/auth/register", new
        {
            Email = email,
            Password = password
        });
        register.IsSuccessStatusCode.ShouldBeTrue();

        var confirmationEmail = _fixture.EmailSender.FindLastBySubjectContains("Confirm your WriteFluency email");
        confirmationEmail.ShouldNotBeNull();

        var confirmationUrl = ExtractHref(confirmationEmail!.HtmlBody);
        var confirmationUri = new Uri(WebUtility.HtmlDecode(confirmationUrl));

        var confirm = await client.GetAsync(confirmationUri.PathAndQuery);
        confirm.IsSuccessStatusCode.ShouldBeTrue();

        var login = await client.PostAsJsonAsync("/users/auth/login?useCookies=true", new
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

    private static string ExtractCode(string html)
    {
        var match = Regex.Match(html, "<strong>\\s*([^<]+?)\\s*</strong>", RegexOptions.IgnoreCase);
        match.Success.ShouldBeTrue("Expected to find OTP code in email body");
        return match.Groups[1].Value.Trim();
    }
}
