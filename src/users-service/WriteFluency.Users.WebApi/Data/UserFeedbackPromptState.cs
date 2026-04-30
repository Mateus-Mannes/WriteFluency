using System.ComponentModel.DataAnnotations;

namespace WriteFluency.Users.WebApi.Data;

public class UserFeedbackPromptState
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public string CampaignKey { get; set; } = string.Empty;

    public DateTimeOffset? LastShownAtUtc { get; set; }

    public DateTimeOffset? LastDismissedAtUtc { get; set; }

    public DateTimeOffset? LastSubmittedAtUtc { get; set; }

    public int DismissCount { get; set; }

    public int SubmitCount { get; set; }

    [Required]
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    [Required]
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
