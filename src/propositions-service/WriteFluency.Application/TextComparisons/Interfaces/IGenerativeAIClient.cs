using FluentResults;
using WriteFluency.Propositions;

namespace WriteFluency.TextComparisons;

public interface IGenerativeAIClient
{
    Task<string> GenerateTextAsync(GetPropositionDto generateTextDto, int attempt = 1, CancellationToken cancellationToken = default);
    Task<Result<AIGeneratedTextDto>> GenerateTextAsync(ComplexityEnum complexity, string articleContent, CancellationToken cancellationToken = default);
}
