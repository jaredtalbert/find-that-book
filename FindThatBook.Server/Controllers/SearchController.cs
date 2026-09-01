using System.Text.Json;
using FindThatBook.Server.Models;
using FindThatBook.Server.Serialization;
using FindThatBook.Server.Services;
using FindThatBook.Server.Services.OpenLibrary;
using Microsoft.AspNetCore.Mvc;

namespace FindThatBook.Server.Controllers;

[ApiController]
[Route("[controller]")]
public class SearchController : ControllerBase {
    private IApiConnectionService ApiConnectionService { get; }

    public SearchController(
        [FromKeyedServices(OpenLibraryApiConnectionService.ServiceKey)] IApiConnectionService apiConnectionService) {
        ApiConnectionService = apiConnectionService;
    }

    [HttpGet]
    public async Task<IActionResult> SearchAsync([FromQuery] string q, CancellationToken cancellationToken) {
        if (string.IsNullOrWhiteSpace(q)) {
            return BadRequest("A search query is required.");
        }

        try {
            using HttpResponseMessage response = await ApiConnectionService.SearchAsync(q, cancellationToken);
            response.EnsureSuccessStatusCode();

            string content = await response.Content.ReadAsStringAsync(cancellationToken);

            // TODO: This defeats the purpose of DIing IApiConnectionService
            OpenLibraryResponse openLibraryResponse = JsonSerializer.Deserialize<OpenLibraryResponse>(
                content,
                JsonDefaults.Options);

            return new OkObjectResult(openLibraryResponse);
        } catch (HttpRequestException) {
            return Problem(statusCode: StatusCodes.Status502BadGateway,
                title: "Unable to reach OpenLibrary."); // TODO: this was returned on a gemini request
        } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
            return Problem(statusCode: StatusCodes.Status504GatewayTimeout,
                title: "OpenLibrary did not respond in time.");
        }
    }
}