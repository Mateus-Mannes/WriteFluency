using WriteFluency.Extensions;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http.HttpResults;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using WriteFluency.Endpoints;

namespace WriteFluency.Authentication;

public class AuthenticationEndpointGroup : IEndpointMapper
{
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("authentication").WithTags("Authentication");
        group.MapPost("token", CreateTokenAsync);
        group.MapPost("register", RegisterAsync);
    }

    private async Task<Results<Ok<string>, BadRequest<string>>> CreateTokenAsync(LoginRequest loginRequest,
        JwtTokenService jwtTokenService)
    {
        var loginResult = await jwtTokenService.LoginAsync(loginRequest.Email, loginRequest.Password);
        return loginResult.IsSuccess switch
        {
            true => TypedResults.Ok(loginResult.Value),
            false => TypedResults.BadRequest(loginResult.Errors.Message())
        };
    }


    private record RegisterUserRequest(
        [Required] string UserName,
        [Required] string UserEmail,
        [Required] string UserPassword);
    private async Task<Results<Ok, BadRequest<ValidationProblemDetails>>> RegisterAsync(
        RegisterUserRequest request,
        UserManager<IdentityUser> userManager)
    {
        var user = new IdentityUser
        {
            UserName = request.UserName,
            Email = request.UserEmail
        };

        var result = await userManager.CreateAsync(user, request.UserPassword);

        return result.Succeeded switch
        {
            true => TypedResults.Ok(),
            false => TypedResults.BadRequest(new ValidationProblemDetails(
                result.Errors.GroupBy(e => e.Code).ToDictionary(
                    group => group.Key,
                    group => group.Select(e => e.Description).ToArray()))
            )
        };
    }
}
