namespace WriteFluencyApi.Services.ListenAndWrite;

public interface ISpeechGenerator
{
    Task<byte[]> GenerateSpeechAsync(string text);    
}
