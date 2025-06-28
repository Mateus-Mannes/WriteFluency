using Microsoft.AspNetCore.Mvc;
using WriteFluency.Propositions;

namespace WriteFluency.TextComparisons;

[ApiController]
[Route("text-comparison")]
public class TextComparisonController : ControllerBase
{
    private readonly TextComparisonService _textComparisonService;

    public TextComparisonController(TextComparisonService textComparisonService)
    {
        _textComparisonService = textComparisonService;
    }

    [HttpPost("compare-texts")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(TopicsDto))]
    public IActionResult CompareTexts([FromBody] CompareTextsDto compareTextsDto)
    {
        return Ok(_textComparisonService.CompareTexts(compareTextsDto.OriginalText, compareTextsDto.UserText));
    }
}
