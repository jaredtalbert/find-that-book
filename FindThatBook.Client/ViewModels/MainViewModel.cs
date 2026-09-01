using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FindThatBook.Client.ViewModels;

public partial class MainViewModel : ViewModelBase {
    [ObservableProperty]
    public partial string SearchQuery { get; set; } = string.Empty;

    // Ready for the server integration; intentionally empty in this UI-only pass.
    public ObservableCollection<BookCandidateViewModel> Candidates { get; } = [];
}
