using System.Text.Json;
using FindThatBook.Server.Models;
using FindThatBook.Server.Serialization;
using Google.GenAI;
using Google.GenAI.Types;

namespace FindThatBook.Server.Gemini;

// TODO: should this be static? 
public static class GeminiClient {

    private const string Model = "gemini-3.5-flash-lite";

    // todo: deep search (generate more candidates)
    private const string StarterPrompt = """
                                         A user wants to search for library books using any combination of:
                                         1) complete or incomplete author name
                                         2) complete or incomplete book title
                                         3) a brief sentence describing the plot
                                         4) a few keywords describing the contents of the book

                                         Generate a concise query string that can be passed into a simple API in the format
                                         `
                                         {
                                           "title": "on the origin of species",
                                           "author": null,
                                           "keywords": [],
                                           "year": 1859
                                         }
                                         `
                                         Do not include any further information, 
                                         clarifying requests, or comments.

                                         Attempt to infer the user's intention - for example, a user requesting "Dickens"
                                         most likely wants books by Charles Dickens, not the book entitled "Dickens" by Peter Ackroyd.

                                         For further example, a user query of "boy wizard school" should return
                                         {
                                           "title": "harry potter",
                                           "author": "jk rowling",
                                           "keywords": [],
                                           "year": null
                                         }

                                         User query: 
                                         """; // reduce token usage?

    private static Client Client { get; } = new Client();

    public async static Task<GeminiResponse> SimplifyUserQueryAsync(string query) {
        if (string.IsNullOrEmpty(query)) {
            throw new ArgumentNullException(nameof(query));
        }

        GenerateContentResponse response = await Client.Models.GenerateContentAsync(Model, StarterPrompt + query);

        string? simplifyUserQueryAsync = response.Candidates[0].Content.Parts[0].Text;

        GeminiResponse canonicalizedGeminiResponse = JsonSerializer.Deserialize<GeminiResponse>(
            simplifyUserQueryAsync,
            JsonDefaults.Options);

        Console.Out.WriteLine("Gemini Response: " + response);

        return canonicalizedGeminiResponse; // todo: error check
    }
}