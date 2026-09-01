using System.Text.Json;

namespace FindThatBook.Server.Serialization;

public static class JsonDefaults {
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web) {
        PropertyNameCaseInsensitive = true
    };
}