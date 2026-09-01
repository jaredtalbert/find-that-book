namespace FindThatBook.Server.Matching;

public enum QueryFieldProvenance {
    Explicit,
    Extracted,
    Inferred
}

public sealed record QueryField<T>(T Value, QueryFieldProvenance Provenance);

public sealed record QueryIntent(
    string OriginalQuery,
    QueryField<string>? Title = null,
    QueryField<string>? Author = null,
    IReadOnlyList<QueryField<string>>? Keywords = null,
    QueryField<long>? Year = null,
    bool UsedFallback = false) {
    public IReadOnlyList<QueryField<string>> KeywordFields => Keywords ?? [];
}