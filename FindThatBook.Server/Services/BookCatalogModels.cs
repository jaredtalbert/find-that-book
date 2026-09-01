using FindThatBook.Server.Models;

namespace FindThatBook.Server.Services;

public sealed record BookCatalogSearchRequest(
    string? RawQuery = null,
    string? Title = null,
    string? Author = null,
    int Limit = 25);

public sealed record BookCatalogSearchResult(
    int Start,
    int TotalFound,
    IReadOnlyList<Doc> Documents);

public sealed record BookCatalogWork(
    string Key,
    string Title,
    IReadOnlyList<string> AuthorKeys,
    IReadOnlyList<string> Subjects);

public sealed record BookCatalogAuthor(
    string Key,
    string Name,
    IReadOnlyList<string> AlternateNames);