using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using WriteFluency.Users.IntegrationTests.Infrastructure;
using WriteFluency.Users.WebApi.Data;

namespace WriteFluency.Users.IntegrationTests.Support;

public class SupportRequestsIntegrationTests : IClassFixture<UsersApiIntegrationFixture>
{
    private readonly UsersApiIntegrationFixture _fixture;

    public SupportRequestsIntegrationTests(UsersApiIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SubmitSupportRequest_AsGuest_ShouldSendEmailToConfiguredRecipients()
    {
        if (!CanRunIntegration())
        {
            return;
        }

        await _fixture.ResetAsync();
        using var client = _fixture.CreateClient();

        var response = await PostSupportRequestAsync(client, new
        {
            Message = "I need help with an exercise.",
            ReplyEmail = "guest@writefluency.test",
            SourceUrl = "http://localhost:4200/support"
        });

        response.IsSuccessStatusCode.ShouldBeTrue();
        using (var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync()))
        {
            doc.RootElement.GetProperty("accepted").GetBoolean().ShouldBeTrue();
        }

        var messages = _fixture.EmailSender.Messages;
        messages.Count.ShouldBe(3);
        messages.Select(message => message.ToEmail).ShouldBe([
            "support-1@writefluency.test",
            "support-2@writefluency.test",
            "support-3@writefluency.test"
        ]);
        messages.ShouldAllBe(message => message.Subject == "WriteFluency support request");
        messages.ShouldAllBe(message => message.TextBody.Contains("I need help with an exercise.", StringComparison.Ordinal));
        messages.ShouldAllBe(message => message.TextBody.Contains("guest@writefluency.test", StringComparison.Ordinal));
        messages.ShouldAllBe(message => message.TextBody.Contains("http://localhost:4200/support", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SubmitSupportRequest_AsAuthenticatedUser_ShouldIncludeUserContext()
    {
        if (!CanRunIntegration())
        {
            return;
        }

        await _fixture.ResetAsync();
        using var client = _fixture.CreateClient();

        var email = $"support-user-{Guid.NewGuid():N}@writefluency.test";
        const string password = "Passw0rd!123";
        await RegisterConfirmAndLoginAsync(client, email, password);
        _fixture.EmailSender.Clear();

        var user = await GetUserByEmailAsync(email);
        var response = await PostSupportRequestAsync(client, new
        {
            Message = "Authenticated support request.",
            SourceUrl = "http://localhost:4200/user"
        });

        response.IsSuccessStatusCode.ShouldBeTrue();

        var supportEmail = _fixture.EmailSender.FindLastBySubjectContains("WriteFluency support request");
        supportEmail.ShouldNotBeNull();
        supportEmail!.TextBody.ShouldContain(user.Id);
        supportEmail.TextBody.ShouldContain(email);
        supportEmail.TextBody.ShouldContain("Authenticated support request.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SubmitSupportRequest_WithEmptyMessage_ShouldReturnBadRequest(string message)
    {
        if (!CanRunIntegration())
        {
            return;
        }

        await _fixture.ResetAsync();
        using var client = _fixture.CreateClient();

        var response = await PostSupportRequestAsync(client, new
        {
            Message = message
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SubmitSupportRequest_WithOversizedMessage_ShouldReturnBadRequest()
    {
        if (!CanRunIntegration())
        {
            return;
        }

        await _fixture.ResetAsync();
        using var client = _fixture.CreateClient();

        var response = await PostSupportRequestAsync(client, new
        {
            Message = new string('a', 4001)
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SubmitSupportRequest_WithInvalidReplyEmail_ShouldReturnBadRequest()
    {
        if (!CanRunIntegration())
        {
            return;
        }

        await _fixture.ResetAsync();
        using var client = _fixture.CreateClient();

        var response = await PostSupportRequestAsync(client, new
        {
            Message = "Please help.",
            ReplyEmail = "not-an-email"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SubmitSupportRequest_WithoutOriginOrReferer_ShouldReturnForbidden()
    {
        if (!CanRunIntegration())
        {
            return;
        }

        await _fixture.ResetAsync();
        using var client = _fixture.CreateClient();

        var response = await client.PostAsJsonAsync("/users/support/requests", new
        {
            Message = "Please help."
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SubmitSupportRequest_WithCrossSiteOrigin_ShouldReturnForbidden()
    {
        if (!CanRunIntegration())
        {
            return;
        }

        await _fixture.ResetAsync();
        using var client = _fixture.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/users/support/requests")
        {
            Content = JsonContent.Create(new
            {
                Message = "Please help."
            })
        };
        request.Headers.TryAddWithoutValidation("Origin", "https://malicious.example");

        var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SubmitSupportRequest_WhenIpRateLimitExceeded_ShouldReturnTooManyRequests()
    {
        if (!CanRunIntegration())
        {
            return;
        }

        await _fixture.ResetAsync();
        using var client = _fixture.CreateClient();

        for (var i = 0; i < 3; i++)
        {
            var allowed = await PostSupportRequestAsync(client, new
            {
                Message = $"Allowed message {i}."
            }, ipAddress: "203.0.113.45");
            allowed.IsSuccessStatusCode.ShouldBeTrue();
        }

        var blocked = await PostSupportRequestAsync(client, new
        {
            Message = "This should be rate limited."
        }, ipAddress: "203.0.113.45");

        blocked.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    private bool CanRunIntegration()
    {
        return _fixture.IsAvailable;
    }

    private async Task RegisterConfirmAndLoginAsync(HttpClient client, string email, string password)
    {
        var register = await PostWithAllowedOriginAsync(client, "/users/auth/register", new
        {
            Email = email,
            Password = password
        });
        register.IsSuccessStatusCode.ShouldBeTrue();

        var confirmationEmail = _fixture.EmailSender.FindLastBySubjectContains("Confirm your WriteFluency email");
        confirmationEmail.ShouldNotBeNull();

        var confirmUrl = BuildUsersConfirmEmailUrlFromWebappLink(confirmationEmail!.HtmlBody);
        var confirm = await client.GetAsync(confirmUrl);
        confirm.IsSuccessStatusCode.ShouldBeTrue();

        var login = await PostWithAllowedOriginAsync(client, "/users/auth/login?useCookies=true", new
        {
            Email = email,
            Password = password
        });
        login.IsSuccessStatusCode.ShouldBeTrue();
    }

    private async Task<ApplicationUser> GetUserByEmailAsync(string email)
    {
        using var scope = _fixture.Factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        var user = await db.Users.SingleAsync(user => user.Email == email);
        return user;
    }

    private static async Task<HttpResponseMessage> PostSupportRequestAsync(
        HttpClient client,
        object payload,
        string? ipAddress = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/users/support/requests")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.TryAddWithoutValidation("Origin", "http://localhost:4200");
        if (!string.IsNullOrWhiteSpace(ipAddress))
        {
            request.Headers.TryAddWithoutValidation("X-Forwarded-For", ipAddress);
        }

        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> PostWithAllowedOriginAsync(HttpClient client, string requestUri, object payload)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.TryAddWithoutValidation("Origin", "http://localhost:4200");

        return await client.SendAsync(request);
    }

    private static string BuildUsersConfirmEmailUrlFromWebappLink(string html)
    {
        var confirmationUrl = ExtractHref(html);
        var decodedUrl = WebUtility.HtmlDecode(confirmationUrl);
        var confirmationUri = new Uri(decodedUrl, UriKind.Absolute);
        var query = QueryHelpers.ParseQuery(confirmationUri.Query);

        query.TryGetValue("userId", out var userId).ShouldBeTrue();
        query.TryGetValue("code", out var code).ShouldBeTrue();

        return QueryHelpers.AddQueryString("/users/auth/confirmEmail", new Dictionary<string, string?>
        {
            ["userId"] = userId.ToString(),
            ["code"] = code.ToString()
        });
    }

    private static string ExtractHref(string html)
    {
        var escapedHref = Regex.Match(html, "href=\\\\\\\"([^\\\\\\\"]+)\\\\\\\"", RegexOptions.IgnoreCase);
        if (escapedHref.Success)
        {
            return escapedHref.Groups[1].Value;
        }

        var normalHref = Regex.Match(html, "href=\"([^\"]+)\"", RegexOptions.IgnoreCase);
        normalHref.Success.ShouldBeTrue("Expected to find confirmation link in email body");
        return normalHref.Groups[1].Value;
    }
}
