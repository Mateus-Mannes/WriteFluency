namespace WriteFluency.Users.WebApi.Authentication;

public sealed record LoginGeoLookupResult(
    string GeoLookupStatus,
    string? CountryIsoCode,
    string? CountryName,
    string? City)
{
    public static LoginGeoLookupResult Success(string? countryIsoCode, string? countryName, string? city)
        => new("success", countryIsoCode, countryName, city);

    public static LoginGeoLookupResult NotFound()
        => new("not_found", null, null, null);

    public static LoginGeoLookupResult PrivateIp()
        => new("private_ip", null, null, null);

    public static LoginGeoLookupResult NoIp()
        => new("no_ip", null, null, null);

    public static LoginGeoLookupResult Disabled()
        => new("disabled", null, null, null);

    public static LoginGeoLookupResult Error()
        => new("error", null, null, null);
}
