using Google.GenAI;
using Google.GenAI.Types;

namespace FindThatBook.Server.Gemini;

public interface IGeminiTextClient {
    Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default);
}

public sealed class GeminiTextClient : IGeminiTextClient {
    private const string Model = "gemini-3.5-flash-lite";

    private Lazy<Client> Client { get; } = new(() => new Client(), LazyThreadSafetyMode.ExecutionAndPublication);

    public async Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default) {
        GenerateContentConfig config = new() {
            ResponseMimeType = "application/json"
        };

        GenerateContentResponse response = await Client.Value.Models.GenerateContentAsync(
            Model,
            prompt,
            config,
            cancellationToken);

        return response.Text ?? throw new InvalidOperationException("Gemini returned no interpretation text.");
    }
}