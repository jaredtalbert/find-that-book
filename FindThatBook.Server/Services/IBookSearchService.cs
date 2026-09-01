using FindThatBook.Server.Models;

namespace FindThatBook.Server.Services;

public interface IBookSearchService {
    Task<OpenLibraryResponse> SearchAsync(string query, CancellationToken cancellationToken = default);
}