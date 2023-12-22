using WriteFluencyApi.ListenAndWrite;

namespace WriteFluencyApi.ListenAndWrite.Domain;

public interface ITextGenerator
{
    Task<string> GenerateTextAsync(GeneratePropositionDto generateTextDto, int attempt = 1);
}
