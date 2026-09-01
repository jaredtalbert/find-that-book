using System.Text.Json.Serialization;
using FindThatBook.Client.Models;

namespace FindThatBook.Client.Serialization;

[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(BookSearchResponse))]
[JsonSerializable(typeof(ApiProblemResponse))]
partial class ClientJsonSerializerContext : JsonSerializerContext { }