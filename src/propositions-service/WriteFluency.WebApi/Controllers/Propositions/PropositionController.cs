using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WriteFluency.Data;
using WriteFluency.TextComparisons;

namespace WriteFluency.Propositions;

[ApiController]
[Route("proposition")]
public class PropositionController : ControllerBase
{
    private readonly IGenerativeAIClient _textGenerator;
    private readonly ITextToSpeechClient _speechGenerator;
    private readonly ILogger<PropositionController> _logger;
    private readonly IAppDbContext _dbContext;

    public PropositionController(
        IGenerativeAIClient textGenerator,
        ITextToSpeechClient speechGenerator,
        ILogger<PropositionController> logger,
        IAppDbContext dbContext)
    {
        _logger = logger;
        _textGenerator = textGenerator;
        _speechGenerator = speechGenerator;
        _dbContext = dbContext;
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

            await _dbContext.Subjects.ToListAsync(); // dummy request to test tracing
            
            _logger.LogInformation("Generated proposition for: {dto}", generatePropositionDto); 
            return Ok(new PropositionDto(text, audio));
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error generating proposition");
            return StatusCode(500, "Unable to generate proposition");
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
