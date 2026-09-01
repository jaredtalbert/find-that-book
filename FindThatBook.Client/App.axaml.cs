using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using FindThatBook.Client.Services;
using FindThatBook.Client.Services.FindThatBook;
using FindThatBook.Client.Services.OpenLibrary;
using FindThatBook.Client.ViewModels;
using FindThatBook.Client.Views;

namespace FindThatBook.Client;

public partial class App : Application {
    public override void Initialize() {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted() {
        if (ApplicationLifetime is ISingleViewApplicationLifetime browser) {
            HttpClient httpClient = new() {
                BaseAddress = ApiEndpoint.GetBaseAddress(),
                Timeout = TimeSpan.FromSeconds(10)
            };

            IApiConnectionService apiConnection =
                new FindThatBookApiConnectionService(httpClient);

            HttpClient coverHttpClient = new() {
                Timeout = TimeSpan.FromSeconds(5)
            };

            IBookCoverLoader bookCoverLoader = new OpenLibraryCoverLoader(coverHttpClient);

            browser.MainView = new MainWindow {
                DataContext = new MainViewModel(apiConnection, bookCoverLoader),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}