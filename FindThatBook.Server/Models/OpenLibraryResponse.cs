using System.Text.Json.Serialization;

namespace FindThatBook.Server.Models;

public sealed class OpenLibraryResponse : IJsonOnDeserialized {
    [JsonPropertyName("start")] public int Start { get; set; }

    [JsonPropertyName("num_found")] public int NumFound { get; set; }

    [JsonPropertyName("docs")] public List<Doc> Docs { get; set; } = [];

    void IJsonOnDeserialized.OnDeserialized() {
        Docs ??= [];
    }
}