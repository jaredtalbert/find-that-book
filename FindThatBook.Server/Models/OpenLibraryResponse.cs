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