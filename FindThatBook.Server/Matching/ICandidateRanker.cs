namespace FindThatBook.Server.Matching;

public interface ICandidateRanker {
    /// <summary>
    /// Evaluates and orders every candidate. Callers retain unusable candidates for
    /// provisional enrichment and filter by <see cref="RankedCandidate.IsUseful"/>
    /// only when producing final results.
    /// </summary>
    IReadOnlyList<RankedCandidate> Rank(
        QueryIntent intent,
        IEnumerable<CandidateRankingInput> candidates);
}