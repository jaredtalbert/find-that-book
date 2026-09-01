using FindThatBook.Server.Models;

namespace FindThatBook.Server.Services;

public interface IBookSearchService {
    Task<BookSearchResponse> SearchAsync(string query, CancellationToken cancellationToken = default);
}