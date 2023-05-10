using WriteFluencyApi.Dtos.ListenAndWrite;
using WriteFluencyApi.Shared;

namespace WriteFluencyApi.ExternalApis.OpenAI;

public static class Prompts
{
    public static string GenerateText(GeneratePropositionDto dto) 
        => @$"
            Write about some subject related to {dto.Subject}.
            Maximum of one paragraph, from 250 to 600 characteres.
            Write it in a way that normal people can understand well, without specialist vocabulary.
            Write just the text please, without title.
            Be creative.
            {dto.Complexity.GetDescription()}
        ";
}
