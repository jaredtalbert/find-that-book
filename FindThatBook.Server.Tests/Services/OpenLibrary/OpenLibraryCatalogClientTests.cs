using System.Net;
using System.Text;
using System.Text.Json;
using FindThatBook.Server.Models;
using FindThatBook.Server.Services.OpenLibrary;
using Xunit;

namespace FindThatBook.Server.Tests.Services.OpenLibrary;

public class OpenLibraryCatalogClientTests {
    [Fact]
    public async Task SearchAsync_UsesStructuredFieldsAndDeserializesRequestedMetadata() {
        Uri? requestedUri = null;

        OpenLibraryCatalogClient client = CreateClient(request => {
            requestedUri = request.RequestUri;

            return JsonResponse("""
                                {
                                  "NUM_FOUND": 1,
                                  "DOCS": [{
                                    "KEY": "/works/OL16509148W",
                                    "TITLE": "The Song of Achilles",
                                    "AUTHOR_NAME": ["Madeline Miller"],
                                    "AUTHOR_KEY": ["/authors/OL1926056A"],
                                    "COVER_I": 7098465,
                                    "FIRST_PUBLISH_YEAR": 2011,
                                    "EDITION_COUNT": 42,
                                    "SUBJECT": ["Achilles (Greek mythology)"]
                                  }]
                                }
                                """);
        });

        OpenLibrarySearchRequest request = new(
            RawQuery: "ignored fallback",
            Title: "song of achilles",
            Author: "Madeline Miller");

        OpenLibraryResponse response = await client.SearchAsync(request, CancellationToken.None);

        Assert.NotNull(requestedUri);
        Assert.Equal("/search.json", requestedUri.AbsolutePath);
        IReadOnlyDictionary<string, string> query = ParseQuery(requestedUri);
        Assert.Equal("song of achilles", query["title"]);
        Assert.Equal("Madeline Miller", query["author"]);
        Assert.False(query.ContainsKey("first_publish_year"));
        Assert.Equal("25", query["limit"]);
        Assert.False(query.ContainsKey("q"));

        Assert.Equal(
            "key,title,author_name,author_key,first_publish_year,edition_count,cover_i,subject",
            query["fields"]);

        Doc document = Assert.Single(response.Docs);
        Assert.Equal("OL16509148W", document.Key);
        Assert.Equal("OL1926056A", Assert.Single(document.AuthorKey));
        Assert.Equal("Madeline Miller", Assert.Single(document.AuthorName));
        Assert.Equal(7098465, document.CoverId);
        Assert.Equal(2011, document.FirstPublishYear);
        Assert.Equal(42, document.EditionCount);
        Assert.Equal("Achilles (Greek mythology)", Assert.Single(document.Subjects));
    }

    [Fact]
    public async Task SearchAsync_UsesRawQueryWhenStructuredFieldsAreAbsent() {
        Uri? requestedUri = null;

        OpenLibraryCatalogClient client = CreateClient(request => {
            requestedUri = request.RequestUri;

            return JsonResponse("{\"docs\":[]}");
        });

        await client.SearchAsync(
            new OpenLibrarySearchRequest(RawQuery: "boy wizard school"),
            CancellationToken.None);

        IReadOnlyDictionary<string, string> query = ParseQuery(Assert.IsType<Uri>(requestedUri));
        Assert.Equal("boy wizard school", query["q"]);
        Assert.False(query.ContainsKey("title"));
        Assert.False(query.ContainsKey("author"));
    }

    [Fact]
    public async Task SearchAsync_MissingAndNullOptionalValuesProduceSafeDefaults() {
        OpenLibraryCatalogClient client = CreateClient(_ => JsonResponse("""
                                                                         {
                                                                           "docs": [{
                                                                             "key": "/works/OL1W",
                                                                             "title": null,
                                                                             "author_name": null,
                                                                             "author_key": null,
                                                                             "subject": null
                                                                           }]
                                                                         }
                                                                         """));

        OpenLibraryResponse response = await client.SearchAsync(
            new OpenLibrarySearchRequest(RawQuery: "anything"),
            CancellationToken.None);

        Doc document = Assert.Single(response.Docs);
        Assert.Equal(string.Empty, document.Title);
        Assert.Empty(document.AuthorName);
        Assert.Empty(document.AuthorKey);
        Assert.Empty(document.Subjects);
        Assert.Null(document.CoverId);
        Assert.Null(document.FirstPublishYear);
        Assert.Null(document.EditionCount);
    }

