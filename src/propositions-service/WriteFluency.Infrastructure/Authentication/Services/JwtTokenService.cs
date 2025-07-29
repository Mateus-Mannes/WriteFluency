using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using FluentResults;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

namespace WriteFluency.Authentication;

public class JwtTokenService
{
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly JwtOptions _jwtOptions;

    public JwtTokenService(
        SignInManager<IdentityUser> signInManager,
        IUserStore<IdentityUser> userStorage,
        IOptionsSnapshot<JwtOptions> jwtOptions,
        UserManager<IdentityUser> userManager)
    {
        _signInManager = signInManager;
        _jwtOptions = jwtOptions.Value;
        _userManager = userManager;
    }

    public async Task<Result<string>> LoginAsync(string email, string password)
    {
        var user = await _userManager.FindByEmailAsync(email);

        if (user is null) return Result.Fail("User not found.");

        var result = await _signInManager.CheckPasswordSignInAsync(user, password, false);

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
