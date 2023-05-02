namespace WriteFluencyApi.ExternalApis.OpenAI;

public class OpenAIConfig
{
    public static readonly string Config = "ExternalApis:OpenAI";

    public string Key { get; set; } = null!;
    public string BaseAddress { get; set; } = null!;
    public Routes Routes { get; set; } = null!;
}

public class Routes
{
    public string Completion { get; set; } = null!;
}
