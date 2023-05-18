namespace WriteFluencyApi.Services.ListenAndWrite;

public interface ISpeechGenerator
{
    Task<byte[]> GenerateSpeechAsync(string text, int attempt = 1);    
}
