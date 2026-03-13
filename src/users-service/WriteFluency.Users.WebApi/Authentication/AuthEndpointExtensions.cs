using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WriteFluency.Users.WebApi.Data;

namespace WriteFluency.Users.WebApi.Authentication;

public static class AuthEndpointExtensions
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var authGroup = app.MapGroup("/auth").WithTags("Authentication");

        authGroup.MapIdentityApi<ApplicationUser>();

        authGroup.MapPost("/logout", LogoutAsync)
            .RequireAuthorization();

        authGroup.MapGet("/session", GetSession)
            .RequireAuthorization();

        authGroup.MapPost("/passwordless/request", RequestPasswordlessOtpAsync);

        authGroup.MapPost("/passwordless/verify", VerifyPasswordlessOtpAsync);

        return app;
    }

    private static async Task<IResult> LogoutAsync(SignInManager<ApplicationUser> signInManager, [FromBody] object payload)
    {
        if (payload is null)
        {
            return Results.BadRequest();
        }

        await signInManager.SignOutAsync();
        return Results.Ok();
    }

    private static async Task<IResult> GetSession(ClaimsPrincipal principal, UserManager<ApplicationUser> userManager)
    {
        var user = await userManager.GetUserAsync(principal);

        return Results.Ok(new
        {
            IsAuthenticated = principal.Identity?.IsAuthenticated ?? false,
            UserId = user?.Id ?? principal.FindFirstValue(ClaimTypes.NameIdentifier),
            Email = user?.Email ?? principal.FindFirstValue(ClaimTypes.Email),
            EmailConfirmed = user?.EmailConfirmed ?? false
        });
    }

    private static async Task<IResult> RequestPasswordlessOtpAsync(
        PasswordlessRequest request,
        HttpContext httpContext,
        PasswordlessOtpService passwordlessOtpService,
        CancellationToken cancellationToken)
    {
        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        await passwordlessOtpService.RequestOtpAsync(request.Email, ipAddress, cancellationToken);

        return Results.Ok(new
        {
            Message = "If the account is eligible, a verification code was sent."
        });
    }

    private static async Task<IResult> VerifyPasswordlessOtpAsync(
        PasswordlessVerifyRequest request,
        PasswordlessOtpService passwordlessOtpService,
        CancellationToken cancellationToken)
    {
        var verified = await passwordlessOtpService.VerifyOtpAndSignInAsync(request.Email, request.Code, cancellationToken);
        return verified ? Results.Ok() : Results.Unauthorized();
    }

    public record PasswordlessRequest([Required, EmailAddress] string Email);

    public record PasswordlessVerifyRequest([Required, EmailAddress] string Email, [Required] string Code);
}
