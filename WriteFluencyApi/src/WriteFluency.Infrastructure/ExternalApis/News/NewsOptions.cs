namespace WriteFluency.Infrastructure.ExternalApis;

public class NewsOptions
{
    public static readonly string Section = "ExternalApis:News";

    public required string Key { get; set; }
    public required string BaseAddress { get; set; }
    public required NewsRoutes Routes { get; set; }

    public class NewsRoutes
    {
        public required string TopStories { get; set; }
    }
}
