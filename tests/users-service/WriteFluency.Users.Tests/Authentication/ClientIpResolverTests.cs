using System.Net;
using Microsoft.AspNetCore.Http;
using Shouldly;
using WriteFluency.Users.WebApi.Authentication;

namespace WriteFluency.Users.Tests.Authentication;

public class ClientIpResolverTests
{
    [Fact]
    public void Resolve_ShouldPreferCloudflareConnectingIp_WhenAvailable()
    {
        var resolver = new ClientIpResolver();
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("172.69.39.83");
        httpContext.Request.Headers["X-Forwarded-For"] = "172.69.39.83";
        httpContext.Request.Headers["CF-Connecting-IP"] = "50.114.87.34";

        var result = resolver.Resolve(httpContext);

        result.ShouldNotBeNull();
        result.ToString().ShouldBe("50.114.87.34");
    }

    [Fact]
    public void Resolve_ShouldUseFirstAddressFromXForwardedFor()
    {
        var resolver = new ClientIpResolver();
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("172.69.39.83");
        httpContext.Request.Headers["X-Forwarded-For"] = "203.0.113.24, 172.69.138.69";

        var result = resolver.Resolve(httpContext);

        result.ShouldNotBeNull();
        result.ToString().ShouldBe("203.0.113.24");
    }

    [Fact]
    public void Resolve_ShouldSkipInvalidForwardedEntryAndUseNextValidIp()
    {
        var resolver = new ClientIpResolver();
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("172.69.39.83");
        httpContext.Request.Headers["X-Forwarded-For"] = "unknown, 198.51.100.42";

        var result = resolver.Resolve(httpContext);

        result.ShouldNotBeNull();
        result.ToString().ShouldBe("198.51.100.42");
    }

    [Fact]
    public void Resolve_ShouldFallbackToRemoteIpAddress_WhenHeadersAreMissing()
    {
        var resolver = new ClientIpResolver();
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("::ffff:198.51.100.80");

        var result = resolver.Resolve(httpContext);

        result.ShouldNotBeNull();
        result.ToString().ShouldBe("198.51.100.80");
    }
}
