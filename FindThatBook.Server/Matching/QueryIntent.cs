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

    /// <summary>
    /// 
    /// </summary>
    /// <param name="originalQuery"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static QueryIntent CreateFallback(string originalQuery) {
        ArgumentException.ThrowIfNullOrWhiteSpace(originalQuery);

        QueryField<string>[] keywords = TextNormalizer.Normalize(originalQuery).LooseTokens
            .Distinct(StringComparer.Ordinal)
            .Select(token => new QueryField<string>(token, QueryFieldProvenance.Extracted))
            .ToArray();

        return new QueryIntent(
            originalQuery,
            Keywords: keywords,
            UsedFallback: true);
    }
}