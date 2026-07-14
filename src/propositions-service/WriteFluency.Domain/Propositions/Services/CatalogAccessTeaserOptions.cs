using System.ComponentModel.DataAnnotations;

namespace WriteFluency.Propositions;

public sealed class CatalogAccessTeaserOptions
{
    public const string Section = "Propositions:CatalogAccessTeaser";

    public bool Enabled { get; set; } = true;

    [Range(1, 1000)]
    public int AnonymousSampleLifetimeLimit { get; set; } = 1;

    [Range(1, 1000)]
    public int FreeIntroLifetimeLimit { get; set; } = 1;

    public string? AnonymousFingerprintSalt { get; set; }
}
