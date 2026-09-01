using FindThatBook.Server.Matching;
using FindThatBook.Server.Models;
using Xunit;

namespace FindThatBook.Server.Tests.Matching;

public class CandidateRankerTests {
    private readonly CandidateRanker _ranker = new();

    [Fact]
    public void Rank_ExactTitleReceivesMaximumTitleScore() {
        QueryIntent intent = Intent(title: "The Song of Achilles");

        RankedCandidate result = Single(intent, Candidate("work-1", "the song of achilles"));

        Assert.Equal(TitleMatchKind.ExactFullTitle, result.Evidence.TitleKind);
        Assert.Equal(RankingScores.TitleExactFull, result.Evidence.TitleScore);
        Assert.True(result.IsUseful);
    }

    [Fact]
    public void Rank_MatchingMainTitleDoesNotRequireMatchingSubtitle() {
        QueryIntent intent = Intent(title: "The Hobbit");

        RankedCandidate result = Single(intent,
            Candidate("work-1", "The Hobbit: There and Back Again"));

        Assert.Equal(TitleMatchKind.ExactMainTitle, result.Evidence.TitleKind);
        Assert.Equal(RankingScores.TitleExactMain, result.Evidence.TitleScore);
        Assert.True(result.IsUseful);
    }

    [Fact]
    public void Rank_AdjacentTranspositionProducesAWeakerFuzzyTitleMatch() {
        QueryIntent intent = Intent(title: "Achilels");

        RankedCandidate result = Single(intent, Candidate("work-1", "Achilles"));

        Assert.Equal(TitleMatchKind.FuzzyTokens, result.Evidence.TitleKind);

        Assert.InRange(result.Evidence.TitleScore,
            RankingScores.TitleFuzzyTokenMinimum,
            RankingScores.TitleFuzzyTokenMaximum);

        Assert.True(result.IsUseful);
    }

    [Theory]
    [InlineData("jk rowling")]
    [InlineData("JK rowling")]
    [InlineData("JK. Rowling")]
    [InlineData("J. K. Rowling")]
    public void Rank_NormalizedAuthorVariantsMatchCanonicalSpelling(string query) {
        QueryIntent intent = Intent(author: query);

        RankedCandidate result = Single(intent,
            Candidate("work-1", "Harry Potter", authors: ["J.K. Rowling"]));

        Assert.Equal(AuthorMatchKind.ExactFullName, result.Evidence.AuthorKind);
        Assert.Equal(RankingScores.AuthorExactFull, result.Evidence.AuthorScore);
        Assert.True(result.IsUseful);
    }

    [Fact]
    public void Rank_ExpandedGivenNamesMatchInitialsAndSurname() {
        QueryIntent intent = Intent(author: "JRR Tolkien");

        RankedCandidate result = Single(intent,
            Candidate("work-1", "The Hobbit", authors: ["John Ronald Reuel Tolkien"]));

        Assert.Equal(AuthorMatchKind.InitialsAndSurname, result.Evidence.AuthorKind);
        Assert.Equal(RankingScores.AuthorInitialsAndSurname, result.Evidence.AuthorScore);
        Assert.True(result.IsUseful);
    }

    [Fact]
    public void Rank_CommonSurnameAloneRequiresCorroboration() {
        QueryIntent intent = Intent(author: "Smith");

        RankedCandidate result = Single(intent,
            Candidate("work-1", "A Book", authors: ["John Smith"]));

        Assert.Equal(AuthorMatchKind.ExactSurname, result.Evidence.AuthorKind);
        Assert.False(result.Evidence.HasMeaningfulAuthorMatch);
        Assert.False(result.IsUseful);
    }

    [Fact]
    public void Rank_DistinctiveSurnameAloneIsMeaningful() {
        QueryIntent intent = Intent(author: "Dickens");

        RankedCandidate result = Single(intent,
            Candidate("work-1", "Great Expectations", authors: ["Charles Dickens"]));

        Assert.Equal(AuthorMatchKind.ExactSurname, result.Evidence.AuthorKind);
        Assert.True(result.Evidence.HasMeaningfulAuthorMatch);
        Assert.True(result.IsUseful);
    }

    [Fact]
    public void Rank_CanonicalAuthorConflictRejectsAnExactTitle() {
        QueryIntent intent = Intent(title: "Dune", author: "Ursula Le Guin");

        CandidateRankingMetadata metadata = new(
            CanonicalAuthorNames: ["Frank Herbert"],
            HasCanonicalAuthorData: true);

        RankedCandidate result = Single(intent, Candidate("work-1", "Dune", metadata: metadata));

        Assert.Equal(AuthorMatchKind.CanonicalConflict, result.Evidence.AuthorKind);
        Assert.Equal(RankingScores.AuthorCanonicalConflict, result.Evidence.AuthorScore);
        Assert.True(result.Evidence.HasCanonicalAuthorConflict);
        Assert.False(result.IsUseful);
    }

