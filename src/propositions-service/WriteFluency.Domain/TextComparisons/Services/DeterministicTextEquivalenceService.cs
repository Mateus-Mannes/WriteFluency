using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;

namespace WriteFluency.TextComparisons;

public sealed partial class DeterministicTextEquivalenceService
{
    private static readonly IReadOnlyDictionary<string, string> PhraseMappings =
        BuildMappings(
            ["do not", "don't"],
            ["does not", "doesn't"],
            ["did not", "didn't"],
            ["is not", "isn't"],
            ["are not", "aren't"],
            ["was not", "wasn't"],
            ["were not", "weren't"],
            ["have not", "haven't"],
            ["has not", "hasn't"],
            ["had not", "hadn't"],
            ["cannot", "can't"],
            ["could not", "couldn't"],
            ["should not", "shouldn't"],
            ["would not", "wouldn't"],
            ["will not", "won't"],
            ["must not", "mustn't"],
            ["need not", "needn't"],
            ["I am", "I'm"],
            ["you are", "you're"],
            ["he is", "he's"],
            ["she is", "she's"],
            ["it is", "it's"],
            ["we are", "we're"],
            ["they are", "they're"],
            ["that is", "that's"],
            ["what is", "what's"],
            ["who is", "who's"],
            ["where is", "where's"],
            ["there is", "there's"],
            ["here is", "here's"],
            ["I have", "I've"],
            ["you have", "you've"],
            ["we have", "we've"],
            ["they have", "they've"],
            ["could have", "could've"],
            ["should have", "should've"],
            ["would have", "would've"],
            ["must have", "must've"],
            ["I will", "I'll"],
            ["you will", "you'll"],
            ["he will", "he'll"],
            ["she will", "she'll"],
            ["it will", "it'll"],
            ["we will", "we'll"],
            ["they will", "they'll"],
            ["I would", "I'd"],
            ["you would", "you'd"],
            ["he would", "he'd"],
            ["she would", "she'd"],
            ["we would", "we'd"],
            ["they would", "they'd"],
            ["want to", "wanna"],
            ["junior", "jr", "jr."],
            ["senior", "sr", "sr."],
            ["mister", "mr", "mr."],
            ["doctor", "dr", "dr."],
            ["versus", "vs", "vs."],
            ["United Kingdom", "UK", "U.K.", "U.K"],
            ["European Union", "EU", "E.U.", "E.U"],
            ["gigabyte", "gigabytes", "GB"],
            ["terabyte", "terabytes", "TB"],
            ["kilogram", "kilograms", "kg"],
            ["gram", "grams", "g"],
            ["milligram", "milligrams", "mg"],
            ["meter", "meters", "metre", "metres", "m"],
            ["kilometer", "kilometers", "kilometre", "kilometres", "km"],
            ["millisecond", "milliseconds", "ms"],
            ["kilometers per hour", "kilometres per hour", "km/h"],
            ["hantavirus", "hanta virus"],
            ["health care", "healthcare"],
            ["heat wave", "heatwave"],
            ["home town", "hometown"],
            ["photo journalist", "photojournalist"],
            ["left leaning", "left-leaning"],
            ["multi dimensional", "multi-dimensional", "multidimensional"],
            ["heart felt", "heart-felt", "heartfelt"],
            ["three point", "three-point"],
            ["well known", "well-known"],
            ["web site", "website"],
            ["web page", "webpage"],
            ["email", "e-mail"],
            ["online", "on-line"],
            ["offline", "off-line"],
            ["smart phone", "smartphone"],
            ["cell phone", "cellphone"],
            ["video game", "videogame"],
            ["data set", "dataset"],
            ["data base", "database", "data-base"],
            ["work place", "workplace"],
            ["work force", "workforce"],
            ["wood work", "woodwork"],
            ["house work", "housework"],
            ["yard work", "yardwork"],
            ["paper work", "paperwork"],
            ["school work", "schoolwork"],
            ["course work", "coursework"],
            ["class room", "classroom"],
            ["news room", "newsroom"],
            ["news paper", "newspaper"],
            ["book store", "bookstore"],
            ["life style", "lifestyle"],
            ["time line", "timeline"],
            ["head line", "headline"],
            ["week end", "weekend"],
            ["birth day", "birthday"],
            ["air port", "airport"],
            ["air line", "airline"],
            ["rail road", "railroad"],
            ["sea food", "seafood"],
            ["earth quake", "earthquake"],
            ["guest house", "guesthouse"],
            ["bed room", "bedroom"],
            ["bath room", "bathroom"],
            ["living room", "living-room"],
            ["dining room", "dining-room"],
            ["decision maker", "decision-maker"],
            ["policy maker", "policy-maker", "policymaker"],
            ["nonprofit", "non-profit"],
            ["worldwide", "world-wide"],
            ["so", "so,"]);

