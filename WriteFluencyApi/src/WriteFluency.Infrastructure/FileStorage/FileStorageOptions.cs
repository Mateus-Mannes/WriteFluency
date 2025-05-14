namespace WriteFluency.Infrastructure.FileStorage;

public class FileStorageOptions
{
    public const string Section = "FileStorage";
    public required string AccessKey { get; set; }
    public required string SecretKey { get; set; }
    public required string Endpoint { get; set; }
}
