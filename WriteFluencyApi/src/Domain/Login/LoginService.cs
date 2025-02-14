using FluentResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using WriteFluencyApi.Data;

namespace WriteFluencyApi.Domain.Login;

public class LoginService
{
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly JwtOptions _jwtOptions;

    public LoginService(
        SignInManager<IdentityUser> signInManager,
        IUserStore<IdentityUser> userStorage,
        IOptions<JwtOptions> jwtOptions,
        UserManager<IdentityUser> userManager)
    {
        _signInManager = signInManager;
        _jwtOptions = jwtOptions.Value;
        _userManager = userManager;
    }

    public async Task<Result<string>> LoginAsync(LoginRequest loginRequest)
    {
        var user = await _userManager.FindByEmailAsync(loginRequest.Email);

        if (user is null) return Result.Fail("User not found.");

        var result = await _signInManager.CheckPasswordSignInAsync(user, loginRequest.Password, false);

        if (!result.Succeeded) return Result.Fail("Invalid login request.");

        var signCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Email, user.Email!)
            },
            expires: DateTime.Now.AddMinutes(30),
            signingCredentials: signCredentials);

        var handler = new JwtSecurityTokenHandler();
        var tokenString = handler.WriteToken(token);

        return Result.Ok(tokenString);
    }
}
