using FluentResults;
using WriteFluency.Propositions;

namespace WriteFluency.TextComparisons;

public interface IGenerativeAIClient
{
    Task<string> GenerateTextAsync(GeneratePropositionDto generateTextDto, int attempt = 1);
    Task<Result<string>> GenerateTextAsync(ComplexityEnum complexity, string articleContent);
    Task<Result<AudioDto>> GenerateAudioAsync(string text);
}
