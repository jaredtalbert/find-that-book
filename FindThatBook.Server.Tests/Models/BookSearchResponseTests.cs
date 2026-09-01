using System.Text.Json;
using FindThatBook.Server.Models;
using FindThatBook.Server.Serialization;
using Xunit;

namespace FindThatBook.Server.Tests.Models;

public class BookSearchResponseTests {
    [Fact]
    public void Serialize_UsesThePublicContractAndDoesNotExposeRankingScores() {
        BookSearchResponse response = new() {
            Results = [
                new BookSearchCandidate {
                    OpenLibraryKey = "OL1W",
                    Title = "Dune",
                    Authors = ["Frank Herbert"],
                    FirstPublishYear = 1965,
                    OpenLibraryUrl = "https://openlibrary.org/works/OL1W",
                    CoverImageUrl = "https://covers.openlibrary.org/b/id/123-M.jpg",
                    Confidence = SearchConfidence.Strong,
                    Explanation = "Exact title match."
                }
            ]
        };

        string json = JsonSerializer.Serialize(response, JsonDefaults.Options);

        Assert.Contains("\"results\"", json);
        Assert.Contains("\"openLibraryKey\":\"OL1W\"", json);
        Assert.Contains("\"confidence\":\"Strong\"", json);
        Assert.Contains("\"explanation\":\"Exact title match.\"", json);
        Assert.DoesNotContain("score", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("evidence", json, StringComparison.OrdinalIgnoreCase);
    }
}