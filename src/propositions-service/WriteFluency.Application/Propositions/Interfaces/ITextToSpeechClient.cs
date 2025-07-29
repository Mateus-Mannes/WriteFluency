namespace WriteFluency.Propositions;

public interface ITextToSpeechClient
{
    Task<byte[]> GenerateSpeechAsync(string text, int attempt = 1);
}
