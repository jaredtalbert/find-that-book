using CommunityToolkit.Mvvm.ComponentModel;

namespace Find_That_Book.ViewModels;

public partial class MainViewModel : ViewModelBase {
    [ObservableProperty] public partial string Greeting { get; set; } = "Welcome to Avalonia!";
}