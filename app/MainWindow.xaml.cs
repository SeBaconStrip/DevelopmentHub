using Microsoft.Web.WebView2.Core;
using System.IO;
using System.Net.Http;
using System.Windows;

namespace DevelopmentHub.App;

public partial class MainWindow : Window
{
    private const string AppUrl = "http://localhost:6131";

    public MainWindow()
    {
        InitializeComponent();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Set window icon from exe directory
        var iconPath = Path.Combine(AppContext.BaseDirectory, "DeveloperHubIcon.ico");
        if (File.Exists(iconPath))
            Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri(iconPath));

        var userDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DevelopmentHub", "WebView2");

        var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
        await WebView.EnsureCoreWebView2Async(env);

        WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
        WebView.CoreWebView2.Settings.AreDevToolsEnabled = false;

        // Show a loading page while Kestrel is starting up
        WebView.NavigateToString("<html><body style='font-family:sans-serif;display:flex;align-items:center;justify-content:center;height:100vh;margin:0;background:#1e1e2e;color:#cdd6f4'><p>Starting...</p></body></html>");

        // Wait until the backend is ready (up to 30 seconds)
        await WaitForBackendAsync();
        WebView.Source = new Uri(AppUrl);
    }

    private static async Task WaitForBackendAsync()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        for (int i = 0; i < 15; i++)
        {
            try
            {
                var response = await http.GetAsync(AppUrl);
                if (response.IsSuccessStatusCode || (int)response.StatusCode < 500)
                    return;
            }
            catch { /* not ready yet */ }

            await Task.Delay(2000);
        }
    }
}
