using System.Text.Json;
using FindThatBook.Server.Gemini;
using FindThatBook.Server.Matching;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FindThatBook.Server.Tests.Gemini;

public class GeminiQueryInterpreterTests {
    [Fact]
    public async Task InterpretAsync_MapsStructuredFieldsAndProvenance() {
        StubGeminiTextClient gemini = new("""
                                          {
                                            "title": { "value": "The Song of Achilles", "provenance": "extracted" },
                                            "author": { "value": "Madeline Miller", "provenance": "explicit" },
                                            "keywords": [
                                              { "value": "mythology", "provenance": "explicit" },
                                              { "value": "Mythology", "provenance": "inferred" }
                                            ],
                                            "year": { "value": 2011, "provenance": "inferred" }
                                          }
                                          """);

        GeminiQueryInterpreter interpreter = CreateInterpreter(gemini);

        QueryIntent result = await interpreter.InterpretAsync(
            "song of achilles by Madeline Miller",
            CancellationToken.None);

        Assert.Equal("The Song of Achilles", result.Title?.Value);
        Assert.Equal(QueryFieldProvenance.Extracted, result.Title?.Provenance);
        Assert.Equal("Madeline Miller", result.Author?.Value);
        Assert.Equal(QueryFieldProvenance.Explicit, result.Author?.Provenance);
        Assert.Equal("mythology", Assert.Single(result.KeywordFields).Value);
        Assert.Equal(2011, result.Year?.Value);
        Assert.Equal(QueryFieldProvenance.Inferred, result.Year?.Provenance);
        Assert.False(result.UsedFallback);
    }

    [Fact]
    public async Task InterpretAsync_QuotesTheUserInputInsideThePrompt() {
        StubGeminiTextClient gemini = new("""
                                          {
                                            "title": { "value": "Dune", "provenance": "explicit" },
                                            "author": null,
                                            "keywords": [],
                                            "year": null
                                          }
                                          """);

        GeminiQueryInterpreter interpreter = CreateInterpreter(gemini);

        const string query = "title: \"Dune\"";
        await interpreter.InterpretAsync(query, CancellationToken.None);

        const string marker = "User input as a JSON string:";
        string prompt = Assert.IsType<string>(gemini.Prompt);

        string serializedInput = prompt[(prompt.LastIndexOf(marker, StringComparison.Ordinal) + marker.Length)..]
            .Trim();

        Assert.Equal(query, JsonSerializer.Deserialize<string>(serializedInput));
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{\"title\":null,\"author\":null,\"keywords\":[],\"year\":null}")]
    [InlineData("{\"title\":{\"value\":\"Dune\",\"provenance\":\"certain\"}}")]
    [InlineData("{\"title\":{\"value\":\"Dune\",\"provenance\":\"1\"}}")]
    [InlineData("{\"title\":{\"value\":\"Dune\",\"provenance\":\"explicit\"},\"ranking\":1}")]
    public async Task InterpretAsync_InvalidStructuredOutputUsesFallback(string response) {
        GeminiQueryInterpreter interpreter = CreateInterpreter(new StubGeminiTextClient(response));

        QueryIntent result = await interpreter.InterpretAsync("desert planet politics", CancellationToken.None);

        Assert.True(result.UsedFallback);

        Assert.Equal(["desert", "planet", "politics"],
            result.KeywordFields.Select(keyword => keyword.Value));

        Assert.All(result.KeywordFields,
            keyword => Assert.Equal(QueryFieldProvenance.Extracted, keyword.Provenance));
    }

    [Fact]
    public async Task InterpretAsync_ClientFailureUsesFallback() {
        GeminiQueryInterpreter interpreter = CreateInterpreter(
            new StubGeminiTextClient(new HttpRequestException("Unavailable")));

        QueryIntent result = await interpreter.InterpretAsync("boy wizard school", CancellationToken.None);

        Assert.True(result.UsedFallback);
        Assert.Equal("boy wizard school", result.OriginalQuery);
    }

    [Fact]
    public async Task InterpretAsync_RequestCancellationIsNotConvertedToFallback() {
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        GeminiQueryInterpreter interpreter = CreateInterpreter(
            new StubGeminiTextClient(new OperationCanceledException(cancellation.Token)));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            interpreter.InterpretAsync("Dune", cancellation.Token));
    }

    private static GeminiQueryInterpreter CreateInterpreter(IGeminiTextClient client) =>
        new(client, NullLogger<GeminiQueryInterpreter>.Instance);

    private sealed class StubGeminiTextClient : IGeminiTextClient {
        private readonly string? _response;
        private readonly Exception? _exception;

        public StubGeminiTextClient(string response) {
            _response = response;
        }

        public StubGeminiTextClient(Exception exception) {
            _exception = exception;
        }

        public string? Prompt { get; private set; }

        public Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default) {
            Prompt = prompt;

            return _exception is null
                ? Task.FromResult(_response!)
                : Task.FromException<string>(_exception);
        }
    }
}