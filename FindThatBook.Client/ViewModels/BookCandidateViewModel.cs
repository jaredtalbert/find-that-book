using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using FindThatBook.Client.Services;

namespace FindThatBook.Client.ViewModels;

public sealed partial class BookCandidateViewModel(IBookCoverLoader bookCoverLoader)
    : ViewModelBase, IDisposable {
    private IBookCoverLoader BookCoverLoader { get; } =
        bookCoverLoader ?? throw new ArgumentNullException(nameof(bookCoverLoader));

    public string Title { get; init; } = string.Empty;

    public string Authors { get; init; } = string.Empty;

    public string FirstPublishYear { get; init; } = string.Empty;

    public string Confidence { get; init; } = string.Empty;

    public string Explanation { get; init; } = string.Empty;

    public Uri? OpenLibraryUri { get; init; }

    public bool HasOpenLibraryUri => OpenLibraryUri is not null;

    public string? CoverImageUrl { get; init; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCoverImage))]
    [NotifyPropertyChangedFor(nameof(ShowCoverPlaceholder))]
    public partial Bitmap? CoverImage { get; private set; }

    public bool HasCoverImage => CoverImage is not null;

    public bool ShowCoverPlaceholder => !HasCoverImage;

    internal async Task LoadCoverAsync(CancellationToken cancellationToken) {
        if (!Uri.TryCreate(CoverImageUrl, UriKind.Absolute, out Uri? coverImageUri)) {
            return;
        }

        CoverImage = await BookCoverLoader.LoadAsync(coverImageUri, cancellationToken);
    }

    public void Dispose() {
        CoverImage?.Dispose();
        CoverImage = null;
    }
}