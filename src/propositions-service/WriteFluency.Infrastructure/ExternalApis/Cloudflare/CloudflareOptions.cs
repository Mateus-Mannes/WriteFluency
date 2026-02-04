namespace WriteFluency.Infrastructure.ExternalApis;

public class CloudflareOptions
{
    public static readonly string Section = "ExternalApis:Cloudflare";

    public string BaseAddress { get; set; } = "https://api.cloudflare.com/client/v4/";
    public string ZoneName { get; set; } = "writefluency.com";
    public string? ZoneId { get; set; } = "7441ef61fbb296b61b4cb4e418f4ebc6";
    public string? ApiToken { get; set; }
    public bool WarmupEnabled { get; set; } = true;
    public int WarmupIntervalHours { get; set; } = 2;
    public int WarmupRecentPropositionsLimit { get; set; } = 120;
    public string AssetsBaseAddress { get; set; } = "https://minioapi.writefluency.com";
    public int WarmupConcurrency { get; set; } = 6;
    public int WarmupTimeoutSeconds { get; set; } = 20;
    public string[] PurgeUrls { get; set; } =
    [
        "https://writefluency.com/",
        "https://writefluency.com/exercises",
        "https://writefluency.com/sitemap.xml"
    ];
}