    [Fact]
    public void Rank_SharedSurnameWithDifferentGivenNameIsACanonicalConflict() {
        QueryIntent intent = Intent(title: "A Book", author: "John Atwood");

        CandidateRankingMetadata metadata = new(
            CanonicalAuthorNames: ["Margaret Atwood"],
            HasCanonicalAuthorData: true);

        RankedCandidate result = Single(intent, Candidate("work-1", "A Book", metadata: metadata));

        Assert.Equal(AuthorMatchKind.CanonicalConflict, result.Evidence.AuthorKind);
        Assert.False(result.IsUseful);
    }

    [Fact]
    public void Rank_MissingAuthorMetadataIsNeutralBeforeEnrichment() {
        QueryIntent intent = Intent(title: "Dune", author: "Frank Herbert");

        RankedCandidate result = Single(intent, Candidate("work-1", "Dune"));

        Assert.Equal(0, result.Evidence.AuthorScore);
        Assert.False(result.Evidence.HasCanonicalAuthorConflict);
        Assert.True(result.IsUseful);
    }

    [Fact]
    public void Rank_SearchResponseAuthorMismatchIsNeutralBeforeCanonicalEnrichment() {
        QueryIntent intent = Intent(title: "Dune", author: "Ursula Le Guin");

        RankedCandidate result = Single(intent,
            Candidate("work-1", "Dune", authors: ["Frank Herbert"]));

        Assert.Equal(0, result.Evidence.AuthorScore);
        Assert.False(result.Evidence.HasCanonicalAuthorConflict);
        Assert.True(result.IsUseful);
    }

    [Fact]
    public void Rank_MissingKeywordsDoNotReduceAnOtherwiseUsefulMatch() {
        QueryIntent withoutKeywords = Intent(title: "Dune");
        QueryIntent withUnmatchedKeywords = Intent(title: "Dune", keywords: ["desert", "politics"]);
        CandidateRankingInput candidate = Candidate("work-1", "Dune");

        RankedCandidate baseline = Single(withoutKeywords, candidate);
        RankedCandidate withKeywords = Single(withUnmatchedKeywords, candidate);

        Assert.Equal(0, withKeywords.Evidence.KeywordScore);
        Assert.Equal(baseline.Score, withKeywords.Score);
        Assert.True(withKeywords.IsUseful);
    }

    [Fact]
    public void Rank_KeywordOnlyQueryRequiresMultipleMatchesOrOneDistinctiveMatch() {
        RankedCandidate twoMatches = Single(
            Intent(keywords: ["magic", "school"]),
            Candidate("work-1", "A School for Magic"));

        RankedCandidate shortSingleMatch = Single(
            Intent(keywords: ["magic"]),
            Candidate("work-2", "A Kind of Magic"));

        RankedCandidate distinctiveSingleMatch = Single(
            Intent(keywords: ["mythology"]),
            Candidate("work-3", "A Study of Mythology"));

        Assert.True(twoMatches.IsUseful);
        Assert.False(shortSingleMatch.IsUseful);
        Assert.True(distinctiveSingleMatch.IsUseful);
    }

    [Fact]
    public void Rank_UsesSubjectsAsSupportingKeywordEvidence() {
        QueryIntent intent = Intent(keywords: ["space", "politics"]);
        CandidateRankingMetadata metadata = new(Subjects: ["Space exploration", "Politics in fiction"]);

        RankedCandidate result = Single(intent,
            Candidate("work-1", "The Dispossessed", metadata: metadata));

        Assert.Equal(["space", "politics"], result.Evidence.MatchedKeywords);
        Assert.True(result.IsUseful);
    }

    [Fact]
    public void Rank_UsesTotalEvidenceBeforeLateTieBreakers() {
        QueryIntent intent = Intent(keywords: ["magic", "school", "mythology"]);
        CandidateRankingInput stronger = Candidate("z-work", "Magic School");

        CandidateRankingInput weakerWithMoreEditions = Candidate(
            "a-work",
            "Mythology",
            metadata: new CandidateRankingMetadata(EditionCount: 100));

        IReadOnlyList<RankedCandidate> results = _ranker.Rank(intent, [weakerWithMoreEditions, stronger]);

        Assert.Equal("z-work", results[0].Candidate.SearchDocument.Key);
        Assert.True(results[0].Score > results[1].Score);
    }

    [Fact]
    public void Rank_CompleteQueryContainmentDoesNotDiscardStopWords() {
        QueryIntent intent = Intent(title: "The Song of Achilles");

        RankedCandidate result = Single(intent, Candidate("work-1", "Song Achilles"));

        Assert.Equal(TitleMatchKind.SignificantTokenOverlap, result.Evidence.TitleKind);
    }

    [Fact]
    public void Rank_OneGenericTokenDoesNotMakeAHalfMatchingTitleUseful() {
        QueryIntent intent = Intent(title: "The Song of Achilles");

        RankedCandidate result = Single(intent, Candidate("work-1", "A Song of Ice and Fire"));

        Assert.Equal(TitleMatchKind.SignificantTokenOverlap, result.Evidence.TitleKind);
        Assert.False(result.Evidence.HasMeaningfulTitleMatch);
        Assert.False(result.IsUseful);
    }

