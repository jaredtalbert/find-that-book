using FindThatBook.Server.Models;

namespace FindThatBook.Server.Matching;

public sealed record CandidateRankingMetadata(
    IReadOnlyList<string>? Subjects = null,
    long? EditionCount = null,
    IReadOnlyList<string>? CanonicalAuthorNames = null,
    bool HasCanonicalAuthorData = false,
    bool HasCompleteCanonicalAuthorData = false,
    string? CanonicalTitle = null) {
    public IReadOnlyList<string> SubjectValues => Subjects ?? [];

    public IReadOnlyList<string> CanonicalAuthors => CanonicalAuthorNames ?? [];
}

public sealed record CandidateRankingInput(
    Doc SearchDocument,
    CandidateRankingMetadata? Metadata = null) {
    private static CandidateRankingMetadata EmptyMetadata { get; } = new();

    public CandidateRankingMetadata RankingMetadata => Metadata ?? EmptyMetadata;
}