    private static readonly IReadOnlyDictionary<string, string> WordMappings =
        BuildMappings(
            ["traveling", "travelling"],
            ["traveled", "travelled"],
            ["traveler", "traveller"],
            ["canceling", "cancelling"],
            ["canceled", "cancelled"],
            ["modeling", "modelling"],
            ["modeled", "modelled"],
            ["labeling", "labelling"],
            ["labeled", "labelled"],
            ["counseling", "counselling"],
            ["counselor", "counsellor"],
            ["fueling", "fuelling"],
            ["fueled", "fuelled"],
            ["jewelry", "jewellery"],
            ["cozy", "cosy"],
            ["gray", "grey"],
            ["aging", "ageing"],
            ["neighborhood", "neighbourhood"],
            ["color", "colour"],
            ["colors", "colours"],
            ["colored", "coloured"],
            ["coloring", "colouring"],
            ["favorite", "favourite"],
            ["favorites", "favourites"],
            ["honor", "honour"],
            ["honors", "honours"],
            ["honored", "honoured"],
            ["labor", "labour"],
            ["behavior", "behaviour"],
            ["behaviors", "behaviours"],
            ["center", "centre"],
            ["centers", "centres"],
            ["centered", "centred"],
            ["theater", "theatre"],
            ["theaters", "theatres"],
            ["meter", "metre"],
            ["meters", "metres"],
            ["liter", "litre"],
            ["liters", "litres"],
            ["fiber", "fibre"],
            ["fibers", "fibres"],
            ["caliber", "calibre"],
            ["organize", "organise"],
            ["organizes", "organises"],
            ["organized", "organised"],
            ["organizing", "organising"],
            ["organization", "organisation"],
            ["organizations", "organisations"],
            ["recognize", "recognise"],
            ["recognizes", "recognises"],
            ["recognized", "recognised"],
            ["recognizing", "recognising"],
            ["analyze", "analyse"],
            ["analyzes", "analyses"],
            ["analyzed", "analysed"],
            ["analyzing", "analysing"],
            ["apologize", "apologise"],
            ["apologized", "apologised"],
            ["apologizing", "apologising"],
            ["defense", "defence"],
            ["defenses", "defences"],
            ["offense", "offence"],
            ["offenses", "offences"],
            ["catalog", "catalogue"],
            ["catalogs", "catalogues"],
            ["dialog", "dialogue"],
            ["dialogs", "dialogues"],
            ["fulfillment", "fulfilment"],
            ["enrollment", "enrolment"],
            ["airplane", "aeroplane"],
            ["airplanes", "aeroplanes"],
            ["artifact", "artefact"],
            ["artifacts", "artefacts"],
            ["ax", "axe"],
            ["omelet", "omelette"],
            ["omelets", "omelettes"],
            ["donut", "doughnut"],
            ["donuts", "doughnuts"],
            ["mustache", "moustache"],
            ["mustaches", "moustaches"],
            ["plow", "plough"],
            ["plows", "ploughs"],
            ["plowed", "ploughed"],
            ["plowing", "ploughing"],
            ["sulfur", "sulphur"],
            ["aluminum", "aluminium"],
            ["pajamas", "pyjamas"],
            ["skeptical", "sceptical"],
            ["maneuver", "manoeuvre"],
            ["maneuvers", "manoeuvres"],
            ["maneuvered", "manoeuvred"],
            ["pediatric", "paediatric"],
            ["estrogen", "oestrogen"]);

    private readonly EnglishNumberNormalizer _numberNormalizer;

    public DeterministicTextEquivalenceService(EnglishNumberNormalizer numberNormalizer)
    {
        _numberNormalizer = numberNormalizer;
    }

    public bool AreEquivalent(string? originalText, string? userText)
        => Evaluate(originalText, userText).IsEquivalent;

    public DeterministicEquivalenceResult Evaluate(string? originalText, string? userText)
    {
        if (originalText is null || userText is null)
        {
            return DeterministicEquivalenceResult.NotEquivalent;
        }

        if (string.Equals(originalText, userText, StringComparison.Ordinal))
        {
            return new DeterministicEquivalenceResult(true, "exact_duplicate");
        }

        var isEquivalent = string.Equals(
            Normalize(originalText),
            Normalize(userText),
            StringComparison.Ordinal);

        return isEquivalent
            ? new DeterministicEquivalenceResult(true, "normalized_equivalence")
            : DeterministicEquivalenceResult.NotEquivalent;
    }

