using Microsoft.AspNetCore.Mvc;
using WriteFluencyApi.Dtos.ListenAndWrite;
using WriteFluencyApi.Services.ListenAndWrite;

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

    [HttpPost]
    public async Task<IActionResult> GenerateText(GenerateTextDto generateTextDto)
    {
        var text = await _textGenerator.GenerateTextAsync(generateTextDto);
        var audio = await _speechGenerator.GenerateSpeechAsync(text);
        return Ok(text);
    }
}
