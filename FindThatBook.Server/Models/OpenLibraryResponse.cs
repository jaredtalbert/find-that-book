using System.Text.Json.Serialization;

namespace FindThatBook.Server.Models;

public struct OpenLibraryResponse {
    [JsonPropertyName("start")]
    public int Start { get; set; }
    
    [JsonPropertyName("num_found")]
    public int NumFound { get; set; }
    
    [JsonPropertyName("docs")]
    public List<Doc> Docs { get; set; }
}

//     • Book title
//     • Primary author or authors, where available
//     • First publish year, where available
//     • Relevant Open Library identifiers or links
//     • Cover image, if readily available
//     • A concise explanation of why the result matched the query

/* sample response
   {
       "start": 0,
       "num_found": 629,
       "docs": [
           {...},
           {...},
           ...
           {...}]
   }

*/