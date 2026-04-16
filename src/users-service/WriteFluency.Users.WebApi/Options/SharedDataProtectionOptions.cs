namespace WriteFluency.Users.WebApi.Options;

public sealed class SharedDataProtectionOptions
{
    public const string SectionName = "SharedDataProtection";

    public string ApplicationName { get; set; } = "WriteFluency.SharedAuth";

    public string BlobUri { get; set; } = string.Empty;

    public string KeyIdentifier { get; set; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApplicationName)
        && !string.IsNullOrWhiteSpace(BlobUri)
        && !string.IsNullOrWhiteSpace(KeyIdentifier);
}
