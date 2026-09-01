using System.Globalization;
using System.Text;

namespace FindThatBook.Server.Matching;

public static class TextNormalizer {
    private static readonly char[] SubtitleDashes = ['\u2013', '\u2014'];

    /// <summary>
    /// Produces a strict comparison form that retains diacritics and a loose form
    /// that removes them. Both forms are lowercase, punctuation-free, and have
    /// collapsed whitespace.
    /// </summary>
    public static NormalizedText Normalize(string? value) {
        string original = value ?? string.Empty;
        string strict = NormalizeCore(original, removeDiacritics: false);
        string loose = NormalizeCore(original, removeDiacritics: true);

        return CreateResult(original, strict, loose);
    }

    /// <summary>
    /// Normalizes an author name and joins consecutive leading initials, making
    /// names such as "J. R. R. Tolkien" and "JRR Tolkien" comparable.
    /// </summary>
    public static NormalizedText NormalizeAuthor(string? value) {
        NormalizedText normalized = Normalize(value);
        string loose = CollapseLeadingInitials(normalized.LooseTokens);

        return CreateResult(normalized.Original, normalized.Strict, loose);
    }

    /// <summary>
    /// Compares two author names using their loose author forms. Empty author
    /// names are never considered equivalent.
    /// </summary>
    public static bool AreEquivalentAuthors(string? left, string? right) {
        string leftKey = NormalizeAuthor(left).Loose;
        string rightKey = NormalizeAuthor(right).Loose;

        return leftKey.Length > 0 &&
               rightKey.Length > 0 &&
               string.Equals(leftKey, rightKey, StringComparison.Ordinal);
    }

    /// <summary>
    /// Preserves a full normalized title while also exposing its main title and
    /// subtitle. Colons, en dashes, em dashes, and spaced hyphens are recognized
    /// as subtitle separators.
    /// </summary>
    public static NormalizedTitle NormalizeTitle(string? value) {
        string original = value ?? string.Empty;
        (int index, int length) = FindSubtitleSeparator(original);

        if (index < 0) {
            NormalizedText title = Normalize(original);

            return new NormalizedTitle(title, title, null);
        }

        NormalizedText full = Normalize(original);
        NormalizedText mainTitle = Normalize(original[..index]);
        NormalizedText subtitle = Normalize(original[(index + length)..]);

        return new NormalizedTitle(full, mainTitle, subtitle);
    }

    private static NormalizedText CreateResult(string original, string strict, string loose) =>
        new(original, strict, loose, Tokenize(strict), Tokenize(loose));

    private static string NormalizeCore(string value, bool removeDiacritics) {
        if (string.IsNullOrWhiteSpace(value)) {
            return string.Empty;
        }

        string decomposed = value.Normalize(NormalizationForm.FormD);
        StringBuilder builder = new(decomposed.Length);
        bool lastCharacterWasSpace = true;

        foreach (char character in decomposed) {
            UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(character);

            if (IsCombiningMark(category)) {
                if (!removeDiacritics) {
                    builder.Append(character);
                }

                continue;
            }

            if (IsApostrophe(character)) {
                continue;
            }

            if (char.IsWhiteSpace(character) || IsPunctuationOrSymbol(category)) {
                if (!lastCharacterWasSpace) {
                    builder.Append(' ');
                    lastCharacterWasSpace = true;
                }

                continue;
            }

            builder.Append(char.ToLowerInvariant(character));
            lastCharacterWasSpace = false;
        }

        return builder.ToString().Trim().Normalize(NormalizationForm.FormC);
    }

    private static string CollapseLeadingInitials(IReadOnlyList<string> tokens) {
        int initialCount = 0;

        while (initialCount < tokens.Count && tokens[initialCount].Length == 1) {
            initialCount++;
        }

        if (initialCount < 2) {
            return string.Join(' ', tokens);
        }

        string initials = string.Concat(tokens.Take(initialCount));

        return initialCount == tokens.Count
            ? initials
            : $"{initials} {string.Join(' ', tokens.Skip(initialCount))}";
    }

    private static (int Index, int Length) FindSubtitleSeparator(string value) {
        int colonIndex = value.IndexOf(':');
        int dashIndex = value.IndexOfAny(SubtitleDashes);
        int spacedHyphenIndex = value.IndexOf(" - ", StringComparison.Ordinal);

        (int Index, int Length)[] candidates = [
            (colonIndex, 1),
            (dashIndex, 1),
            (spacedHyphenIndex, 3)
        ];

        return candidates
            .Where(candidate => candidate.Index >= 0)
            .OrderBy(candidate => candidate.Index)
            .FirstOrDefault((-1, 0));
    }

    private static string[] Tokenize(string value) =>
        value.Length == 0
            ? []
            : value.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    private static bool IsApostrophe(char character) => character is '\'' or '\u2019';

    private static bool IsCombiningMark(UnicodeCategory category) => category is
        UnicodeCategory.NonSpacingMark or
        UnicodeCategory.SpacingCombiningMark or
        UnicodeCategory.EnclosingMark;

    private static bool IsPunctuationOrSymbol(UnicodeCategory category) => category is
        UnicodeCategory.ConnectorPunctuation or
        UnicodeCategory.DashPunctuation or
        UnicodeCategory.OpenPunctuation or
        UnicodeCategory.ClosePunctuation or
        UnicodeCategory.InitialQuotePunctuation or
        UnicodeCategory.FinalQuotePunctuation or
        UnicodeCategory.OtherPunctuation or
        UnicodeCategory.MathSymbol or
        UnicodeCategory.CurrencySymbol or
        UnicodeCategory.ModifierSymbol or
        UnicodeCategory.OtherSymbol;
}