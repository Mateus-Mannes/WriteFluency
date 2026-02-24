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
        "behead",
        "torture",
        "gore",
        "dismember",
        "abduction",
    ];

    private static readonly string[] CommercialContentTerms =
    [
        "top picks",
        "top products",
        "buying guide",
        "gift guide",
        "shopping guide",
        "discount",
        "discounts",
        "coupon",
        "coupons",
        "promo code",
        "giveaway",
        "gift card",
        "affiliate",
        "sale"
    ];

    private static readonly Regex NonWordRegex = new(@"\W+", RegexOptions.Compiled);

    public Result Validate(string articleContent)
    {
        if (string.IsNullOrWhiteSpace(articleContent))
            return Result.Fail("Article content is empty");

        var normalizedText = Normalize(articleContent);
        var validationErrors = new List<Error>();

        AddViolationIfAny(validationErrors, normalizedText, ViolenceTerms, nameof(ViolenceTerms));

        AddViolationIfAny(validationErrors, normalizedText, CommercialContentTerms, nameof(CommercialContentTerms));

        AddViolationIfAny(validationErrors, normalizedText, SexualTerms, nameof(SexualTerms));

        AddViolationIfAny(validationErrors, normalizedText, AbuseTerms, nameof(AbuseTerms));

        if (validationErrors.Count > 0)
            return Result.Fail(validationErrors);

        return Result.Ok();
    }

    private static void AddViolationIfAny(
        ICollection<Error> validationErrors,
        string normalizedText,
        IEnumerable<string> terms,
        string messagePrefix)
    {
        var matchedTerms = FindMatchedTerms(normalizedText, terms);
        if (matchedTerms.Count == 0)
            return;

        var identifiedTerms = string.Join(", ", matchedTerms.Select(term => $"'{term}'"));
        validationErrors.Add(new Error($"{messagePrefix}. Invalid terms identified: {identifiedTerms}"));
    }

    private static IReadOnlyList<string> FindMatchedTerms(string normalizedText, IEnumerable<string> terms)
        => terms
            .Where(term => normalizedText.Contains(Normalize(term), StringComparison.Ordinal))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static string Normalize(string input)
    {
        var lowered = input.ToLowerInvariant();
        var normalized = NonWordRegex.Replace(lowered, " ");
        return string.Join(' ', normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }
}
