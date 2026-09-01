using Avalonia.Media.Imaging;

namespace FindThatBook.Client.Services.OpenLibrary;

public sealed class OpenLibraryCoverLoader(HttpClient httpClient) : IBookCoverLoader {
    private HttpClient HttpClient { get; } =
        httpClient ?? throw new ArgumentNullException(nameof(httpClient));

    public async Task<Bitmap?> LoadAsync(
        Uri coverImageUri,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(coverImageUri);

        try {
            using HttpResponseMessage response = await HttpClient.GetAsync(
                coverImageUri,
                cancellationToken);

            if (!response.IsSuccessStatusCode) {
                return null;
            }

            await using Stream imageStream = await response.Content.ReadAsStreamAsync(cancellationToken);

            return new Bitmap(imageStream);
        } catch (Exception exception) when (exception is
                                                HttpRequestException or
                                                IOException or
                                                ArgumentException or
                                                InvalidOperationException or
                                                NotSupportedException or
                                                OperationCanceledException) {
            // Covers are optional presentation data and must not fail a search.
            return null;
        }
    }
}