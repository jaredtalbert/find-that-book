# Find That Book

**AI Disclosure**
- Codex was used for:
  - Client UI
  - Normalizer
  - Ranking pipeline

## Live Deployment:
https://find-that-book-s131.onrender.com
(may take a minute or so to start up since it goes to sleep after a few minutes of inactivity)

## Setup and Run

Requirements: .NET 10 SDK and a Gemini API key.

Set `GEMINI_API_KEY` in your local environment or IDE run configuration.

```bash
dotnet restore "Find That Book.sln"
dotnet run --project FindThatBook.Server --launch-profile http
dotnet run --project FindThatBook.Client --launch-profile "FindThatBook.Client: Browser"
```

The development URLs are `http://localhost:5287` for the API and `http://localhost:5235` for the browser client. The
search endpoint is `GET /Search?q={query}`.

### Limitations

- The API is tightly coupled to OpenLibrary and Gemini. Future integrations will require a decent refactor.
- Caching is not implemented, so repeated requests for the same query still go through the entire response pipeline.
- Occasionally Gemini will hallucinate values, leading to false reasoning. For example, searching "Harry Potter" may
  show some works with "Matches remembered details: magic, fantasy".

## Approach

- Gemini attempts to normalize arbitrary "messy" input into a stable `GeminiResponse` object
- If Gemini is not available or otherwise returns an invalid response, we fall back to searching the API using the raw
  query.
- The initial pool of results ("candidates") from OpenLibrary is passed into the response pipeline which:
  - Normalizes the title and author
  - Removes duplicates
  - Scores each candidate (details below)
  - Returns <=5 strongest candidates.

## Testing

Run the focused normalization, ranking, Gemini fallback, OpenLibrary adapter, orchestration, serialization, and
controller tests with:

```bash
dotnet test "Find That Book.sln"
```

## Next Steps

- Consider caching, rate limiting, retries, and scaling.
