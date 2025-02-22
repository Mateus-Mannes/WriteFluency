using Microsoft.AspNetCore.Authorization;

namespace WriteFluencyApi.Domain.Login;

public class AppAuthorizeAttribute : AuthorizeAttribute
{
    public const string AuthorizationPolicyName = "AppAuthorizationPolicy";

    public AppAuthorizeAttribute()
    {
        Policy = AuthorizationPolicyName;
    }
}
