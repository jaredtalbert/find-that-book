namespace FindThatBook.Client.ViewModels;

// Presentation shape for a future search result card.
public sealed class BookCandidateViewModel : ViewModelBase {
    public string Title { get; init; } = string.Empty;
    public string Authors { get; init; } = string.Empty;
    public int? FirstPublishYear { get; init; }
    public string Explanation { get; init; } = string.Empty;
    public string? OpenLibraryUrl { get; init; }
    public string? CoverImageUrl { get; init; }
}