    [Fact]
    public async Task GetWorkAsync_PreservesCanonicalAuthorOrderAndNormalizesKeys() {
        Uri? requestedUri = null;

        OpenLibraryCatalogClient client = CreateClient(request => {
            requestedUri = request.RequestUri;

            return JsonResponse("""
                                {
                                  "key": "/works/OL27448W",
                                  "title": "The Lord of the Rings",
                                  "authors": [
                                    { "author": { "key": "/authors/OL26320A" } },
                                    { "author": { "key": "/authors/OL999A" } }
                                  ],
                                  "subjects": ["Middle Earth", "Fantasy fiction"]
                                }
                                """);
        });

        OpenLibraryWork work = await client.GetWorkAsync(
            "/works/OL27448W",
            CancellationToken.None);

        Assert.Equal("/works/OL27448W.json", requestedUri?.AbsolutePath);
        Assert.Equal("OL27448W", work.Key);
        Assert.Equal(["OL26320A", "OL999A"], work.Authors.Select(author => author.Author.Key));
        Assert.Equal(["Middle Earth", "Fantasy fiction"], work.Subjects);
    }

    [Fact]
    public async Task GetAuthorAsync_DeserializesFallbackAuthorMetadata() {
        Uri? requestedUri = null;

        OpenLibraryCatalogClient client = CreateClient(request => {
            requestedUri = request.RequestUri;

            return JsonResponse("""
                                {
                                  "key": "/authors/OL26320A",
                                  "name": "J. R. R. Tolkien",
                                  "alternate_names": ["John Ronald Reuel Tolkien"]
                                }
                                """);
        });

        OpenLibraryAuthor author = await client.GetAuthorAsync(
            "OL26320A",
            CancellationToken.None);

        Assert.Equal("/authors/OL26320A.json", requestedUri?.AbsolutePath);
        Assert.Equal("OL26320A", author.Key);
        Assert.Equal("J. R. R. Tolkien", author.Name);
        Assert.Equal("John Ronald Reuel Tolkien", Assert.Single(author.AlternateNames));
    }

    [Fact]
    public async Task Requests_PropagateHttpFailures() {
        OpenLibraryCatalogClient client = CreateClient(_ =>
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        HttpRequestException exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.GetWorkAsync("OL1W", CancellationToken.None));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.StatusCode);
    }

    [Fact]
    public async Task Requests_RejectAnEmptyJsonDocument() {
        OpenLibraryCatalogClient client = CreateClient(_ => JsonResponse("null"));

        await Assert.ThrowsAsync<JsonException>(() =>
            client.GetAuthorAsync("OL1A", CancellationToken.None));
    }

    [Fact]
    public async Task Requests_PropagateCancellation() {
        HttpClient httpClient = new(new NeverCompletingHttpMessageHandler()) {
            BaseAddress = new Uri("https://openlibrary.org/")
        };

        OpenLibraryCatalogClient client = new(httpClient);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.GetWorkAsync("OL1W", cancellation.Token));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task SearchAsync_RejectsInvalidLimits(int limit) {
        OpenLibraryCatalogClient client = CreateClient(_ => JsonResponse("{\"docs\":[]}"));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.SearchAsync(new OpenLibrarySearchRequest(RawQuery: "query", Limit: limit),
                CancellationToken.None));
    }

    [Fact]
    public async Task SearchAsync_RequiresAtLeastOneSearchTerm() {
        OpenLibraryCatalogClient client = CreateClient(_ => JsonResponse("{\"docs\":[]}"));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.SearchAsync(new OpenLibrarySearchRequest(), CancellationToken.None));
    }

    [Theory]
    [InlineData("")]
    [InlineData("/works/")]
    [InlineData("OL1W/editions")]
    public async Task GetWorkAsync_RejectsInvalidKeys(string key) {
        OpenLibraryCatalogClient client = CreateClient(_ => JsonResponse("{}"));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.GetWorkAsync(key, CancellationToken.None));
    }

    private static OpenLibraryCatalogClient CreateClient(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) {
        HttpClient httpClient = new(new StubHttpMessageHandler(responseFactory)) {
            BaseAddress = new Uri("https://openlibrary.org/")
        };

        return new OpenLibraryCatalogClient(httpClient);
    }

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK) {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private static IReadOnlyDictionary<string, string> ParseQuery(Uri uri) =>
        uri.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(parameter => parameter.Split('=', 2))
            .ToDictionary(
                pair => Uri.UnescapeDataString(pair[0]),
                pair => Uri.UnescapeDataString(pair[1]),
                StringComparer.Ordinal);

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(responseFactory(request));
    }

    private sealed class NeverCompletingHttpMessageHandler : HttpMessageHandler {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);

            throw new InvalidOperationException("The cancellation token should stop this request.");
        }
    }
}