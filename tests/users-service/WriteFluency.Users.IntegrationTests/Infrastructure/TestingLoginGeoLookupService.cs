using System.Net;
using WriteFluency.Users.WebApi.Authentication;

namespace WriteFluency.Users.IntegrationTests.Infrastructure;

public sealed class TestingLoginGeoLookupService : ILoginGeoLookupService
{
    public bool ReturnError { get; set; }

    public LoginGeoLookupResult Lookup(IPAddress? ipAddress)
    {
        if (ReturnError)
        {
            return LoginGeoLookupResult.Error();
        }

        return LoginGeoLookupResult.Success("US", "United States", "Seattle");
    }

    public void Reset()
    {
        ReturnError = false;
    }
}
