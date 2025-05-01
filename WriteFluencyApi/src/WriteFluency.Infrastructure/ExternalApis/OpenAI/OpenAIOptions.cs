namespace WriteFluency.Infrastructure.ExternalApis;

public class OpenAIOptions
{
    public static readonly string Section = "ExternalApis:OpenAI";

    public required string Key { get; set; }
    public required string BaseAddress { get; set; }
    public required OpenAIRoutes Routes { get; set; }

    public class OpenAIRoutes
    {
        public required string Completion { get; set; }
    }
}
