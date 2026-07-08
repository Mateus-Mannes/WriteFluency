using System.ComponentModel.DataAnnotations;

namespace WriteFluency.TextComparisons;

public sealed class ProReviewTeaserOptions
{
    public const string Section = "TextComparison:ProReviewTeaser";

    public bool Enabled { get; set; } = true;

    [Range(1, 1000)]
    public int AnonymousSampleLifetimeLimit { get; set; } = 1;

    [Range(1, 1000)]
    public int FreeIntroLifetimeLimit { get; set; } = 1;

    [Range(1, 1000)]
    public int FreeMonthlyLimit { get; set; } = 1;

    [Range(1, 240)]
    public int PendingReviewExpiryMinutes { get; set; } = 15;

    public string? AnonymousFingerprintSalt { get; set; }
}
