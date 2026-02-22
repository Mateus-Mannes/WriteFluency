using System.Text.RegularExpressions;
using FluentResults;
using WriteFluency.Application.Propositions.Interfaces;

namespace WriteFluency.Propositions;

public class ArticleContentPolicyValidator : IArticleContentPolicyValidator
{
    private static readonly string[] SexualTerms =
    [
        "sexual",
        "sex",
        "porn",
        "pornography",
        "child pornography",
        "rape porn",
        "rape",
        "molest",
        "molestation",
        "csam",
        "child sexual abuse material"
    ];

    private static readonly string[] AbuseTerms =
    [
        "abuse",
        "assault",
        "exploit",
        "exploitation",
        "attack",
        "offender",
        "sexual abuse of children",
        "sexual abuse of minors",
        "sexual exploitation of children",
        "sexual exploitation of minors",
        "baby rape",
        "tot rape",
        "child rape"
    ];

    private static readonly string[] ViolenceTerms =
    [
        "violence",
        "violent",
        "murder",
        "kill",
        "killed",
        "killing",
        "homicide",
        "manslaughter",
        "shooting",
        "shot",
        "gunfire",
        "stab",
        "stabbing",
        "behead",
        "torture",
        "gore",
        "dismember",
        "kidnap",
        "abduction",
        "terror",
        "terrorist",
        "terrorism",
        "bombing",
        "war",
        "armed conflict"
    ];

    private static readonly string[] CommercialContentTerms =
    [
        "best",
        "top picks",
        "top products",
        "roundup",
        "product comparison",
        "vs",
        "versus",
        "head to head",
        "buying guide",
        "gift guide",
        "shopping guide",
        "review",
        "reviews",
        "deals",
        "discount",
        "discounts",
        "coupon",
        "coupons",
        "promo code",
        "giveaway",
        "gift card",
        "affiliate",
        "sponsored",
        "price",
        "prices",
        "pricing",
        "subscription",
        "subscribe and save"
    ];

    private static readonly Regex NonWordRegex = new(@"\W+", RegexOptions.Compiled);

    public Result Validate(string articleContent)
    {
        if (string.IsNullOrWhiteSpace(articleContent))
            return Result.Fail("Article content is empty");

        var normalizedText = Normalize(articleContent);

        if (ContainsAnyTerm(normalizedText, ViolenceTerms))
            return Result.Fail("Article content contains violence-related content");

        if (ContainsAnyTerm(normalizedText, CommercialContentTerms))
            return Result.Fail("Article content contains commercial/listicle content");

        if (ContainsAnyTerm(normalizedText, SexualTerms))
            return Result.Fail("Article content contains sexual content");

        if (ContainsAnyTerm(normalizedText, AbuseTerms))
            return Result.Fail("Article content contains abuse-related content");

        return Result.Ok();
    }

    private static bool ContainsAnyTerm(string normalizedText, IEnumerable<string> terms)
        => terms.Any(term => normalizedText.Contains(Normalize(term), StringComparison.Ordinal));

    private static string Normalize(string input)
    {
        var lowered = input.ToLowerInvariant();
        var normalized = NonWordRegex.Replace(lowered, " ");
        return string.Join(' ', normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }
}
