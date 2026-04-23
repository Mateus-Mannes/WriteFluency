using System.Net;

namespace WriteFluency.Users.WebApi.Authentication;

public interface ILoginGeoLookupService
{
    LoginGeoLookupResult Lookup(IPAddress? ipAddress);
}
