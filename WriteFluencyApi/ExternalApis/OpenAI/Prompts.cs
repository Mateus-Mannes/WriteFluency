using WriteFluencyApi.Dtos.ListenAndWrite;
using WriteFluencyApi.Shared;

namespace WriteFluencyApi.ExternalApis.OpenAI;

public static class Prompts
{
    public static string GenerateText(GeneratePropositionDto dto) 
        => @$"
            Write about some subject related to {dto.Subject.GetDescription()}.
            Maximum of one paragraph, from 250 to 600 characteres.
            Write it in a way that normal people can understand well, without specialist vocabulary.
            Write just the text please.
            Without titles.
            Without identation, like paragraphs.
            Without line breaks.
            Without special characters, like quotes. 
            Don't use %, use 'percent'.
            Don't use $100, use '100 dollars'.
            Don't write numbers with thousands separator like 100,000.00, write just 100000.00.
            Be creative.
            {dto.Complexity.GetDescription()}
        ";
}