    private string Normalize(string value)
    {
        var normalized = value
            .Normalize(NormalizationForm.FormKC)
            .ToLowerInvariant()
            .Replace('\u2018', '\'')
            .Replace('\u2019', '\'')
            .Replace('\u201c', '"')
            .Replace('\u201d', '"')
            .Replace('\u2010', '-')
            .Replace('\u2011', '-')
            .Replace('\u2012', '-')
            .Replace('\u2013', '-')
            .Replace('\u2014', '-')
            .Replace('\u2212', '-');

        normalized = RemoveDiacritics(normalized);
        normalized = TransliterateLatinLetters(normalized);
        normalized = DigitGroupingRegex().Replace(normalized, string.Empty);
        normalized = UsAbbreviationRegex().Replace(normalized, "us");
        normalized = ApplyPhraseMappings(normalized);
        normalized = NormalizeWhitespaceAndPunctuation(normalized);
        normalized = TrimDecorativeAndSentenceBoundaryPunctuation(normalized);
        normalized = DecadeRegex().Replace(normalized, "$1s");
        normalized = ApplyPhraseMappings(normalized);
        normalized = ApplyWordMappings(normalized);
        normalized = normalized.Replace('-', ' ');
        normalized = _numberNormalizer.Normalize(normalized);
        normalized = normalized
            .Replace("%", " percent", StringComparison.Ordinal)
            .Replace("&", " and ", StringComparison.Ordinal);
        normalized = NormalizeWhitespaceAndPunctuation(normalized);

        return TrimDecorativeAndSentenceBoundaryPunctuation(normalized);
    }

    private static string RemoveDiacritics(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character)
                != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string TransliterateLatinLetters(string value)
    {
        return value
            .Replace("æ", "ae", StringComparison.Ordinal)
            .Replace("œ", "oe", StringComparison.Ordinal)
            .Replace('ø', 'o')
            .Replace('ð', 'd')
            .Replace("þ", "th", StringComparison.Ordinal)
            .Replace('ł', 'l')
            .Replace('đ', 'd');
    }

    private static string NormalizeWhitespaceAndPunctuation(string value)
    {
        var normalized = WhitespaceRegex().Replace(value, " ").Trim();
        return PunctuationSpacingRegex().Replace(normalized, "$1 ");
    }

    private static string TrimDecorativeAndSentenceBoundaryPunctuation(string value)
    {
        var normalized = value.Trim();
        var changed = true;

        while (changed && normalized.Length > 0)
        {
            changed = false;

            if (normalized.Length >= 2
                && ((normalized[0] == '"' && normalized[^1] == '"')
                    || (normalized[0] == '\'' && normalized[^1] == '\'')))
            {
                normalized = normalized[1..^1].Trim();
                changed = true;
            }

            var trimmed = BoundaryPunctuationRegex().Replace(normalized, string.Empty).Trim();
            if (!string.Equals(trimmed, normalized, StringComparison.Ordinal))
            {
                normalized = trimmed;
                changed = true;
            }
        }

        return normalized;
    }

    private static string ApplyPhraseMappings(string value)
    {
        var normalized = value;
        foreach (var mapping in PhraseMappings.OrderByDescending(mapping => mapping.Key.Length))
        {
            normalized = Regex.Replace(
                normalized,
                $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(mapping.Key)}(?![\p{{L}}\p{{N}}])",
                mapping.Value,
                RegexOptions.CultureInvariant);
        }

        return normalized;
    }

    private static string ApplyWordMappings(string value)
    {
        return WordRegex().Replace(
            value,
            match => WordMappings.TryGetValue(match.Value, out var replacement)
                ? replacement
                : match.Value);
    }

    private static IReadOnlyDictionary<string, string> BuildMappings(
        params string[][] equivalenceGroups)
    {
        var mappings = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var group in equivalenceGroups)
        {
            var canonical = group[0].ToLowerInvariant();
            foreach (var variant in group)
            {
                mappings.Add(variant.ToLowerInvariant(), canonical);
            }
        }

        return mappings;
    }

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"\s*([,.;:!?])\s*", RegexOptions.CultureInvariant)]
    private static partial Regex PunctuationSpacingRegex();

    [GeneratedRegex(@"^[.,!?;:]+|[.,!?;:]+$", RegexOptions.CultureInvariant)]
    private static partial Regex BoundaryPunctuationRegex();

    [GeneratedRegex(@"(?<=\d),(?=\d{3}\b)", RegexOptions.CultureInvariant)]
    private static partial Regex DigitGroupingRegex();

    [GeneratedRegex(@"\b(\d{3,4})['’]s\b", RegexOptions.CultureInvariant)]
    private static partial Regex DecadeRegex();

    [GeneratedRegex(@"\bu\.s\.?(?=\W|$)", RegexOptions.CultureInvariant)]
    private static partial Regex UsAbbreviationRegex();

    [GeneratedRegex(@"\p{L}+(?:'\p{L}+|')?", RegexOptions.CultureInvariant)]
    private static partial Regex WordRegex();
}

public sealed record DeterministicEquivalenceResult(
    bool IsEquivalent,
    string? ReasonCode)
{
    public static DeterministicEquivalenceResult NotEquivalent { get; } =
        new(false, null);
}
