using System.Threading.Tasks;
using Avalonia;
using Avalonia.Browser;

namespace FindThatBook.Client;

sealed class Program {
    private static Task Main(string[] args) => BuildAvaloniaApp()
        .StartBrowserAppAsync("out", new BrowserPlatformOptions {
            // Brave can block the GPU-identification extension used during WebGL startup.
            RenderingMode = [BrowserRenderingMode.Software2D]
        });

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .WithInterFont()
            .LogToTrace();
}
