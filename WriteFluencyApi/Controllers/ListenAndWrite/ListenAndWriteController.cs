using Microsoft.AspNetCore.Mvc;
using WriteFluencyApi.ListenAndWrite.Domain;

namespace WriteFluencyApi.ListenAndWrite.Controllers;

[ApiController]
[Route("listen-and-write")]
public class ListenAndWriteController : ControllerBase
{
    private readonly ITextGenerator _textGenerator;
    private readonly ISpeechGenerator _speechGenerator;
    private readonly TextComparisionService _textComparisionService;

    public ListenAndWriteController(
        ITextGenerator textGenerator, 
        ISpeechGenerator speechGenerator,
        TextComparisionService textComparisionService)
    {
        _textGenerator = textGenerator;
        _speechGenerator = speechGenerator;
        _textComparisionService = textComparisionService;
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

    [HttpPost("compare-texts")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(TopicsDto))]
    public IActionResult CompareTexts([FromBody] CompareTextsDto compareTextsDto)
    {
        return Ok(_textComparisionService.CompareTexts(compareTextsDto.OriginalText, compareTextsDto.UserText));
    }
}
