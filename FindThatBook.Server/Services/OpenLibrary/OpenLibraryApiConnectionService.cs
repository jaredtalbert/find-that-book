using FindThatBook.Server.Gemini;

namespace FindThatBook.Server.Services.OpenLibrary;

public class OpenLibraryApiConnectionService : IApiConnectionService {
    public const string ServiceKey = "OpenLibrary";

    private HttpClient HttpClient { get; }

    public OpenLibraryApiConnectionService(HttpClient httpClient) {
        HttpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<HttpResponseMessage> SearchAsync(string query, CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        // TODO: how do we determine whether or not to use AI? a toggle? heuristics? more AI?
        string geminiResult = await GeminiClient.SimplifyUserQueryAsync(query);

        // TODO: the OL API lets us request specific fields; adjust the query parameters so we're not wasting resources
        return await HttpClient.GetAsync($"search.json?q={Uri.EscapeDataString(geminiResult)}", cancellationToken);
    }
}