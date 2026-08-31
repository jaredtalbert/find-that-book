using Microsoft.AspNetCore.Mvc;

namespace FindThatBook.Server.Controllers;

[ApiController]
[Route("[controller]")]
public class SearchController {
    
    // will probably 

    [HttpGet("/")]
    public async Task<IActionResult> SearchAsync(CancellationToken cancellationToken) {

        return new OkResult();
    }
}