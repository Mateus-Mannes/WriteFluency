using Microsoft.Azure.Functions.Worker.Http;

namespace WriteFluency.UsersProgressService.Authentication;

public interface ISharedCookieAuthenticationService
{
    CookieAuthenticationResult Authenticate(HttpRequestData request);
}
