namespace WriteFluency.Users.WebApi.Options;

public sealed class LoginLocationOptions
{
    public const string SectionName = "LoginLocation";

    public bool Enabled { get; set; } = true;

    public string GeoLite2CityBlobUri { get; set; } = string.Empty;

    public int BlobMetadataRefreshMinutes { get; set; } = 60;

    public string GeoLite2CityDbPath { get; set; } = "/app/data/GeoLite2-City.mmdb";
}
