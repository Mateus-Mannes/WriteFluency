using System.Net.Mail;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WriteFluency.Users.WebApi.Authentication;
using WriteFluency.Users.WebApi.Data;
using WriteFluency.Users.WebApi.Email;
using WriteFluency.Users.WebApi.Options;

namespace WriteFluency.Users.WebApi.Support;

public static class SupportRequestEndpointExtensions
{
    private const int MaxMessageLength = 4000;
    private const string Subject = "WriteFluency support request";

    public static IEndpointRouteBuilder MapSupportRequestEndpoints(this IEndpointRouteBuilder app)
    {
        var supportGroup = app.MapGroup("/support").WithTags("Support");

        supportGroup.MapPost("/requests", SubmitSupportRequestAsync)
            .WithSummary("Submit a support request")
            .WithDescription("Accepts guest and authenticated support requests and emails them to configured support recipients.");

        return app;
    }

    private static async Task<IResult> SubmitSupportRequestAsync(
        [FromBody] SupportRequestPayload? request,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        IClientIpResolver clientIpResolver,
        SupportRequestRateLimiter rateLimiter,
        IAppEmailSender emailSender,
        IOptions<SupportRequestOptions> options,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("WriteFluency.Users.SupportRequests");
        if (request is null)
        {
            return Results.BadRequest(new
            {
                Error = "message_required",
                Message = "Please describe what you need help with."
            });
        }

        var validationResult = Validate(request);
        if (validationResult is not null)
        {
            return validationResult;
        }

        var message = request.Message.Trim();
        var replyEmail = NormalizeOptional(request.ReplyEmail);
        var sourceUrl = NormalizeOptional(request.SourceUrl);
        var clientIp = clientIpResolver.Resolve(httpContext)?.ToString() ?? "unknown";

        if (!await rateLimiter.CanSubmitAsync(clientIp))
        {
            return Results.Json(
                new
                {
                    Error = "support_rate_limited",
                    Message = "Too many support requests were submitted recently. Please try again later."
                },
                statusCode: StatusCodes.Status429TooManyRequests);
        }

        var user = principal.Identity?.IsAuthenticated == true
            ? await userManager.GetUserAsync(principal)
            : null;
        var userId = user?.Id ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var userEmail = user?.Email ?? principal.FindFirstValue(ClaimTypes.Email);
        var recipientEmails = options.Value.RecipientEmails
            .Where(email => !string.IsNullOrWhiteSpace(email))
            .Select(email => email.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        logger.LogInformation(
            "Support request submitted. MessageLength={MessageLength}, UserId={UserId}, UserEmail={UserEmail}, HasReplyEmail={HasReplyEmail}, SourceUrl={SourceUrl}, RecipientCount={RecipientCount}",
            message.Length,
            userId,
            userEmail,
            !string.IsNullOrWhiteSpace(replyEmail),
            sourceUrl,
            recipientEmails.Length);

        var content = SupportRequestEmailBuilder.Build(new SupportRequestEmailModel(
            message,
            replyEmail,
            userId,
            userEmail,
            sourceUrl,
            clientIp,
            DateTimeOffset.UtcNow));

        try
        {
            foreach (var recipientEmail in recipientEmails)
            {
                await emailSender.SendAsync(recipientEmail, Subject, content.HtmlBody, content.TextBody, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send support request email.");
            return Results.Problem(
                detail: "Unable to send the support request right now.",
                statusCode: StatusCodes.Status500InternalServerError);
        }

        return Results.Ok(new { Accepted = true });
    }

    private static IResult? Validate(SupportRequestPayload request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return Results.BadRequest(new
            {
                Error = "message_required",
                Message = "Please describe what you need help with."
            });
        }

        if (request.Message.Trim().Length > MaxMessageLength)
        {
            return Results.BadRequest(new
            {
                Error = "message_too_long",
                Message = $"Support messages must be {MaxMessageLength} characters or fewer."
            });
        }

        var replyEmail = NormalizeOptional(request.ReplyEmail);
        if (!string.IsNullOrWhiteSpace(replyEmail) && !IsValidEmail(replyEmail))
        {
            return Results.BadRequest(new
            {
                Error = "reply_email_invalid",
                Message = "Enter a valid reply email address."
            });
        }

        return null;
    }

    private static string? NormalizeOptional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var address = new MailAddress(email);
            return string.Equals(address.Address, email, StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

public sealed record SupportRequestPayload(
    string Message,
    string? ReplyEmail,
    string? SourceUrl);
