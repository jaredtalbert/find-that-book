using System.Globalization;
using FindThatBook.Server.Matching;
using FindThatBook.Server.Models;

namespace FindThatBook.Server.Services;

internal static class BookSearchResultFactory {
    private const string OpenLibraryWorkBaseUrl = "https://openlibrary.org/works/";
    private const string OpenLibraryCoverBaseUrl = "https://covers.openlibrary.org/b/id/";

    internal static BookSearchCandidate Create(QueryIntent intent, RankedCandidate rankedCandidate) {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(rankedCandidate);

        CandidateRankingInput candidate = rankedCandidate.Candidate;
        Doc document = candidate.SearchDocument;
        string? key = NonBlank(document.Key);

        return new BookSearchCandidate {
            OpenLibraryKey = key,
            Title = NonBlank(candidate.RankingMetadata.CanonicalTitle) ?? document.Title,
            Authors = SelectAuthors(candidate),
            FirstPublishYear = document.FirstPublishYear,
            OpenLibraryUrl = key is null
                ? null
                : OpenLibraryWorkBaseUrl + Uri.EscapeDataString(key),
            CoverImageUrl = document.CoverId is > 0
                ? OpenLibraryCoverBaseUrl +
                  document.CoverId.Value.ToString(CultureInfo.InvariantCulture) +
                  "-M.jpg"
                : null,
            Confidence = DetermineConfidence(rankedCandidate.Evidence),
            Explanation = Explain(intent, rankedCandidate.Evidence)
        };
    }

    private static IReadOnlyList<string> SelectAuthors(CandidateRankingInput candidate) {
        IReadOnlyList<string> authors = candidate.RankingMetadata.HasCanonicalAuthorData
            ? candidate.RankingMetadata.CanonicalAuthors
            : candidate.SearchDocument.AuthorName;

        return authors
            .Select(NonBlank)
            .Where(author => author is not null)
            .Cast<string>()
            .DistinctBy(author => TextNormalizer.NormalizeAuthor(author).Loose, StringComparer.Ordinal)
            .ToArray();
    }

    private static SearchConfidence DetermineConfidence(MatchEvidence evidence) {
        bool exactTitle = evidence.TitleKind is TitleMatchKind.ExactFullTitle or TitleMatchKind.ExactMainTitle;

        bool strongPrimaryAuthor = evidence.IsCanonicalPrimaryAuthorMatch &&
                                   (evidence.AuthorKind is AuthorMatchKind.ExactFullName or
                                       AuthorMatchKind.InitialsAndSurname);

        if (exactTitle || strongPrimaryAuthor ||
            evidence.HasMeaningfulTitleMatch && evidence.HasMeaningfulAuthorMatch) {
            return SearchConfidence.Strong;
        }

        if (evidence.HasMeaningfulTitleMatch || evidence.HasMeaningfulAuthorMatch ||
            evidence.MatchedKeywords.Count >= RankingThresholds.KeywordOnlyMatchCount) {
            return SearchConfidence.Likely;
        }

        return SearchConfidence.Possible;
    }

    private static string Explain(QueryIntent intent, MatchEvidence evidence) {
        List<string> reasons = [];

        string? titleReason = ExplainTitle(evidence.TitleKind);

        if (titleReason is not null) {
            reasons.Add(titleReason);
        }

        string? authorReason = ExplainAuthor(intent.Author?.Value, evidence);

        if (authorReason is not null) {
            reasons.Add(authorReason);
        }

        if (evidence.MatchedKeywords.Count > 0) {
            reasons.Add($"Matches remembered details: {string.Join(", ", evidence.MatchedKeywords)}");
        }

        string? yearReason = ExplainYear(intent.Year?.Value, evidence.YearKind);

        if (yearReason is not null) {
            reasons.Add(yearReason);
        }

        return reasons.Count == 0
            ? "Plausible match based on the available catalog evidence."
            : string.Join("; ", reasons) + ".";
    }

    private static string? ExplainTitle(TitleMatchKind kind) => kind switch {
        TitleMatchKind.ExactFullTitle => "Exact title match",
        TitleMatchKind.ExactMainTitle => "Exact main-title match",
        TitleMatchKind.ContainsCompleteQuery => "Strong title match",
        TitleMatchKind.SignificantTokenOverlap => "Strong title-word overlap",
        TitleMatchKind.FuzzyTokens => "Close title-word match",
        _ => null
    };

    private static string? ExplainAuthor(string? requestedAuthor, MatchEvidence evidence) {
        string? author = NonBlank(requestedAuthor);

        if (author is null || evidence.AuthorKind is AuthorMatchKind.None or AuthorMatchKind.CanonicalConflict) {
            return null;
        }

        string role = evidence.IsCanonicalPrimaryAuthorMatch
            ? "primary-author"
            : evidence.AuthorSource == AuthorEvidenceSource.CanonicalWork
                ? "canonical-author"
                : "author";

        return evidence.AuthorKind switch {
            AuthorMatchKind.ExactFullName => $"Exact {role} match for {author}",
            AuthorMatchKind.InitialsAndSurname => $"{Capitalize(role)} initials and surname match for {author}",
            AuthorMatchKind.ExactSurname => $"{Capitalize(role)} surname match for {author}",
            AuthorMatchKind.MultiToken => $"Strong {role} name match for {author}",
            AuthorMatchKind.FuzzySurname => $"Close {role} surname match for {author}",
            _ => null
        };
    }

    private static string? ExplainYear(long? requestedYear, YearMatchKind kind) {
        if (requestedYear is null || kind is YearMatchKind.None or YearMatchKind.OutsideTolerance) {
            return null;
        }

        return kind == YearMatchKind.Exact
            ? $"First publication year matches {requestedYear.Value}"
            : $"First publication year is close to {requestedYear.Value}";
    }

    private static string Capitalize(string value) =>
        char.ToUpperInvariant(value[0]) + value[1..];

    private static string? NonBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}