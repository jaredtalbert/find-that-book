using System.Collections.Concurrent;
using FindThatBook.Server.Gemini;
using FindThatBook.Server.Matching;
using FindThatBook.Server.Models;
using FindThatBook.Server.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FindThatBook.Server.Tests.Services;

public class BookSearchServiceTests {
    [Fact]
    public async Task SearchAsync_UsesStructuredDiscoveryAndRejectsCanonicalAuthorConflicts() {
        QueryIntent intent = Intent(title: "Dune", author: "Frank Herbert");

        StubBookCatalogClient catalog = new() {
            SearchResponse = Response(
                Document("wrong", "Dune", ["Ursula Le Guin"], ["wrong-author"]),
                Document("exact", "Dune", ["Frank Herbert"], ["frank"]),
                Document("related", "Dune Messiah", ["Frank Herbert"], ["frank"]))
        };

        catalog.Works["wrong"] = Work("wrong", "Dune", "wrong-author");
        catalog.Works["exact"] = Work("exact", "Dune", "frank");
        catalog.Works["related"] = Work("related", "Dune Messiah", "frank");
        BookSearchService service = CreateService(new StubQueryInterpreter(intent), catalog, new CandidateRanker());

        OpenLibraryResponse result = await service.SearchAsync("Dune by Frank Herbert", CancellationToken.None);

        Assert.NotNull(catalog.SearchRequest);
        Assert.Equal("Dune", catalog.SearchRequest.Title);
        Assert.Equal("Frank Herbert", catalog.SearchRequest.Author);
        Assert.Null(catalog.SearchRequest.RawQuery);
        Assert.Equal(25, catalog.SearchRequest.Limit);
        Assert.Equal(["exact", "related"], result.Docs.Select(document => document.Key));
        Assert.DoesNotContain(result.Docs, document => document.Key == "wrong");
        Assert.Empty(catalog.AuthorRequests);
    }

    [Fact]
    public async Task SearchAsync_UsesRawFallbackAndSurvivesEnrichmentFailure() {
        StubBookCatalogClient catalog = new() {
            SearchResponse = Response(Document("wizard", "Boy Wizard School"))
        };

        catalog.WorkFailures.Add("wizard");

        BookSearchService service = CreateService(
            new StubQueryInterpreter(new InvalidOperationException("Interpreter failed")),
            catalog,
            new CandidateRanker());

        OpenLibraryResponse result = await service.SearchAsync("boy wizard school", CancellationToken.None);

        Assert.Equal("boy wizard school", catalog.SearchRequest?.RawQuery);
        Assert.Null(catalog.SearchRequest?.Title);
        Assert.Equal("wizard", Assert.Single(result.Docs).Key);
        Assert.Contains("wizard", catalog.WorkRequests);
    }

    [Fact]
    public async Task SearchAsync_DiversifiesTheFiveEnrichmentCandidates() {
        string[] keys = ["a", "b", "c", "f", "d", "e"];

        StubBookCatalogClient catalog = new() {
            SearchResponse = Response(keys.Select(key => Document(key, $"Title {key}")).ToArray())
        };

        foreach (string key in keys) {
            catalog.Works[key] = Work(key, $"Title {key}");
        }

        ScriptedRanker ranker = new((call, candidates) => {
            if (call > 1) {
                return candidates.Select(candidate => Ranked(candidate, totalScore: 60, titleScore: 60, useful: true))
                    .ToArray();
            }

            Dictionary<string, (int Total, int Title, int Author)> scores = new() {
                ["a"] = (100, 60, 30),
                ["b"] = (90, 55, 25),
                ["c"] = (80, 50, 20),
                ["f"] = (70, 0, 0),
                ["d"] = (60, 50, 0),
                ["e"] = (50, 0, 40)
            };

            return candidates
                .Select(candidate => {
                    (int total, int title, int author) = scores[candidate.SearchDocument.Key];

                    return Ranked(candidate, total, title, author, useful: true);
                })
                .OrderByDescending(candidate => candidate.Score)
                .ToArray();
        });

        BookSearchService service = CreateService(
            new StubQueryInterpreter(Intent(title: "Title", author: "Author")),
            catalog,
            ranker);

        await service.SearchAsync("Title by Author", CancellationToken.None);

        Assert.Equal(5, catalog.WorkRequests.Count);
        Assert.Equal(["a", "b", "c", "d", "e"], catalog.WorkRequests.Order());
        Assert.DoesNotContain("f", catalog.WorkRequests);
    }

