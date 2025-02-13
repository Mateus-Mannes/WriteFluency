namespace WriteFluencyApi.ListenAndWrite.Domain;

public interface ISpeechGenerator
{
    Task<byte[]> GenerateSpeechAsync(string text, int attempt = 1);
}
