using FluentResults;
using WriteFluency.Propositions;

namespace WriteFluency.TextComparisons;

public interface IGenerativeAIClient
{
    Task<string> GenerateTextAsync(GeneratePropositionDto generateTextDto, int attempt = 1, CancellationToken cancellationToken = default);
    Task<Result<AIGeneratedTextDto>> GenerateTextAsync(ComplexityEnum complexity, string articleContent, CancellationToken cancellationToken = default);
    Task<Result<AudioDto>> GenerateAudioAsync(string text, CancellationToken cancellationToken = default);
}
