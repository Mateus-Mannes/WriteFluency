namespace WriteFluency.Infrastructure.ExternalApis;

public class OpenAIOptions
{
    public static readonly string Section = "ExternalApis:OpenAI";

    public required string Key { get; set; }
    public required string BaseAddress { get; set; }
    public string ArticleValidationModel { get; set; } = "gpt-5.4-nano-2026-03-17";
    public required OpenAIRoutes Routes { get; set; }

    public class OpenAIRoutes
    {
        public required string Completion { get; set; }
        public required string Speech { get; set; }
    }
}
