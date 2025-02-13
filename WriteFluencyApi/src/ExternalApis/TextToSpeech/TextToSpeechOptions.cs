namespace WriteFluencyApi.ExternalApis.TextToSpeech;

public class TextToSpeechOptions
{
    public static readonly string Section = "ExternalApis:TextToSpeech";

    public string KeyName { get; set; } = null!;
    public string Key { get; set; } = null!;
    public string BaseAddress { get; set; } = null!;
    public Routes Routes { get; set; } = null!;
}

public class Routes
{
    public string TextSynthesize { get; set; } = null!;
}
