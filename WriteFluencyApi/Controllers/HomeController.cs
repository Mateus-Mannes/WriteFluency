using Microsoft.AspNetCore.Mvc;

namespace WriteFluencyApi.Controllers;

[ApiController]
[Route("")]
public class HomeController : ControllerBase
{
    [HttpGet]
    public string Get()
    {
        return "Hello, World!";
    }
}