    [Fact]
    public async Task SearchAsync_MapsCanonicalAuthorsAndFetchesOnlyMissingNames() {
        Doc document = Document("work", "Shared Title", ["Secondary Author"], ["secondary"]);

        StubBookCatalogClient catalog = new() {
            SearchResponse = Response(document)
        };

        catalog.Works["work"] = Work("work", "Canonical Title", "primary", "secondary");

        catalog.Authors["primary"] = new OpenLibraryAuthor {
            Key = "primary",
            Name = "Primary Author"
        };

        CapturingRanker ranker = new();

        BookSearchService service = CreateService(
            new StubQueryInterpreter(Intent(title: "Shared Title", author: "Secondary Author")),
            catalog,
            ranker);

        await service.SearchAsync("Shared Title by Secondary Author", CancellationToken.None);

        CandidateRankingMetadata metadata = Assert.Single(ranker.FinalInputs).RankingMetadata;
        Assert.Equal(["Primary Author", "Secondary Author"], metadata.CanonicalAuthors);
        Assert.Equal("Canonical Title", metadata.CanonicalTitle);
        Assert.True(metadata.HasCompleteCanonicalAuthorData);
        Assert.Equal("primary", Assert.Single(catalog.AuthorRequests));
    }

    [Fact]
    public async Task SearchAsync_PreservesAuthorPositionsWhenPrimaryNameCannotBeResolved() {
        Doc document = Document("work", "Shared Title", ["Secondary Author"], ["secondary"]);

        StubBookCatalogClient catalog = new() {
            SearchResponse = Response(document)
        };

        catalog.Works["work"] = Work("work", "Shared Title", "missing-primary", "secondary");
        CapturingRanker ranker = new();

        BookSearchService service = CreateService(
            new StubQueryInterpreter(Intent(title: "Shared Title", author: "Secondary Author")),
            catalog,
            ranker);

        await service.SearchAsync("Shared Title by Secondary Author", CancellationToken.None);

        CandidateRankingMetadata metadata = Assert.Single(ranker.FinalInputs).RankingMetadata;
        Assert.Equal([string.Empty, "Secondary Author"], metadata.CanonicalAuthors);
        Assert.False(metadata.HasCompleteCanonicalAuthorData);
    }

    [Fact]
    public async Task SearchAsync_DeduplicatesByWorkKeyThenTitleAndAuthorFallback() {
        Doc keyed = Document("same-work", "Dune", ["Frank Herbert"]);
        Doc keyedDuplicate = Document("same-work", "Dune", ["Frank Herbert"]);
        Doc fallback = Document(string.Empty, "The Hobbit", ["J. R. R. Tolkien"]);
        Doc fallbackDuplicate = Document(string.Empty, "the hobbit", ["JRR Tolkien"]);
        Doc titleOnly = Document(string.Empty, "Untitled Work");
        Doc titleOnlyDuplicate = Document(string.Empty, "Untitled Work");

        StubBookCatalogClient catalog = new() {
            SearchResponse = Response(
                keyed,
                keyedDuplicate,
                fallback,
                fallbackDuplicate,
                titleOnly,
                titleOnlyDuplicate)
        };

        catalog.Works["same-work"] = Work("same-work", "Dune");

        BookSearchService service = CreateService(
            new StubQueryInterpreter(Intent(title: "Dune")),
            catalog,
            new CapturingRanker());

        OpenLibraryResponse result = await service.SearchAsync("Dune", CancellationToken.None);

        Assert.Equal(4, result.Docs.Count);
        Assert.Equal("same-work", Assert.Single(catalog.WorkRequests));
    }

    [Fact]
    public async Task SearchAsync_ReturnsNoDocumentsWhenFinalCandidatesAreNotUseful() {
        StubBookCatalogClient catalog = new() {
            SearchResponse = Response(Document("unrelated", "Pride and Prejudice"))
        };

        catalog.Works["unrelated"] = Work("unrelated", "Pride and Prejudice");

        BookSearchService service = CreateService(
            new StubQueryInterpreter(Intent(title: "Dune")),
            catalog,
            new CandidateRanker());

        OpenLibraryResponse result = await service.SearchAsync("Dune", CancellationToken.None);

        Assert.Empty(result.Docs);
    }

