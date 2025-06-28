using Microsoft.AspNetCore.Authorization;

namespace WriteFluency.WebApi;

public class AppAuthorizeAttribute : AuthorizeAttribute
{
    public const string AuthorizationPolicyName = "AppAuthorizationPolicy";

    public AppAuthorizeAttribute()
    {
        Policy = AuthorizationPolicyName;
    }
}
