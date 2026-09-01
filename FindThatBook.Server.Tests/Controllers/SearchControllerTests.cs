using System.Text.Json;
using FindThatBook.Server.Controllers;
using FindThatBook.Server.Models;
using FindThatBook.Server.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FindThatBook.Server.Tests.Controllers;

public class SearchControllerTests {
    [Fact]
    public async Task SearchAsync_ReturnsTheOrchestratedResponse() {
        BookSearchResponse response = new() {
            Results = [new BookSearchCandidate { OpenLibraryKey = "OL1W", Title = "Dune" }]
        };

        SearchController controller = new(new StubBookSearchService(response));

        IActionResult action = await controller.SearchAsync("Dune", CancellationToken.None);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(action);
        Assert.Same(response, ok.Value);
    }

    [Fact]
    public async Task SearchAsync_RejectsBlankQueriesBeforeCallingTheService() {
        StubBookSearchService service = new(new BookSearchResponse());
        SearchController controller = new(service);

        IActionResult action = await controller.SearchAsync("  ", CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(action);
        Assert.Equal(0, service.CallCount);
    }

    [Theory]
    [InlineData(typeof(HttpRequestException), StatusCodes.Status502BadGateway)]
    [InlineData(typeof(JsonException), StatusCodes.Status502BadGateway)]
    [InlineData(typeof(TaskCanceledException), StatusCodes.Status504GatewayTimeout)]
    public async Task SearchAsync_MapsExpectedUpstreamFailures(Type exceptionType, int expectedStatus) {
        Exception exception = (Exception)Activator.CreateInstance(exceptionType)!;
        SearchController controller = new(new StubBookSearchService(exception));

        IActionResult action = await controller.SearchAsync("Dune", CancellationToken.None);

        ObjectResult problem = Assert.IsType<ObjectResult>(action);
        Assert.Equal(expectedStatus, problem.StatusCode);
    }

    private sealed class StubBookSearchService : IBookSearchService {
        private BookSearchResponse? Response { get; }

        private Exception? Exception { get; }

        public StubBookSearchService(BookSearchResponse response) {
            Response = response;
        }

        public StubBookSearchService(Exception exception) {
            Exception = exception;
        }

        public int CallCount { get; private set; }

        public Task<BookSearchResponse> SearchAsync(
            string query,
            CancellationToken cancellationToken = default) {
            CallCount++;

            return Exception is null
                ? Task.FromResult(Response!)
                : Task.FromException<BookSearchResponse>(Exception);
        }
    }
}