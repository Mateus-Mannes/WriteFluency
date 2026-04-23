using System.Security.Claims;

namespace WriteFluency.Users.WebApi.Authentication;

public interface ILoginActivityRecorder
{
    Task RecordIfApplicableAsync(
        HttpContext httpContext,
        ClaimsPrincipal? principal,
        CancellationToken cancellationToken = default);
}
