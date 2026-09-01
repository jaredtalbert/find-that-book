using System.Text.Json.Serialization;

namespace FindThatBook.Server.Models;

public sealed class BookSearchResponse {
    public IReadOnlyList<BookSearchCandidate> Results { get; init; } = [];
}

[JsonConverter(typeof(JsonStringEnumConverter<SearchConfidence>))]
public enum SearchConfidence {
    Possible,
    Likely,
    Strong
}