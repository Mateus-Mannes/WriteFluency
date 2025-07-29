namespace WriteFluency.Infrastructure.ExternalApis;

public class TextToSpeechOptions
{
    public static readonly string Section = "ExternalApis:TextToSpeech";

    public required string KeyName { get; set; }
    public required string Key { get; set; }
    public required string BaseAddress { get; set; }
    public required TextToSpeechRoutes Routes { get; set; }

    public class TextToSpeechRoutes
    {
        public required string TextSynthesize { get; set; }
    }
}
