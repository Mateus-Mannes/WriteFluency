using Microsoft.AspNetCore.Mvc;
using WriteFluencyApi.Dtos.ListenAndWrite;
using WriteFluencyApi.Services.ListenAndWrite;

namespace WriteFluencyApi.Controllers.ListenAndWrite;

[ApiController]
[Route("[controller]")]
public class ListenAndWriteController : ControllerBase
{
    private readonly ITextGenerator _textGenerator;

    public ListenAndWriteController(ITextGenerator textGenerator)
    {
        _textGenerator = textGenerator;
    }


    [HttpPost]
    public IActionResult GenerateText(GenerateTextDto generateTextDto)
    {
        var text = _textGenerator.GenerateText(generateTextDto);
        return Ok(text);
    }
}
