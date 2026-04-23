namespace WriteFluency.Users.WebApi.Authentication;

internal readonly record struct LoginAuthContext(string AuthMethod, string? AuthProvider);

internal static class LoginAuthContextResolver
{
    private const string UsersAuthLoginPath = "/users/auth/login";
    private const string UsersAuthPasswordlessVerifyPath = "/users/auth/passwordless/verify";
    private const string UsersAuthExternalPrefix = "/users/auth/external/";
    private const string CallbackSegment = "callback";

    public static bool TryResolve(HttpRequest request, out LoginAuthContext context)
    {
        var path = request.Path.Value ?? string.Empty;

        if (HttpMethods.IsPost(request.Method)
            && string.Equals(path, UsersAuthLoginPath, StringComparison.OrdinalIgnoreCase))
        {
            context = new LoginAuthContext("password", null);
            return true;
        }

        if (HttpMethods.IsPost(request.Method)
            && string.Equals(path, UsersAuthPasswordlessVerifyPath, StringComparison.OrdinalIgnoreCase))
        {
            context = new LoginAuthContext("otp", null);
            return true;
        }

        if (!HttpMethods.IsGet(request.Method))
        {
            context = default;
            return false;
        }

        if (!path.StartsWith(UsersAuthExternalPrefix, StringComparison.OrdinalIgnoreCase))
        {
            context = default;
            return false;
        }

        var provider = ResolveProviderFromPath(path)
            ?? request.RouteValues["provider"]?.ToString();
        if (string.IsNullOrWhiteSpace(provider))
        {
            context = default;
            return false;
        }

        context = new LoginAuthContext("external", provider.Trim().ToLowerInvariant());
        return true;
    }

    private static string? ResolveProviderFromPath(string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 5)
        {
            return null;
        }

        if (!string.Equals(segments[0], "users", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(segments[1], "auth", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(segments[2], "external", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(segments[4], CallbackSegment, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var provider = segments[3].Trim();
        return string.IsNullOrWhiteSpace(provider) ? null : provider;
    }
}
