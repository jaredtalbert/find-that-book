using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace FindThatBook.Client.Services;

// providing an interface for potential future expansion to other library providers
public interface IApiConnectionService {
    // The caller owns the response and must dispose it after reading the content.
    Task<HttpResponseMessage> SearchAsync(string query, CancellationToken cancellationToken = default);
}
