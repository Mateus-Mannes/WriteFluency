using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using WriteFluencyApi.Domain.Login;
using WriteFluencyApi.Shared;

namespace WriteFluencyApi.Controllers.Login;

[ApiController]
[Route("login")]
public class LoginController : ControllerBase
{
    private readonly LoginService _loginService;
    private readonly UserManager<IdentityUser> _userManager;

    public LoginController(LoginService loginService, UserManager<IdentityUser> userManager)
    {
        _loginService = loginService;
        _userManager = userManager;
    }

    [HttpPost]
    [Route("token")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(string))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> LoginAsync(LoginRequest loginRequest)
    {
        var loginResult = await _loginService.LoginAsync(loginRequest);
        return loginResult.IsSuccess switch
        {
            true => Ok(loginResult.Value),
            false => BadRequest(loginResult.Errors.Message())
        };
    }

    [HttpPost]
    [Route("register")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RegisterAsync(
        string userName, 
        string userEmail, 
        string userPassword) 
    {
        if(userEmail is null || userPassword is null || userName is null)
        {
            return BadRequest("Email, password, and username are required");
        }

        var user = new IdentityUser
        {
            UserName = userEmail,
            Email = userEmail
        };

        var result = await _userManager.CreateAsync(user, userPassword);

        return result.Succeeded switch
        {
            true => Ok(),
            false => BadRequest(new ValidationProblemDetails(
                result.Errors.GroupBy(e => e.Code).ToDictionary(
                    group => group.Key,
                    group => group.Select(e => e.Description).ToArray()))
            )
        };
    }
}
