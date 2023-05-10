using Microsoft.AspNetCore.Mvc;
using WriteFluencyApi.Dtos.ListenAndWrite;
using WriteFluencyApi.Services.ListenAndWrite;
using WriteFluencyApi.Shared.ListenAndWrite;

namespace WriteFluencyApi.Controllers.ListenAndWrite;

[ApiController]
[Route("[controller]")]
public class ListenAndWriteController : ControllerBase
{
    private readonly ITextGenerator _textGenerator;
    private readonly ISpeechGenerator _speechGenerator;

    public ListenAndWriteController(
        ITextGenerator textGenerator, 
        ISpeechGenerator speechGenerator)
    {
        _textGenerator = textGenerator;
        _speechGenerator = speechGenerator;
    }

    [HttpPost("generate-proposition")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PropositionDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GenerateProposition(GeneratePropositionDto generatePropositionDto)
    {
        try
        {
            var text = await _textGenerator.GenerateTextAsync(generatePropositionDto);
            var audio = await _speechGenerator.GenerateSpeechAsync(text);
            return Ok(new PropositionDto(text, audio));
        }
        catch (Exception)
        {
            return StatusCode(500, "Enable to generate proposition");
        }
    }

    [HttpGet("topics")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(TopicsDto))]
    public IActionResult GetTopics()
    {
        return Ok(new TopicsDto(
            Enum.GetNames(typeof(ComplexityEnum)),
            Enum.GetNames(typeof(SubjectEnum))));
    }
}
