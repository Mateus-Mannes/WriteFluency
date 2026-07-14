namespace WriteFluency.WebApi.Users;

public class UsersServiceOptions
{
    public const string Section = "UsersService";

    public string BaseUrl { get; set; } = "https://localhost:5101/users";

    public bool HasValidBaseUrl()
    {
        return !string.IsNullOrWhiteSpace(BaseUrl)
            && Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}
