using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace WriteFluency.TextComparisons;

public sealed partial class EnglishNumberNormalizer
{
    private static readonly IReadOnlyDictionary<string, long> SmallNumbers =
        new Dictionary<string, long>(StringComparer.Ordinal)
        {
            ["zero"] = 0,
            ["one"] = 1,
            ["two"] = 2,
            ["three"] = 3,
            ["four"] = 4,
            ["five"] = 5,
            ["six"] = 6,
            ["seven"] = 7,
            ["eight"] = 8,
            ["nine"] = 9,
            ["ten"] = 10,
            ["eleven"] = 11,
            ["twelve"] = 12,
            ["thirteen"] = 13,
            ["fourteen"] = 14,
            ["fifteen"] = 15,
            ["sixteen"] = 16,
            ["seventeen"] = 17,
            ["eighteen"] = 18,
            ["nineteen"] = 19,
            ["twenty"] = 20,
            ["thirty"] = 30,
            ["forty"] = 40,
            ["fifty"] = 50,
            ["sixty"] = 60,
            ["seventy"] = 70,
            ["eighty"] = 80,
            ["ninety"] = 90,
            ["first"] = 1,
            ["second"] = 2,
            ["third"] = 3,
            ["fourth"] = 4,
            ["fifth"] = 5,
            ["sixth"] = 6,
            ["seventh"] = 7,
            ["eighth"] = 8,
            ["ninth"] = 9,
            ["tenth"] = 10,
            ["eleventh"] = 11,
            ["twelfth"] = 12,
            ["thirteenth"] = 13,
            ["fourteenth"] = 14,
            ["fifteenth"] = 15,
            ["sixteenth"] = 16,
            ["seventeenth"] = 17,
            ["eighteenth"] = 18,
            ["nineteenth"] = 19,
            ["twentieth"] = 20,
            ["thirtieth"] = 30,
            ["fortieth"] = 40,
            ["fiftieth"] = 50,
            ["sixtieth"] = 60,
            ["seventieth"] = 70,
            ["eightieth"] = 80,
            ["ninetieth"] = 90
        };

    private static readonly IReadOnlyDictionary<string, long> Scales =
        new Dictionary<string, long>(StringComparer.Ordinal)
        {
            ["hundred"] = 100,
            ["thousand"] = 1_000,
            ["million"] = 1_000_000,
            ["billion"] = 1_000_000_000
        };

    public string Normalize(string value)
    {
        var matches = NumberTokenRegex().Matches(value);
        if (matches.Count == 0)
        {
            return value;
        }

        var result = new StringBuilder();
        var currentIndex = 0;
        var matchIndex = 0;

        while (matchIndex < matches.Count)
        {
            var match = matches[matchIndex];
            result.Append(value, currentIndex, match.Index - currentIndex);

            if (TryParseDigitToken(match.Value, out var digitValue))
            {
                result.Append(digitValue.ToString(CultureInfo.InvariantCulture));
                currentIndex = match.Index + match.Length;
                matchIndex++;
                continue;
            }

            var numberWords = new List<string>();
            var sequenceEnd = match.Index;
            var sequenceMatchIndex = matchIndex;

            while (sequenceMatchIndex < matches.Count)
            {
                var candidate = matches[sequenceMatchIndex];
                var separator = value[sequenceEnd..candidate.Index];
                if (numberWords.Count > 0 && !NumberSeparatorRegex().IsMatch(separator))
                {
                    break;
                }

                if (!IsNumberWord(candidate.Value))
                {
                    break;
                }

                numberWords.Add(candidate.Value);
                sequenceEnd = candidate.Index + candidate.Length;
                sequenceMatchIndex++;
            }

            if (numberWords.Count > 0 && TryParseNumberWords(numberWords, out var parsedValue))
            {
                result.Append(parsedValue.ToString(CultureInfo.InvariantCulture));
                currentIndex = sequenceEnd;
                matchIndex = sequenceMatchIndex;
                continue;
            }

            result.Append(match.Value);
            currentIndex = match.Index + match.Length;
            matchIndex++;
        }

        result.Append(value, currentIndex, value.Length - currentIndex);
        return result.ToString();
    }

    private static bool TryParseDigitToken(string token, out long value)
    {
        var normalized = OrdinalSuffixRegex().Replace(token, string.Empty);
        return long.TryParse(
            normalized,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out value);
    }

    private static bool IsNumberWord(string word)
    {
        return SmallNumbers.ContainsKey(word)
            || Scales.ContainsKey(word)
            || string.Equals(word, "and", StringComparison.Ordinal);
    }

    private static bool TryParseNumberWords(IReadOnlyList<string> words, out long value)
    {
        value = 0;
        if (words.Count == 0 || words.All(word => word == "and"))
        {
            return false;
        }

        if (TryParseSpokenYear(words, out value))
        {
            return true;
        }

        for (var index = 0; index < words.Count; index++)
        {
            if (words[index] != "and")
            {
                continue;
            }

            if (index == 0
                || index == words.Count - 1
                || !Scales.ContainsKey(words[index - 1]))
            {
                return false;
            }
        }

        long total = 0;
        long current = 0;
        var sawNumber = false;
        long? previousSmallNumber = null;
        var lastLargeScale = long.MaxValue;

        foreach (var word in words)
        {
            if (word == "and")
            {
                previousSmallNumber = null;
                continue;
            }

            if (SmallNumbers.TryGetValue(word, out var small))
            {
                if (previousSmallNumber.HasValue
                    && !IsValidSmallNumberContinuation(previousSmallNumber.Value, small))
                {
                    return false;
                }

                current += small;
                sawNumber = true;
                previousSmallNumber = small;
                continue;
            }

            if (!Scales.TryGetValue(word, out var scale))
            {
                return false;
            }

            sawNumber = true;
            if (scale == 100)
            {
                if (current is < 1 or > 9)
                {
                    return false;
                }

                current *= scale;
            }
            else
            {
                if (current == 0 || scale >= lastLargeScale)
                {
                    return false;
                }

                total += current * scale;
                current = 0;
                lastLargeScale = scale;
            }

            previousSmallNumber = null;
        }

        value = total + current;
        return sawNumber;
    }

    private static bool IsValidSmallNumberContinuation(long previous, long current)
    {
        return previous is >= 20 and <= 90
            && previous % 10 == 0
            && current is >= 1 and <= 9;
    }

    private static bool TryParseSpokenYear(IReadOnlyList<string> words, out long value)
    {
        value = 0;
        if (words.Count < 2 || words.Count > 3)
        {
            return false;
        }

        if (!TryParseSmallNumber(words.Take(1), out var century)
            || century is < 10 or > 99
            || !TryParseSmallNumber(words.Skip(1), out var yearPart)
            || yearPart is < 10 or > 99)
        {
            return false;
        }

        value = (century * 100) + yearPart;
        return true;
    }

    private static bool TryParseSmallNumber(IEnumerable<string> words, out long value)
    {
        value = 0;
        foreach (var word in words)
        {
            if (!SmallNumbers.TryGetValue(word, out var small))
            {
                return false;
            }

            value += small;
        }

        return true;
    }

    [GeneratedRegex(@"\b(?:\d+(?:st|nd|rd|th)?|[\p{L}]+)\b", RegexOptions.CultureInvariant)]
    private static partial Regex NumberTokenRegex();

    [GeneratedRegex(@"^[\s-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex NumberSeparatorRegex();

    [GeneratedRegex(@"(?<=\d)(?:st|nd|rd|th)$", RegexOptions.CultureInvariant)]
    private static partial Regex OrdinalSuffixRegex();
}
