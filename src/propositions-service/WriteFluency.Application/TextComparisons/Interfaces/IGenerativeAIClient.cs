using FluentResults;
using WriteFluency.Propositions;

namespace WriteFluency.TextComparisons;

public interface IGenerativeAIClient
{
    Task<Result<AIGeneratedTextDto>> GenerateTextAsync(ComplexityEnum complexity, string articleContent, CancellationToken cancellationToken = default);
    Task<Result<bool>> ValidateImageAsync(byte[] imageBytes, string articleTitle, CancellationToken cancellationToken = default);
}
