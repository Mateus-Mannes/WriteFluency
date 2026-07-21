namespace WriteFluency.Infrastructure.ExternalApis;

public class NewsOptions
{
    public static readonly string Section = "ExternalApis:News";

    public required string Key { get; set; }
    public required string BaseAddress { get; set; }
    public required NewsRoutes Routes { get; set; }
    public int AttemptTimeoutSeconds { get; set; } = 30;
    public int TotalRequestTimeoutSeconds { get; set; } = 90;

    public class NewsRoutes
    {
        public required string TopStories { get; set; }
    }
}
