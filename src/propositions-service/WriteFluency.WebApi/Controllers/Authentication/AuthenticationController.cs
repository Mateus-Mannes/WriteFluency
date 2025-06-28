using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using WriteFluency.Extensions;

namespace WriteFluency.Authentication;

[ApiController]
[Route("authentication")]
public class AuthenticationController : ControllerBase
{
    private readonly JwtTokenService _jwtTokenService;
    private readonly UserManager<IdentityUser> _userManager;

    public AuthenticationController(JwtTokenService jwtTokenService, UserManager<IdentityUser> userManager)
    {
        _jwtTokenService = jwtTokenService;
        _userManager = userManager;
    }

    [HttpPost]
    [Route("token")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(string))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateTokeAsync(LoginRequest loginRequest)
    {
        var loginResult = await _jwtTokenService.LoginAsync(loginRequest.Email, loginRequest.Password);
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
        [Required] string userName,
        [Required] string userEmail,
        [Required] string userPassword)
    {
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
