using System.ComponentModel.DataAnnotations;

namespace WriteFluency.Users.WebApi.Data;

public class UserLoginActivity
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public DateTimeOffset OccurredAtUtc { get; set; } = DateTimeOffset.UtcNow;

    [Required]
    public string AuthMethod { get; set; } = string.Empty;

    public string? AuthProvider { get; set; }

    public string? IpAddress { get; set; }

    public string? CountryIsoCode { get; set; }

    public string? CountryName { get; set; }

    public string? City { get; set; }

    [Required]
    public string GeoLookupStatus { get; set; } = string.Empty;
}
