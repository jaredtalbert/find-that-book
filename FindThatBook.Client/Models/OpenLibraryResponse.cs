using System.Collections.Generic;

namespace FindThatBook.Client.Models;

public struct OpenLibraryResponse {
    private long Start;
    public long NumFound;
    public List<Doc> Docs;
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