    [Fact]
    public async Task SearchAsync_EnrichesAndReturnsAtMostFiveCandidates() {
        Doc[] documents = Enumerable.Range(1, 6)
            .Select(index => Document($"work-{index}", "Dune"))
            .ToArray();

        StubBookCatalogClient catalog = new() {
            SearchResponse = Response(documents)
        };

        foreach (Doc document in documents) {
            catalog.Works[document.Key] = Work(document.Key, document.Title);
        }

        BookSearchService service = CreateService(
            new StubQueryInterpreter(Intent(title: "Dune")),
            catalog,
            new CandidateRanker());

        OpenLibraryResponse result = await service.SearchAsync("Dune", CancellationToken.None);

        Assert.Equal(5, result.Docs.Count);
        Assert.Equal(5, catalog.WorkRequests.Count);
    }

    [Fact]
    public async Task SearchAsync_EnrichesSelectedWorksConcurrently() {
        Doc[] documents = Enumerable.Range(1, 5)
            .Select(index => Document($"work-{index}", "Dune"))
            .ToArray();

        ConcurrencyTrackingCatalogClient catalog = new(documents);

        BookSearchService service = CreateService(
            new StubQueryInterpreter(Intent(title: "Dune")),
            catalog,
            new CandidateRanker());

        await service.SearchAsync("Dune", CancellationToken.None);

        Assert.InRange(catalog.MaximumConcurrentWorkRequests, 2, 5);
    }

    private static BookSearchService CreateService(
        IQueryInterpreter interpreter,
        IBookCatalogClient catalog,
        ICandidateRanker ranker) =>
        new(interpreter, catalog, ranker, NullLogger<BookSearchService>.Instance);

    private static QueryIntent Intent(string? title = null, string? author = null) =>
        new(
            title ?? author ?? string.Empty,
            title is null ? null : new QueryField<string>(title, QueryFieldProvenance.Explicit),
            author is null ? null : new QueryField<string>(author, QueryFieldProvenance.Explicit));

    private static OpenLibraryResponse Response(params Doc[] documents) => new() {
        NumFound = documents.Length,
        Docs = documents.ToList()
    };

    private static Doc Document(
        string key,
        string title,
        IReadOnlyList<string>? authors = null,
        IReadOnlyList<string>? authorKeys = null) => new() {
        Key = key,
        Title = title,
        AuthorName = authors?.ToList() ?? [],
        AuthorKey = authorKeys?.ToList() ?? []
    };

    private static OpenLibraryWork Work(string key, string title, params string[] authorKeys) => new() {
        Key = key,
        Title = title,
        Authors = authorKeys
            .Select(authorKey => new OpenLibraryWorkAuthor {
                Author = new OpenLibraryAuthorReference { Key = authorKey }
            })
            .ToList()
    };

    private static RankedCandidate Ranked(
        CandidateRankingInput candidate,
        int totalScore,
        int titleScore = 0,
        int authorScore = 0,
        bool useful = true) {
        int keywordScore = totalScore - titleScore - authorScore;

        MatchEvidence evidence = new(
            titleScore > 0 ? TitleMatchKind.SignificantTokenOverlap : TitleMatchKind.None,
            titleScore,
            titleScore > 0,
            authorScore > 0 ? AuthorMatchKind.MultiToken : AuthorMatchKind.None,
            authorScore,
            authorScore > 0,
            false,
            false,
            authorScore > 0 ? AuthorEvidenceSource.SearchResponse : AuthorEvidenceSource.None,
            [],
            keywordScore,
            false,
            YearMatchKind.None,
            0);

        return new RankedCandidate(candidate, evidence, useful);
    }

    private sealed class StubQueryInterpreter : IQueryInterpreter {
        private readonly QueryIntent? _intent;
        private readonly Exception? _exception;

        public StubQueryInterpreter(QueryIntent intent) {
            _intent = intent;
        }

        public StubQueryInterpreter(Exception exception) {
            _exception = exception;
        }

        public Task<QueryIntent> InterpretAsync(string query, CancellationToken cancellationToken = default) =>
            _exception is null
                ? Task.FromResult(_intent!)
                : Task.FromException<QueryIntent>(_exception);
    }

