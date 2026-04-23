using Microsoft.AspNetCore.Http;
using Shouldly;
using WriteFluency.Users.WebApi.Authentication;

namespace WriteFluency.Users.Tests.Authentication;

public class LoginAuthContextResolverTests
{
    [Fact]
    public void TryResolve_ShouldIdentifyPasswordLogin()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/users/auth/login";

        var resolved = LoginAuthContextResolver.TryResolve(context.Request, out var authContext);

        resolved.ShouldBeTrue();
        authContext.AuthMethod.ShouldBe("password");
        authContext.AuthProvider.ShouldBeNull();
    }

    [Fact]
    public void TryResolve_ShouldIdentifyOtpLogin()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/users/auth/passwordless/verify";

        var resolved = LoginAuthContextResolver.TryResolve(context.Request, out var authContext);

        resolved.ShouldBeTrue();
        authContext.AuthMethod.ShouldBe("otp");
        authContext.AuthProvider.ShouldBeNull();
    }

    [Fact]
    public void TryResolve_ShouldIdentifyExternalLoginFromPath()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/users/auth/external/google/callback";

        var resolved = LoginAuthContextResolver.TryResolve(context.Request, out var authContext);

        resolved.ShouldBeTrue();
        authContext.AuthMethod.ShouldBe("external");
        authContext.AuthProvider.ShouldBe("google");
    }

    [Fact]
    public void TryResolve_ShouldUseRouteProviderFallback()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/users/auth/external/callback";
        context.Request.RouteValues["provider"] = "microsoft";

        var resolved = LoginAuthContextResolver.TryResolve(context.Request, out var authContext);

        resolved.ShouldBeTrue();
        authContext.AuthMethod.ShouldBe("external");
        authContext.AuthProvider.ShouldBe("microsoft");
    }

    [Fact]
    public void TryResolve_ShouldReturnFalseForUnsupportedRequest()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/users/auth/register";

        var resolved = LoginAuthContextResolver.TryResolve(context.Request, out _);

        resolved.ShouldBeFalse();
    }
}
