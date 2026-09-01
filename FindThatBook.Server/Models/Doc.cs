using System.Text.Json.Serialization;
using FindThatBook.Server.Services.OpenLibrary;

namespace FindThatBook.Server.Models;

/// <summary>
/// A work returned by the Open Library search endpoint.
/// </summary>
public sealed class Doc : IJsonOnDeserialized {
    [JsonPropertyName("key")] public string Key { get; set; } = string.Empty;

    [JsonPropertyName("title")] public string Title { get; set; } = string.Empty;

    [JsonPropertyName("author_name")] public List<string> AuthorName { get; set; } = [];

    [JsonPropertyName("author_key")] public List<string> AuthorKey { get; set; } = [];

    [JsonPropertyName("cover_i")] public long? CoverId { get; set; }

    [JsonPropertyName("first_publish_year")] public long? FirstPublishYear { get; set; }

    [JsonPropertyName("edition_count")] public long? EditionCount { get; set; }

    [JsonPropertyName("subject")] public List<string> Subjects { get; set; } = [];

    void IJsonOnDeserialized.OnDeserialized() {
        Key = OpenLibraryKeys.Work(Key);
        Title ??= string.Empty;
        AuthorName ??= [];
        AuthorKey = (AuthorKey ?? []).Select(OpenLibraryKeys.Author).ToList();
        Subjects ??= [];
    }
}