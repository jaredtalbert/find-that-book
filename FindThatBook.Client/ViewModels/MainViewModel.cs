using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FindThatBook.Client.Models;
using FindThatBook.Client.Services;

namespace FindThatBook.Client.ViewModels;

public partial class MainViewModel(IApiConnectionService apiConnectionService) : ViewModelBase {
    private IApiConnectionService ApiConnectionService { get; } =
        apiConnectionService ?? throw new ArgumentNullException(nameof(apiConnectionService));

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanSearch))] public partial string SearchQuery { get; set; } =
        string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowInitialState))]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    [NotifyPropertyChangedFor(nameof(ShowResults))]
    public partial bool HasSearched { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSearch))]
    [NotifyPropertyChangedFor(nameof(ShowInitialState))]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    [NotifyPropertyChangedFor(nameof(ShowResults))]
    public partial bool IsSearching { get; private set; }

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasError))] [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    public partial string ErrorMessage { get; private set; } = string.Empty;

    public ObservableCollection<BookCandidateViewModel> Candidates { get; } = [];

    public bool CanSearch => !IsSearching && !string.IsNullOrWhiteSpace(SearchQuery);

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool ShowInitialState => !HasSearched && !IsSearching;

    public bool ShowEmptyState => HasSearched && !IsSearching && Candidates.Count == 0 && !HasError;

    public bool ShowResults => HasSearched && !IsSearching && Candidates.Count > 0;

    public string ResultCountText => Candidates.Count == 1
        ? "1 RESULT"
        : $"{Candidates.Count} RESULTS";

    partial void OnSearchQueryChanged(string value) {
        SearchCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsSearchingChanged(bool value) {
        SearchCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanSearch))]
    private async Task SearchAsync(CancellationToken cancellationToken) {
        IsSearching = true;
        ErrorMessage = string.Empty;

        try {
            BookSearchResponse response = await ApiConnectionService.SearchAsync(
                SearchQuery,
                cancellationToken);

            Candidates.Clear();

            foreach (BookSearchCandidate candidate in response.Results) {
                Candidates.Add(CreateCandidateViewModel(candidate));
            }

            HasSearched = true;
            NotifyResultStateChanged();
        } catch (ApiConnectionException exception) {
            ShowError(exception.Message);
        } catch (HttpRequestException) {
            ShowError("The server could not be reached. Please try again.");
        } catch (JsonException) {
            ShowError("The server returned an invalid response. Please try again.");
        } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
            ShowError("The search timed out. Please try again.");
        }
        finally {
            IsSearching = false;
        }
    }

    private static BookCandidateViewModel CreateCandidateViewModel(BookSearchCandidate candidate) {
        return new BookCandidateViewModel {
            Title = candidate.Title,
            Authors = candidate.Authors.Count == 0
                ? "Unknown author"
                : string.Join(", ", candidate.Authors),
            FirstPublishYear = candidate.FirstPublishYear?.ToString() ?? "Year unknown",
            Confidence = candidate.Confidence.ToString(),
            Explanation = candidate.Explanation,
            OpenLibraryUrl = candidate.OpenLibraryUrl,
            CoverImageUrl = candidate.CoverImageUrl
        };
    }

    private void NotifyResultStateChanged() {
        OnPropertyChanged(nameof(ResultCountText));
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(ShowResults));
    }

    private void ShowError(string message) {
        Candidates.Clear();
        HasSearched = true;
        ErrorMessage = message;
        NotifyResultStateChanged();
    }
}