    private sealed class StubBookCatalogClient : IBookCatalogClient {
        public OpenLibraryResponse SearchResponse { get; init; } = new();

        public Dictionary<string, OpenLibraryWork> Works { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, OpenLibraryAuthor> Authors { get; } = new(StringComparer.OrdinalIgnoreCase);

        public HashSet<string> WorkFailures { get; } = new(StringComparer.OrdinalIgnoreCase);

        public ConcurrentBag<string> WorkRequests { get; } = [];

        public ConcurrentBag<string> AuthorRequests { get; } = [];

        public BookCatalogSearchRequest? SearchRequest { get; private set; }

        public Task<BookCatalogSearchResult> SearchAsync(
            BookCatalogSearchRequest request,
            CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            SearchRequest = request;

            return Task.FromResult(new BookCatalogSearchResult(
                SearchResponse.Start,
                SearchResponse.NumFound,
                SearchResponse.Docs));
        }

        public Task<BookCatalogWork> GetWorkAsync(
            string workKey,
            CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            WorkRequests.Add(workKey);

            if (WorkFailures.Contains(workKey) || !Works.TryGetValue(workKey, out OpenLibraryWork? work)) {
                return Task.FromException<BookCatalogWork>(new HttpRequestException("Work unavailable"));
            }

            return Task.FromResult(new BookCatalogWork(
                work.Key,
                work.Title,
                work.Authors.Select(author => author.Author.Key).ToArray(),
                work.Subjects));
        }

        public Task<BookCatalogAuthor> GetAuthorAsync(
            string authorKey,
            CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            AuthorRequests.Add(authorKey);

            return Authors.TryGetValue(authorKey, out OpenLibraryAuthor? author)
                ? Task.FromResult(new BookCatalogAuthor(
                    author.Key,
                    author.Name,
                    author.AlternateNames))
                : Task.FromException<BookCatalogAuthor>(new HttpRequestException("Author unavailable"));
        }
    }

    private sealed class ScriptedRanker(
        Func<int, IReadOnlyList<CandidateRankingInput>, IReadOnlyList<RankedCandidate>> rank) : ICandidateRanker {
        private int _callCount;

        public IReadOnlyList<RankedCandidate> Rank(
            QueryIntent intent,
            IEnumerable<CandidateRankingInput> candidates) =>
            rank(Interlocked.Increment(ref _callCount), candidates.ToArray());
    }

    private sealed class CapturingRanker : ICandidateRanker {
        private int _callCount;

        public IReadOnlyList<CandidateRankingInput> FinalInputs { get; private set; } = [];

        public IReadOnlyList<RankedCandidate> Rank(
            QueryIntent intent,
            IEnumerable<CandidateRankingInput> candidates) {
            CandidateRankingInput[] inputs = candidates.ToArray();

            if (Interlocked.Increment(ref _callCount) > 1) {
                FinalInputs = inputs;
            }

            return inputs.Select(candidate => Ranked(candidate, 60, titleScore: 60, useful: true)).ToArray();
        }
    }

    private sealed class ConcurrencyTrackingCatalogClient(IReadOnlyList<Doc> documents) : IBookCatalogClient {
        private int _activeWorkRequests;
        private int _maximumConcurrentWorkRequests;

        public int MaximumConcurrentWorkRequests => _maximumConcurrentWorkRequests;

        public Task<BookCatalogSearchResult> SearchAsync(
            BookCatalogSearchRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new BookCatalogSearchResult(0, documents.Count, documents));

        public async Task<BookCatalogWork> GetWorkAsync(
            string workKey,
            CancellationToken cancellationToken = default) {
            int active = Interlocked.Increment(ref _activeWorkRequests);
            SetMaximum(active);

            try {
                await Task.Delay(25, cancellationToken);

                return new BookCatalogWork(workKey, "Dune", [], []);
            }
            finally {
                Interlocked.Decrement(ref _activeWorkRequests);
            }
        }

        public Task<BookCatalogAuthor> GetAuthorAsync(
            string authorKey,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("No author lookup is expected.");

        private void SetMaximum(int value) {
            int current;

            do {
                current = _maximumConcurrentWorkRequests;

                if (current >= value) {
                    return;
                }
            } while (Interlocked.CompareExchange(ref _maximumConcurrentWorkRequests, value, current) != current);
        }
    }
}