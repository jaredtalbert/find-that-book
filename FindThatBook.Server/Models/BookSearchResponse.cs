using System.Text.Json.Serialization;

namespace FindThatBook.Server.Models;

public sealed class BookSearchResponse {
    public IReadOnlyList<BookSearchCandidate> Results { get; init; } = [];
}

public sealed class BookSearchCandidate {
    public string? OpenLibraryKey { get; init; }

    public string Title { get; init; } = string.Empty;

    public IReadOnlyList<string> Authors { get; init; } = [];

    public long? FirstPublishYear { get; init; }

    public string? OpenLibraryUrl { get; init; }

    public string? CoverImageUrl { get; init; }

    public SearchConfidence Confidence { get; init; }

    public string Explanation { get; init; } = string.Empty;
}

[JsonConverter(typeof(JsonStringEnumConverter<SearchConfidence>))]
public enum SearchConfidence {
    Possible,
    Likely,
    Strong
}