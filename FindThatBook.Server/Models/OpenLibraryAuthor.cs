using System.Text.Json.Serialization;
using FindThatBook.Server.Services.OpenLibrary;

namespace FindThatBook.Server.Models;

// TODO: Generic author?
public sealed class OpenLibraryAuthor : IJsonOnDeserialized {
    [JsonPropertyName("key")] public string Key { get; set; } = string.Empty;

    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;

    [JsonPropertyName("alternate_names")] public List<string> AlternateNames { get; set; } = [];

    void IJsonOnDeserialized.OnDeserialized() {
        Key = OpenLibraryKeys.Author(Key);
        Name ??= string.Empty;
        AlternateNames ??= [];
    }
}