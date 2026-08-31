using System.Net.Http;

namespace FindThatBook.Client.Services.OpenLibrary;

public class OpenLibraryApiConnectionService : IApiConnectionService {
    private HttpClient HttpClient { get; set; }
    
    public OpenLibraryApiConnectionService(HttpClient httpClient) {}
}