    [Fact]
    public void Rank_InferredEvidenceIsDiscountedAndRequiresCombinedSupport() {
        QueryIntent titleOnly = Intent(title: "Dune", provenance: QueryFieldProvenance.Inferred);

        QueryIntent combined = Intent(
            title: "Dune",
            author: "Frank Herbert",
            provenance: QueryFieldProvenance.Inferred);

        CandidateRankingInput candidate = Candidate("work-1", "Dune", authors: ["Frank Herbert"]);

        RankedCandidate titleResult = Single(titleOnly, candidate);
        RankedCandidate combinedResult = Single(combined, candidate);

        Assert.Equal(42, titleResult.Evidence.TitleScore);
        Assert.False(titleResult.IsUseful);
        Assert.Equal(25, combinedResult.Evidence.AuthorScore);
        Assert.True(combinedResult.IsUseful);
    }

    [Fact]
    public void Rank_YearIsSmallSupportingEvidence() {
        QueryIntent intent = Intent(title: "Dune", year: 1965);

        RankedCandidate exact = Single(intent, Candidate("exact", "Dune", year: 1965));
        RankedCandidate near = Single(intent, Candidate("near", "Dune", year: 1966));
        RankedCandidate far = Single(intent, Candidate("far", "Dune", year: 2000));

        Assert.Equal(RankingScores.YearExact, exact.Evidence.YearScore);
        Assert.Equal(RankingScores.YearWithinOne, near.Evidence.YearScore);
        Assert.Equal(0, far.Evidence.YearScore);
        Assert.True(far.IsUseful);
    }

    [Fact]
    public void Rank_PrefersCanonicalPrimaryAuthorBeforeOtherTieBreakers() {
        QueryIntent intent = Intent(title: "Shared Title", author: "Second Author");

        CandidateRankingInput secondary = Candidate(
            "a-work",
            "Shared Title",
            metadata: new CandidateRankingMetadata(
                EditionCount: 100,
                CanonicalAuthorNames: ["First Author", "Second Author"],
                HasCanonicalAuthorData: true));

        CandidateRankingInput primary = Candidate(
            "z-work",
            "Shared Title",
            metadata: new CandidateRankingMetadata(
                EditionCount: 1,
                CanonicalAuthorNames: ["Second Author"],
                HasCanonicalAuthorData: true));

        IReadOnlyList<RankedCandidate> results = _ranker.Rank(intent, [secondary, primary]);

        Assert.Equal("z-work", results[0].Candidate.SearchDocument.Key);
        Assert.True(results[0].Evidence.IsCanonicalPrimaryAuthorMatch);
    }

    [Fact]
    public void Rank_UsesEditionCountThenWorkKeyAsStableLateTieBreakers() {
        QueryIntent intent = Intent(title: "Dune");

        CandidateRankingInput lowerEdition =
            Candidate("a-work", "Dune", metadata: new CandidateRankingMetadata(EditionCount: 2));

        CandidateRankingInput higherEdition =
            Candidate("z-work", "Dune", metadata: new CandidateRankingMetadata(EditionCount: 10));

        IReadOnlyList<RankedCandidate> byEdition = _ranker.Rank(intent, [lowerEdition, higherEdition]);

        IReadOnlyList<RankedCandidate> byKey = _ranker.Rank(intent,
            [Candidate("z-work", "Dune"), Candidate("a-work", "Dune")]);

        Assert.Equal("z-work", byEdition[0].Candidate.SearchDocument.Key);
        Assert.Equal("a-work", byKey[0].Candidate.SearchDocument.Key);
    }

    private RankedCandidate Single(QueryIntent intent, CandidateRankingInput candidate) =>
        Assert.Single(_ranker.Rank(intent, [candidate]));

    private static QueryIntent Intent(
        string? title = null,
        string? author = null,
        IReadOnlyList<string>? keywords = null,
        long? year = null,
        QueryFieldProvenance provenance = QueryFieldProvenance.Explicit) =>
        new(
            OriginalQuery: title ?? author ?? string.Join(' ', keywords ?? []),
            Title: title is null ? null : new QueryField<string>(title, provenance),
            Author: author is null ? null : new QueryField<string>(author, provenance),
            Keywords: keywords?.Select(keyword => new QueryField<string>(keyword, provenance)).ToArray(),
            Year: year is null ? null : new QueryField<long>(year.Value, provenance));

    private static CandidateRankingInput Candidate(
        string key,
        string title,
        IReadOnlyList<string>? authors = null,
        long year = 0,
        CandidateRankingMetadata? metadata = null) =>
        new(new Doc {
            Key = key,
            Title = title,
            AuthorName = authors?.ToList() ?? [],
            AuthorKey = [],
            FirstPublishYear = year
        }, metadata);
}