namespace FindThatBook.Server.Matching;

public sealed record NormalizedTitle(
    NormalizedText Full,
    NormalizedText MainTitle,
    NormalizedText? Subtitle);