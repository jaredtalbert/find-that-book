using System.Text.Json.Serialization;

namespace FindThatBook.Server.Models;

public struct Doc { // used to represent a book returned by openlibrary

    private const string DocKeyPrefix = "/works/"; // this is risky
    
    [JsonPropertyName("key")]
    public string Key { 
        get;
        set => field = value.StartsWith(DocKeyPrefix, StringComparison.Ordinal)
            ? value[DocKeyPrefix.Length..]
            : value;
    }
    
    [JsonPropertyName("title")]
    public string Title { get; set; }
    
    [JsonPropertyName("author_name")]
    public List<string> AuthorName { get; set; } // TODO [Future]: Author object
    
    [JsonPropertyName("author_key")]
    public List<string> AuthorKey { get; set; } // are these guaranteed to match to the correct index? may want to hit the author endpoint later to confirm
    
    [JsonPropertyName("cover_i")]
    public long CoverId { get; set; }
    
    [JsonPropertyName("first_publish_year")]
    public long FirstPublishYear { get; set; }
    
}

//     • Book title
//     • Primary author or authors, where available
//     • First publish year, where available
//     • Relevant Open Library identifiers or links
//     • Cover image, if readily available
//     • A concise explanation of why the result matched the query

/* sample response
{
       "cover_i": 258027,
       "has_fulltext": true,
       "edition_count": 120,
       "title": "The Lord of the Rings",
       "author_name": [
           "J. R. R. Tolkien"
       ],
       "first_publish_year": 1954,
       "key": "OL27448W",
       "ia": [
           "returnofking00tolk_1",
           "lordofrings00tolk_1",
           "lordofrings00tolk_0",
       ],
       "author_key": [
           "OL26320A"
       ],
       "public_scan_b": true
   }
   */