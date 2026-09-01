using System.Text.Json;
using FindThatBook.Server.Models;
using FindThatBook.Server.Serialization;

namespace FindThatBook.Server.Services.OpenLibrary;

/// <summary>
/// Typed access to Open Library. This remains separate from the legacy raw-response
/// service until search orchestration is migrated in the next implementation chunk.
/// </summary>
public sealed class OpenLibraryCatalogClient {
    private readonly HttpClient _httpClient;

    public OpenLibraryCatalogClient(HttpClient httpClient) {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public Task<OpenLibraryResponse> SearchAsync(
        OpenLibrarySearchRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        return GetAsync<OpenLibraryResponse>(request.BuildRelativeUri(), cancellationToken);
    }

    public Task<OpenLibraryWork> GetWorkAsync(
        string workKey,
        CancellationToken cancellationToken = default) {
        string normalizedKey = RequireKey(OpenLibraryKeys.Work(workKey), nameof(workKey));

        return GetAsync<OpenLibraryWork>($"works/{Uri.EscapeDataString(normalizedKey)}.json", cancellationToken);
    }

    public Task<OpenLibraryAuthor> GetAuthorAsync(
        string authorKey,
        CancellationToken cancellationToken = default) {
        string normalizedKey = RequireKey(OpenLibraryKeys.Author(authorKey), nameof(authorKey));

        return GetAsync<OpenLibraryAuthor>($"authors/{Uri.EscapeDataString(normalizedKey)}.json", cancellationToken);
    }

    private async Task<T> GetAsync<T>(string requestUri, CancellationToken cancellationToken) {
        using HttpResponseMessage response = await _httpClient.GetAsync(requestUri, cancellationToken);
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