# Find That Book

**AI Disclosure**
- Codex was used for:
  - Client UI
  - Normalizer
  - Ranking pipeline
  - Helping write this readme

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

## Approach

- Gemini attempts to normalize arbitrary "messy" input into a stable `GeminiResponse` object
  - Using an LLM for this task is significantly more straightforward than attempting to do it deterministically, at least as long as a "type anything" input field exists.
- If Gemini is not available or otherwise returns an invalid response, we fall back to searching the API using the raw
  query.
- The initial pool of results ("candidates") from OpenLibrary is passed into the response pipeline which:
  - Normalizes the title and author
    - `TextNormalizer.cs`
  - Removes duplicates
    - `BookSearchService.cs:Deduplicate()`
  - Scores each candidate (details below)
  - Returns <=5 strongest candidates.

### Scoring Logic

- Ranking runs twice: first against the deduplicated search results to select up to five candidates for enrichment, then
  again using canonical OpenLibrary titles, authors, subjects, and edition counts.

| Evidence | Score | Matching logic |
| --- | ---: | --- |
| Title | 0-60 | Exact matches score highest; token overlap and Damerau-Levenshtein fuzzy matches receive partial credit. |
| Author | -50-35 | Exact names score highest, followed by initials/surname, token overlap, and fuzzy surname matches. |
| Keywords | 0-10 | Title or subject matches add 2 points, or 3 for distinctive keywords. |
| Year | 0-5 | Exact year: 5; within one year: 3; within three years: 1. |

- A confirmed canonical-author mismatch applies a -50 penalty. Gemini-inferred evidence receives 70% of its normal
  score, and fully inferred queries require matches in at least two categories and a total score of at least 35.
- Raw score alone does not make a result eligible: title and author queries require a meaningful match, while
  keyword-only queries require two matches or one distinctive keyword. Fallback queries may qualify through meaningful
  title, author, or keyword evidence.
- Candidates are ordered by:
  - total score, then 
  - evidence strength, 
  - primary-author match, 
  - component scores, 
  - exact year,
  - edition count, and 
  - OpenLibrary key 
  to produce stable ties. Only relevant candidates are returned.

### Limitations

- Caching is not implemented, so repeated requests for the same query still go through the entire response pipeline.
- Occasionally Gemini will hallucinate values, leading to false reasoning. For example, searching "Harry Potter" may
  show some works with "Matches remembered details: magic, fantasy".
- The deduplication logic needs improvement; OpenLibrary may return low-quality data that cannot be reliably determined as a duplicate with our current setup.

## Testing

Run the focused normalization, ranking, Gemini fallback, OpenLibrary adapter, orchestration, serialization, and
controller tests with:

```bash
dotnet test "Find That Book.sln"
```

## Completed Extras
- `More sophisticated normalization for punctuation, diacritics, subtitles, aliases, or misspellings`
- `Advanced contributor-vs-primary-author resolution using additional Open Library endpoints`
- `A deployed demo or hosted version of the project`

## Deliberate omissions
- `LLM-based re-ranking after deterministic candidate retrieval`
  - Reversing the order on this reduces latency
- `Caching, retries, resilience policies, observability, or other production-oriented API concerns`
  - Time constraints
- `A richer UI, accessibility refinements, loading states, or thoughtful interaction design beyond the basic
  workflow`
  - The UI is generic AI boilerplate design.
- `Additional automated test coverage, contract tests, or test doubles around external dependencies`
  - We could include integration tests to refine the Gemini prompt to mitigate hallucinations or otherwise low-quality responses
  
## Next Steps

- Consider caching, rate limiting, retries, and scaling.
