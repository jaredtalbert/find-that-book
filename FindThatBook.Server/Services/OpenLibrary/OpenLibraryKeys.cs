namespace FindThatBook.Server.Services.OpenLibrary;

internal static class OpenLibraryKeys {
    private const string WorkPrefix = "/works/";
    private const string AuthorPrefix = "/authors/";

    public static string Work(string? value) => StripPrefix(value, WorkPrefix);

    public static string Author(string? value) => StripPrefix(value, AuthorPrefix);

    private static string StripPrefix(string? value, string prefix) {
        string key = value?.Trim() ?? string.Empty;

        return key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? key[prefix.Length..]
            : key;
    }
}