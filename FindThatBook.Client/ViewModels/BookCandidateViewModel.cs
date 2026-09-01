namespace FindThatBook.Client.ViewModels;

public sealed class BookCandidateViewModel : ViewModelBase {
    public string Title { get; init; } = string.Empty;

    public string Authors { get; init; } = string.Empty;

    public string FirstPublishYear { get; init; } = string.Empty;

    public string Confidence { get; init; } = string.Empty;

    public string Explanation { get; init; } = string.Empty;

    public string? OpenLibraryUrl { get; init; }

    public string? CoverImageUrl { get; init; }
}