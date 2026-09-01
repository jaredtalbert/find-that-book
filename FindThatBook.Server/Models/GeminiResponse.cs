namespace FindThatBook.Server.Models;

public struct GeminiResponse {

    public string? Title { get; init; }

    public string? Author { get; init; }

    public List<string>? Keywords { get; init; }

    public long? Year { get; init; }
}