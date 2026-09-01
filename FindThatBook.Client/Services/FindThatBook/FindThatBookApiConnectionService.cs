using System.Net.Http.Json;
using FindThatBook.Client.Models;
using FindThatBook.Client.Serialization;

namespace FindThatBook.Client.Services.FindThatBook;

public sealed class FindThatBookApiConnectionService(HttpClient httpClient) : IApiConnectionService {
    private HttpClient HttpClient { get; } =
        httpClient ?? throw new ArgumentNullException(nameof(httpClient));

    public async Task<BookSearchResponse> SearchAsync(
        string query,
        CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        string requestUri = $"Search?q={Uri.EscapeDataString(query.Trim())}";
        using HttpResponseMessage response = await HttpClient.GetAsync(requestUri, cancellationToken);

        if (!response.IsSuccessStatusCode) {
            ApiProblemResponse? problem = await response.Content.ReadFromJsonAsync(
                ClientJsonSerializerContext.Default.ApiProblemResponse,
                cancellationToken);

            throw new ApiConnectionException(
                problem?.Title ?? "The search request could not be completed.",
                response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync(
                   ClientJsonSerializerContext.Default.BookSearchResponse,
                   cancellationToken)
               ?? new BookSearchResponse();
    }
}