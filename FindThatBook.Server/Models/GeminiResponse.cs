namespace FindThatBook.Server.Models;

public sealed class GeminiResponse {
    public GeminiTextField? Title { get; set; }

    public GeminiTextField? Author { get; set; }

    public List<GeminiTextField> Keywords { get; set; } = [];

    public GeminiYearField? Year { get; set; }
}

public sealed class GeminiTextField {
    public string? Value { get; set; }

    public string? Provenance { get; set; }
}

public sealed class GeminiYearField {
    public long? Value { get; set; }

    public string? Provenance { get; set; }
}