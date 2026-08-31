namespace FindThatBook.Server.Services.OpenLibrary;

public class OpenLibraryApiConnectionService : IApiConnectionService {
    public const string ServiceKey = "OpenLibrary";

    private HttpClient HttpClient { get; }

    public OpenLibraryApiConnectionService(HttpClient httpClient) {
        HttpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<HttpResponseMessage> SearchAsync(string query, CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        
        return await HttpClient.GetAsync($"search.json?q={Uri.EscapeDataString(query)}", cancellationToken);
    }
}
