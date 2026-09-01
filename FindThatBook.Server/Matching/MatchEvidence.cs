namespace FindThatBook.Server.Matching;

public enum TitleMatchKind {
    None,
    FuzzyTokens,
    SignificantTokenOverlap,
    ContainsCompleteQuery,
    ExactMainTitle,
    ExactFullTitle
}

public enum AuthorMatchKind {
    None,
    FuzzySurname,
    MultiToken,
    ExactSurname,
    InitialsAndSurname,
    ExactFullName,
    CanonicalConflict
}

public enum YearMatchKind {
    None,
    OutsideTolerance,
    WithinThreeYears,
    WithinOneYear,
    Exact
}

public sealed record MatchEvidence(
    TitleMatchKind TitleKind,
    int TitleScore,
    bool HasMeaningfulTitleMatch,
    AuthorMatchKind AuthorKind,
    int AuthorScore,
    bool HasMeaningfulAuthorMatch,
    bool IsCanonicalPrimaryAuthorMatch,
    bool HasCanonicalAuthorConflict,
    IReadOnlyList<string> MatchedKeywords,
    int KeywordScore,
    bool HasDistinctiveKeywordMatch,
    YearMatchKind YearKind,
    int YearScore) {
    public int TotalScore => TitleScore + AuthorScore + KeywordScore + YearScore;
}

public sealed record RankedCandidate(
    CandidateRankingInput Candidate,
    MatchEvidence Evidence,
    bool IsUseful) {
    public int Score => Evidence.TotalScore;
}