using System.Text.Json;
using FindThatBook.Server.Models;
using FindThatBook.Server.Serialization;

namespace FindThatBook.Server.Services.OpenLibrary;

/// <summary>
/// Adapts Open Library's transport models and endpoints to the catalog abstraction
/// consumed by search orchestration.
/// </summary>
public sealed class OpenLibraryCatalogClient : IBookCatalogClient {
    private HttpClient HttpClient { get; }

    public OpenLibraryCatalogClient(HttpClient httpClient) {
        HttpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<BookCatalogSearchResult> SearchAsync(
        BookCatalogSearchRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        OpenLibrarySearchRequest openLibraryRequest = new(
            request.RawQuery,
            request.Title,
            request.Author,
            request.Limit);

        OpenLibraryResponse response = await GetAsync<OpenLibraryResponse>(
            openLibraryRequest.BuildRelativeUri(),
            cancellationToken);

        return new BookCatalogSearchResult(response.Start, response.NumFound, response.Docs);
    }

    public async Task<BookCatalogWork> GetWorkAsync(
        string workKey,
        CancellationToken cancellationToken = default) {
        string normalizedKey = RequireKey(OpenLibraryKeys.Work(workKey), nameof(workKey));

        OpenLibraryWork work = await GetAsync<OpenLibraryWork>(
            $"works/{Uri.EscapeDataString(normalizedKey)}.json",
            cancellationToken);

        return new BookCatalogWork(
            work.Key,
            work.Title,
            work.Authors.Select(author => author.Author.Key).ToArray(),
            work.Subjects);
    }

    public async Task<BookCatalogAuthor> GetAuthorAsync(
        string authorKey,
        CancellationToken cancellationToken = default) {
        string normalizedKey = RequireKey(OpenLibraryKeys.Author(authorKey), nameof(authorKey));

        OpenLibraryAuthor author = await GetAsync<OpenLibraryAuthor>(
            $"authors/{Uri.EscapeDataString(normalizedKey)}.json",
            cancellationToken);

        return new BookCatalogAuthor(author.Key, author.Name, author.AlternateNames);
    }

    private async Task<T> GetAsync<T>(string requestUri, CancellationToken cancellationToken) {
        using HttpResponseMessage response = await HttpClient.GetAsync(requestUri, cancellationToken);
        response.EnsureSuccessStatusCode();

        T? result = await response.Content.ReadFromJsonAsync<T>(JsonDefaults.Options, cancellationToken);

        return result ?? throw new JsonException($"Open Library returned an empty {typeof(T).Name} response.");
    }

    private static string RequireKey(string key, string parameterName) {
        if (string.IsNullOrWhiteSpace(key)) {
            throw new ArgumentException("An Open Library key is required.", parameterName);
        }

        if (key.IndexOfAny(['/', '\\', '?', '#']) >= 0) {
            throw new ArgumentException("The Open Library key must be a single identifier.", parameterName);
        }

        return key;
    }
}