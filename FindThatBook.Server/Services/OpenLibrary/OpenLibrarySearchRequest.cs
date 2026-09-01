using System.Globalization;

namespace FindThatBook.Server.Services.OpenLibrary;

public sealed record OpenLibrarySearchRequest(
    string? RawQuery = null,
    string? Title = null,
    string? Author = null,
    int Limit = 25) {
    public const int MaximumLimit = 100;

    internal string BuildRelativeUri() {
        if (Limit is < 1 or > MaximumLimit) {
            throw new ArgumentOutOfRangeException(nameof(Limit), Limit,
                $"The result limit must be between 1 and {MaximumLimit}.");
        }

        bool hasStructuredQuery = !string.IsNullOrWhiteSpace(Title) || !string.IsNullOrWhiteSpace(Author);

        if (!hasStructuredQuery && string.IsNullOrWhiteSpace(RawQuery)) {
            throw new ArgumentException("A raw query, title, or author is required.");
        }

        List<KeyValuePair<string, string>> parameters = [];

        if (hasStructuredQuery) {
            AddIfPresent(parameters, "title", Title);
            AddIfPresent(parameters, "author", Author);
        }
        else {
            AddIfPresent(parameters, "q", RawQuery);
        }

        // The requested year remains ranking evidence. Sending it here would turn
        // a small preference into a hard discovery filter and discard works whose
        // indexed publication year is missing or differs slightly.

        parameters.Add(new KeyValuePair<string, string>("limit", Limit.ToString(CultureInfo.InvariantCulture)));

        parameters.Add(new KeyValuePair<string, string>("fields",
            "key,title,author_name,author_key,first_publish_year,edition_count,cover_i,subject"));

        string queryString = string.Join('&', parameters.Select(parameter =>
            $"{Uri.EscapeDataString(parameter.Key)}={Uri.EscapeDataString(parameter.Value)}"));

        return $"search.json?{queryString}";
    }

    private static void AddIfPresent(
        ICollection<KeyValuePair<string, string>> parameters,
        string name,
        string? value) {
        if (!string.IsNullOrWhiteSpace(value)) {
            parameters.Add(new KeyValuePair<string, string>(name, value.Trim()));
        }
    }
}