namespace WriteFluencyApi.ExternalApis.TextToSpeech;

public class TextToSpeechConfig
{
    public static readonly string Config = "ExternalApis:TextToSpeech";

    public string KeyName { get; set; } = null!;
    public string Key { get; set; } = null!;
    public string BaseAddress { get; set; } = null!;
    public Routes Routes { get; set; } = null!;
}

public class Routes
{
    public string TextSynthesize { get; set; } = null!;
}
