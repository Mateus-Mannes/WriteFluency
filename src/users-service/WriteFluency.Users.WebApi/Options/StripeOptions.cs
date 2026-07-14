using System.ComponentModel.DataAnnotations;

namespace WriteFluency.Users.WebApi.Options;

public sealed class StripeOptions
{
    public const string SectionName = "Stripe";

    [Required]
    public string SecretKey { get; set; } = string.Empty;

    [Required]
    public string ProMonthlyPriceId { get; set; } = string.Empty;

    [Required]
    public string SuccessUrl { get; set; } = string.Empty;

    [Required]
    public string CancelUrl { get; set; } = string.Empty;

    [Required]
    public string PortalConfigurationId { get; set; } = string.Empty;

    [Required]
    public string PortalReturnUrl { get; set; } = string.Empty;

    [Required]
    public string WebhookSecret { get; set; } = string.Empty;
}
