using System.Text.Json;
using FindThatBook.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace FindThatBook.Server.Controllers;

[ApiController]
[Route("[controller]")]
public class SearchController(IBookSearchService bookSearchService) : ControllerBase {
    private IBookSearchService BookSearchService { get; } =
        bookSearchService ?? throw new ArgumentNullException(nameof(bookSearchService));

    [HttpGet]
    public async Task<IActionResult> SearchAsync([FromQuery] string q, CancellationToken cancellationToken) {
        if (string.IsNullOrWhiteSpace(q)) {
            return BadRequest("A search query is required.");
        }

        try {
            return Ok(await BookSearchService.SearchAsync(q, cancellationToken));
        } catch (HttpRequestException) {
            return Problem(statusCode: StatusCodes.Status502BadGateway,
                title: "Unable to reach OpenLibrary.");
        } catch (JsonException) {
            return Problem(statusCode: StatusCodes.Status502BadGateway,
                title: "OpenLibrary returned an invalid response.");
        } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
            return Problem(statusCode: StatusCodes.Status504GatewayTimeout,
                title: "OpenLibrary did not respond in time.");
        }
    }
}