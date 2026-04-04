namespace WriteFluency.Users.WebApi.Options;

public sealed class ExternalAuthenticationOptions
{
    public const string SectionName = "Authentication";

    public required ProviderOptions Google { get; set; }
    public required ProviderOptions Microsoft { get; set; }
    public required string ConfirmationRedirectUrl { get; set; }

    public required RedirectOptions ExternalRedirect { get; set; }
}

public sealed class ProviderOptions
{
    public required string ClientId { get; set; }
    public required string ClientSecret { get; set; }
}

public sealed class RedirectOptions
{
    public required string[] AllowedReturnUrls { get; set; }
}
