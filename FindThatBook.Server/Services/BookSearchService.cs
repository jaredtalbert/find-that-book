using FindThatBook.Server.Gemini;
using FindThatBook.Server.Matching;
using FindThatBook.Server.Models;

namespace FindThatBook.Server.Services;

public sealed class BookSearchService(
    IQueryInterpreter queryInterpreter,
    IBookCatalogClient catalogClient,
    ICandidateRanker candidateRanker,
    ILogger<BookSearchService> logger) : IBookSearchService {
    private const int DiscoveryLimit = 25;
    private const int EnrichmentLimit = 5;
    private const int OverallSelectionCount = 3;

    private static HashSet<string> DiscoveryFillerWords { get; } = new(StringComparer.Ordinal) {
        "a", "about", "an", "and", "book", "books", "called", "for", "in", "novel", "novels", "of", "on",
        "or", "story", "stories", "tale", "the", "to", "where", "with"
    };

    private IQueryInterpreter QueryInterpreter { get; } =
        queryInterpreter ?? throw new ArgumentNullException(nameof(queryInterpreter));

    private IBookCatalogClient CatalogClient { get; } =
        catalogClient ?? throw new ArgumentNullException(nameof(catalogClient));

    private ICandidateRanker CandidateRanker { get; } =
        candidateRanker ?? throw new ArgumentNullException(nameof(candidateRanker));

    private ILogger<BookSearchService> Logger { get; } = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<BookSearchResponse> SearchAsync(
        string query,
        CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        QueryIntent intent = await InterpretAsync(query, cancellationToken);

        BookCatalogSearchResult discovery = await DiscoverAsync(intent, cancellationToken);

        Doc[] uniqueDocuments = Deduplicate(discovery.Documents).ToArray();

        if (uniqueDocuments.Length == 0) {
            return new BookSearchResponse();
        }

        CandidateRankingInput[] provisionalInputs = uniqueDocuments
            .Select(document => new CandidateRankingInput(document))
            .ToArray();

        IReadOnlyList<RankedCandidate> provisionalRanking = CandidateRanker.Rank(intent, provisionalInputs);
        IReadOnlyList<CandidateRankingInput> enrichmentSet = SelectForEnrichment(intent, provisionalRanking);

        CandidateRankingInput[] enriched = await Task.WhenAll(
            enrichmentSet.Select(candidate => EnrichAsync(candidate, cancellationToken)));

        IReadOnlyList<RankedCandidate> finalRanking = CandidateRanker.Rank(intent, enriched);

        return new BookSearchResponse {
            Results = finalRanking
                .Where(candidate => candidate.IsUseful)
                .Take(EnrichmentLimit)
                .Select(candidate => BookSearchResultFactory.Create(intent, candidate))
                .ToArray()
        };
    }

    private async Task<QueryIntent> InterpretAsync(string query, CancellationToken cancellationToken) {
        try {
            return await QueryInterpreter.InterpretAsync(query, cancellationToken);
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch (Exception exception) {
            Logger.LogWarning(exception,
                "Query interpreter failed outside its fallback boundary; using the raw query.");

            return QueryIntent.CreateFallback(query);
        }
    }

    private static BookCatalogSearchRequest CreateSearchRequest(QueryIntent intent) {
        string? title = NonBlank(intent.Title?.Value);
        string? author = NonBlank(intent.Author?.Value);

        return intent.UsedFallback || title is null && author is null
            ? new BookCatalogSearchRequest(RawQuery: intent.OriginalQuery, Limit: DiscoveryLimit)
            : new BookCatalogSearchRequest(Title: title, Author: author, Limit: DiscoveryLimit);
    }

    private async Task<BookCatalogSearchResult> DiscoverAsync(
        QueryIntent intent,
        CancellationToken cancellationToken) {
        BookCatalogSearchRequest primaryRequest = CreateSearchRequest(intent);
        BookCatalogSearchResult primary = await CatalogClient.SearchAsync(primaryRequest, cancellationToken);
        Doc[] primaryDocuments = Deduplicate(primary.Documents).Take(DiscoveryLimit).ToArray();

        BookCatalogSearchRequest? relaxedRequest = CreateRelaxedSearchRequest(
            primaryRequest,
            primaryDocuments.Length);

        if (relaxedRequest is null) {
            return new BookCatalogSearchResult(primary.Start, primary.TotalFound, primaryDocuments);
        }

        try {
            Logger.LogInformation(
                "Supplementing catalog discovery with relaxed query {RelaxedQuery}.",
                relaxedRequest.RawQuery);

            BookCatalogSearchResult relaxed = await CatalogClient.SearchAsync(relaxedRequest, cancellationToken);

            Doc[] mergedDocuments = Deduplicate(primaryDocuments.Concat(relaxed.Documents))
                .Take(DiscoveryLimit)
                .ToArray();

            return new BookCatalogSearchResult(
                primary.Start,
                Math.Max(primary.TotalFound, relaxed.TotalFound),
                mergedDocuments);
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch (Exception exception) {
            Logger.LogWarning(exception,
                "Relaxed catalog discovery failed; retaining the primary candidate pool.");

            return new BookCatalogSearchResult(primary.Start, primary.TotalFound, primaryDocuments);
        }
    }

    private static BookCatalogSearchRequest? CreateRelaxedSearchRequest(
        BookCatalogSearchRequest primaryRequest,
        int primaryDocumentCount) {
        if (primaryDocumentCount >= DiscoveryLimit || string.IsNullOrWhiteSpace(primaryRequest.RawQuery)) {
            return null;
        }

        string[] relaxedTokens = TextNormalizer.Normalize(primaryRequest.RawQuery).LooseTokens
            .Where(token => !DiscoveryFillerWords.Contains(token))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (relaxedTokens.Length < 2) {
            return null;
        }

        string relaxedQuery = string.Join(' ', relaxedTokens);
        string normalizedPrimary = TextNormalizer.Normalize(primaryRequest.RawQuery).Loose;

        return relaxedQuery == normalizedPrimary
            ? null
            : new BookCatalogSearchRequest(
                RawQuery: relaxedQuery,
                Limit: DiscoveryLimit - primaryDocumentCount);
    }

    private static IEnumerable<Doc> Deduplicate(IEnumerable<Doc> documents) {
        HashSet<string> workKeys = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> fallbackKeys = new(StringComparer.Ordinal);

        foreach (Doc document in documents) {
            if (!string.IsNullOrWhiteSpace(document.Key)) {
                if (workKeys.Add(document.Key.Trim())) {
                    yield return document;
                }

                continue;
            }

            string title = TextNormalizer.NormalizeTitle(document.Title).MainTitle.Loose;

            string author = document.AuthorName
                .Select(name => TextNormalizer.NormalizeAuthor(name).Loose)
                .FirstOrDefault(name => name.Length > 0) ?? string.Empty;

            if (title.Length == 0 || author.Length == 0 || fallbackKeys.Add($"{title}\n{author}")) {
                yield return document;
            }
        }
    }

    private static IReadOnlyList<CandidateRankingInput> SelectForEnrichment(
        QueryIntent intent,
        IReadOnlyList<RankedCandidate> provisionalRanking) {
        List<CandidateRankingInput> selected = [];
        HashSet<Doc> selectedDocuments = new(ReferenceEqualityComparer.Instance);

        void Add(RankedCandidate candidate) {
            if (selected.Count < EnrichmentLimit && selectedDocuments.Add(candidate.Candidate.SearchDocument)) {
                selected.Add(candidate.Candidate);
            }
        }

        foreach (RankedCandidate candidate in provisionalRanking.Take(OverallSelectionCount)) {
            Add(candidate);
        }

        bool hasAuthor = !string.IsNullOrWhiteSpace(intent.Author?.Value);

        if (hasAuthor) {
            RankedCandidate? strongestTitle = provisionalRanking
                .Where(candidate => !selectedDocuments.Contains(candidate.Candidate.SearchDocument))
                .Where(candidate => candidate.Evidence.TitleScore > 0)
                .OrderByDescending(candidate => candidate.Evidence.TitleScore)
                .ThenByDescending(candidate => candidate.Score)
                .FirstOrDefault();

            if (strongestTitle is not null) {
                Add(strongestTitle);
            }

            RankedCandidate? strongestAuthor = provisionalRanking
                .Where(candidate => !selectedDocuments.Contains(candidate.Candidate.SearchDocument))
                .Where(candidate => candidate.Evidence.AuthorScore > 0)
                .OrderByDescending(candidate => candidate.Evidence.AuthorScore)
                .ThenByDescending(candidate => candidate.Score)
                .FirstOrDefault();

            if (strongestAuthor is not null) {
                Add(strongestAuthor);
            }
        }

        foreach (RankedCandidate candidate in provisionalRanking) {
            Add(candidate);
        }

        return selected;
    }

    private async Task<CandidateRankingInput> EnrichAsync(
        CandidateRankingInput candidate,
        CancellationToken cancellationToken) {
        Doc document = candidate.SearchDocument;

        if (string.IsNullOrWhiteSpace(document.Key)) {
            return candidate;
        }

        try {
            BookCatalogWork work = await CatalogClient.GetWorkAsync(document.Key, cancellationToken);

            CanonicalAuthorResolution canonicalAuthors = await ResolveCanonicalAuthorsAsync(
                document,
                work,
                cancellationToken);

            CandidateRankingMetadata metadata = new(
                Subjects: work.Subjects,
                EditionCount: document.EditionCount,
                CanonicalAuthorNames: canonicalAuthors.Names,
                HasCanonicalAuthorData: true,
                HasCompleteCanonicalAuthorData: canonicalAuthors.IsComplete,
                CanonicalTitle: NonBlank(work.Title));

            return new CandidateRankingInput(document, metadata);
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch (Exception exception) {
            Logger.LogWarning(exception,
                "Unable to enrich catalog work {WorkKey}; retaining provisional evidence.",
                document.Key);

            return candidate;
        }
    }

    private async Task<CanonicalAuthorResolution> ResolveCanonicalAuthorsAsync(
        Doc document,
        BookCatalogWork work,
        CancellationToken cancellationToken) {
        Dictionary<string, string> searchAuthorNames = new(StringComparer.OrdinalIgnoreCase);
        int mappedAuthorCount = Math.Min(document.AuthorKey.Count, document.AuthorName.Count);

        for (int index = 0; index < mappedAuthorCount; index++) {
            string key = NonBlank(document.AuthorKey[index]) ?? string.Empty;
            string? name = NonBlank(document.AuthorName[index]);

            if (key.Length > 0 && name is not null) {
                searchAuthorNames.TryAdd(key, name);
            }
        }

        List<string> canonicalAuthors = [];
        HashSet<string> seenKeys = new(StringComparer.OrdinalIgnoreCase);
        bool isComplete = true;

        foreach (string workAuthorKey in work.AuthorKeys) {
            string authorKey = NonBlank(workAuthorKey) ?? string.Empty;

            if (authorKey.Length == 0) {
                isComplete = false;

                continue;
            }

            if (!seenKeys.Add(authorKey)) {
                continue;
            }

            if (searchAuthorNames.TryGetValue(authorKey, out string? mappedName)) {
                canonicalAuthors.Add(mappedName);

                continue;
            }

            try {
                BookCatalogAuthor author = await CatalogClient.GetAuthorAsync(authorKey, cancellationToken);
                string? resolvedName = NonBlank(author.Name);
                canonicalAuthors.Add(resolvedName ?? string.Empty);
                isComplete &= resolvedName is not null;
            } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                throw;
            } catch (Exception exception) {
                Logger.LogWarning(exception,
                    "Unable to resolve catalog author {AuthorKey}; continuing without its name.",
                    authorKey);

                canonicalAuthors.Add(string.Empty);
                isComplete = false;
            }
        }

        return new CanonicalAuthorResolution(canonicalAuthors, isComplete);
    }

    private static string? NonBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record CanonicalAuthorResolution(IReadOnlyList<string> Names, bool IsComplete);
}