using FindThatBook.Client.Models;

namespace FindThatBook.Client.Services;

public interface IApiConnectionService {
    Task<BookSearchResponse> SearchAsync(
        string query,
        CancellationToken cancellationToken = default);
}