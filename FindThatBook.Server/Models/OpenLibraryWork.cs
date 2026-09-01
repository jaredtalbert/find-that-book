using System.Text.Json.Serialization;
using FindThatBook.Server.Services.OpenLibrary;

namespace FindThatBook.Server.Models;

/// <summary>
/// Canonical metadata returned by an Open Library work endpoint.
/// Author order is preserved because the first entry is used as the primary author.
/// </summary>
public sealed class OpenLibraryWork : IJsonOnDeserialized {
    [JsonPropertyName("key")] public string Key { get; set; } = string.Empty;

    [JsonPropertyName("title")] public string Title { get; set; } = string.Empty;

    [JsonPropertyName("authors")] public List<OpenLibraryWorkAuthor> Authors { get; set; } = [];

    [JsonPropertyName("subjects")] public List<string> Subjects { get; set; } = [];

    void IJsonOnDeserialized.OnDeserialized() {
        Key = OpenLibraryKeys.Work(Key);
        Title ??= string.Empty;
        Authors ??= [];
        Subjects ??= [];
    }
}

public sealed class OpenLibraryWorkAuthor : IJsonOnDeserialized {
    [JsonPropertyName("author")] public OpenLibraryAuthorReference Author { get; set; } = new();

    void IJsonOnDeserialized.OnDeserialized() {
        Author ??= new OpenLibraryAuthorReference();
    }
}

public sealed class OpenLibraryAuthorReference : IJsonOnDeserialized {
    [JsonPropertyName("key")] public string Key { get; set; } = string.Empty;

    void IJsonOnDeserialized.OnDeserialized() {
        Key = OpenLibraryKeys.Author(Key);
    }
}