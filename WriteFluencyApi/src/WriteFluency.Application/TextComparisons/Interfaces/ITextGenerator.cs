using WriteFluency.Propositions;

namespace WriteFluency.TextComparisons;

public interface ITextGenerator
{
    Task<string> GenerateTextAsync(GeneratePropositionDto generateTextDto, int attempt = 1);
}
