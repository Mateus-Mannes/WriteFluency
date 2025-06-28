namespace WriteFluency.Authentication;

public class JwtOptions
{
    public const string Section = "Jwt";
    public required string Issuer { get; set; }
    public required string Audience { get; set; }
    public required string Key { get; set; }
}
