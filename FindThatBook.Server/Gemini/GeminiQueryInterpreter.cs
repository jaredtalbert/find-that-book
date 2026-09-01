using System.Text.Json;
using System.Text.Json.Serialization;
using FindThatBook.Server.Matching;
using FindThatBook.Server.Models;
using FindThatBook.Server.Serialization;

namespace FindThatBook.Server.Gemini;

public sealed class GeminiQueryInterpreter(
    IGeminiTextClient geminiClient,
    ILogger<GeminiQueryInterpreter> logger) : IQueryInterpreter {
    private const string Prompt = """
                                  Interpret a user's book-search text into structured evidence.

                                  Return only JSON with this shape:
                                  {
                                    "title": { "value": "book title", "provenance": "explicit|extracted|inferred" } | null,
                                    "author": { "value": "author name", "provenance": "explicit|extracted|inferred" } | null,
                                    "keywords": [
                                      { "value": "one concept", "provenance": "explicit|extracted|inferred" }
                                    ],
                                    "year": { "value": 1859, "provenance": "explicit|extracted|inferred" } | null
                                  }

                                  Provenance definitions:
                                  - explicit: the value appears directly in the user's text.
                                  - extracted: the value is only normalized or structurally separated from text the user supplied.
                                  - inferred: the value adds information that the user did not state directly.

                                  A plot description may support inferred title, author, and keywords. Do not provide ranking,
                                  recommendations, explanations, or additional properties. Do not invent keywords when none are useful.

                                  User input as a JSON string:
                                  """;

    private static JsonSerializerOptions InterpretationJsonOptions { get; } = new(JsonDefaults.Options) {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    private IGeminiTextClient GeminiClient { get; } =
        geminiClient ?? throw new ArgumentNullException(nameof(geminiClient));

    private ILogger<GeminiQueryInterpreter> Logger { get; } =
        logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<QueryIntent> InterpretAsync(
        string query,
        CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        try {
            string response = await GeminiClient.GenerateAsync(
                Prompt + JsonSerializer.Serialize(query),
                cancellationToken);

            GeminiResponse interpretation = JsonSerializer.Deserialize<GeminiResponse>(
                response,
                InterpretationJsonOptions) ?? throw new JsonException("Gemini returned an empty interpretation.");

            return Map(query, interpretation);
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch (Exception exception) {
            Logger.LogWarning(exception,
                "Gemini query interpretation failed; using deterministic raw-query fallback.");

            return QueryIntent.CreateFallback(query);
        }
    }

    private static QueryIntent Map(string originalQuery, GeminiResponse response) {
        QueryField<string>? title = MapTextField(response.Title);
        QueryField<string>? author = MapTextField(response.Author);

        QueryField<string>[] keywords = (response.Keywords ?? [])
            .Select(MapTextField)
            .Where(field => field is not null)
            .Cast<QueryField<string>>()
            .DistinctBy(field => TextNormalizer.Normalize(field.Value).Loose, StringComparer.Ordinal)
            .ToArray();

        QueryField<long>? year = MapYearField(response.Year);

        if (title is null && author is null && keywords.Length == 0 && year is null) {
            throw new JsonException("Gemini returned no usable search evidence.");
        }

        return new QueryIntent(originalQuery, title, author, keywords, year);
    }

    private static QueryField<string>? MapTextField(GeminiTextField? field) {
        if (field is null) {
            return null;
        }

        if (string.IsNullOrWhiteSpace(field.Value)) {
            throw new JsonException("Gemini returned a text field without a value.");
        }

        return new QueryField<string>(field.Value.Trim(), ParseProvenance(field.Provenance));
    }

    private static QueryField<long>? MapYearField(GeminiYearField? field) {
        if (field is null) {
            return null;
        }

        return field.Value is null or <= 0
            ? throw new JsonException("Gemini returned an invalid publication year.")
            : new QueryField<long>(field.Value.Value, ParseProvenance(field.Provenance));
    }

    private static QueryFieldProvenance ParseProvenance(string? value) {
        bool isNamedValue = value is not null && Enum.GetNames<QueryFieldProvenance>()
            .Contains(value, StringComparer.OrdinalIgnoreCase);

        if (!isNamedValue || !Enum.TryParse(value, ignoreCase: true, out QueryFieldProvenance provenance)) {
            throw new JsonException($"Gemini returned an invalid provenance value: '{value}'.");
        }

        return provenance;
    }
}