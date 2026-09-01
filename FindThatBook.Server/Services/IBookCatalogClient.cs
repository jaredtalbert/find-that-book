namespace FindThatBook.Server.Services;

public interface IBookCatalogClient {
    Task<BookCatalogSearchResult> SearchAsync(
        BookCatalogSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<BookCatalogWork> GetWorkAsync(
        string workKey,
        CancellationToken cancellationToken = default);

    Task<BookCatalogAuthor> GetAuthorAsync(
        string authorKey,
        CancellationToken cancellationToken = default);
}