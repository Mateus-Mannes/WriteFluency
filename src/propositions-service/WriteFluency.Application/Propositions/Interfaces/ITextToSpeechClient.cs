using FluentResults;

namespace WriteFluency.Propositions;

public interface ITextToSpeechClient
{
    Task<Result<AudioDto>> GenerateAudioAsync(string text, CancellationToken cancellationToken = default);
}
