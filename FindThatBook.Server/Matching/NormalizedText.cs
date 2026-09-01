namespace FindThatBook.Server.Matching;

public sealed record NormalizedText(
    string Original,
    string Strict,
    string Loose,
    IReadOnlyList<string> StrictTokens,
    IReadOnlyList<string> LooseTokens);