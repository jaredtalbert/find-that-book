using Avalonia.Media.Imaging;

namespace FindThatBook.Client.Services;

public interface IBookCoverLoader {
    Task<Bitmap?> LoadAsync(
        Uri coverImageUri,
        CancellationToken cancellationToken = default);
}