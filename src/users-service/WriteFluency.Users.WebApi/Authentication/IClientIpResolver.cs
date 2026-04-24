using System.Net;

namespace WriteFluency.Users.WebApi.Authentication;

public interface IClientIpResolver
{
    IPAddress? Resolve(HttpContext httpContext);
}
