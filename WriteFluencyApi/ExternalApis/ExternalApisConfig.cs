namespace WriteFluencyApi.ExternalApis;

public class ExternalApisConfig
{
    public static readonly string ExternalApis = "ExternalApis";
    public OpenAIConfig OpenAI { get; set; } = null!;
    public GoogleCloudConfig GoogleCloud { get; set; } = null!;
}

public class OpenAIConfig
{
    public string Key { get; set; }
    public string Url { get; set; }
}

public class GoogleCloudConfig
{
    public string Key { get; set; }
    public string Url { get; set; }
}
