using Microsoft.Web.WebView2.Core;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DevelopmentHub.App;

public partial class MainWindow : Window
{
    private static readonly bool _isDev =
        Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
    private static readonly string AppUrl =
        _isDev ? "http://localhost:5173" : "http://localhost:6131";
    private const int HotkeyId = 1;
    private const uint MOD_CTRL = 0x0002, MOD_SHIFT = 0x0004, VK_D = 0x44;

    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public Action? RequestExit { get; set; }

    private string _hotkeyBinding = "Ctrl+Shift+D";
    private bool _hotkeyInitialized = false;

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) =>
        {
            _hotkeyInitialized = true;
            ApplyHotkeyRegistration();
            HwndSource.FromHwnd(new WindowInteropHelper(this).Handle).AddHook(WndProc);
        };
    }

    public void UpdateHotkey(string binding)
    {
        _hotkeyBinding = binding;
        if (_hotkeyInitialized)
            ApplyHotkeyRegistration();
    }

    private void ApplyHotkeyRegistration()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        UnregisterHotKey(hwnd, HotkeyId);
        var (mods, vk) = ParseHotkey(_hotkeyBinding);
        if (vk != 0) RegisterHotKey(hwnd, HotkeyId, mods, vk);
    }

    private static (uint Mods, uint Vk) ParseHotkey(string binding)
    {
        uint mods = 0, vk = 0;
        foreach (var raw in binding.Split('+'))
        {
            switch (raw.Trim().ToLowerInvariant())
            {
                case "ctrl":  mods |= 0x0002; break;
                case "shift": mods |= 0x0004; break;
                case "alt":   mods |= 0x0001; break;
                case "win":   mods |= 0x0008; break;
                default:
                    var part = raw.Trim();
                    if (part.Length == 1)
                        vk = (uint)char.ToUpperInvariant(part[0]);
                    else if (part.StartsWith("F", StringComparison.OrdinalIgnoreCase) &&
                             int.TryParse(part[1..], out var fNum) && fNum is >= 1 and <= 12)
                        vk = (uint)(0x6F + fNum); // VK_F1=0x70
                    break;
            }
        }
        return (mods, vk);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == 0x0312 && wParam.ToInt32() == HotkeyId) // WM_HOTKEY
        {
            Show();
            Activate();
        }
        return IntPtr.Zero;
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

        WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = _isDev;
        WebView.CoreWebView2.Settings.AreDevToolsEnabled = _isDev;

        // Open all external links (e.g. PR URLs) in the default system browser
        WebView.CoreWebView2.NewWindowRequested += (_, e) =>
        {
            e.Handled = true;
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri)
            {
                UseShellExecute = true
            });
        };


        if (!_isDev)
        {
            // Show a loading page while Kestrel is starting up
            WebView.NavigateToString("<html><body style='font-family:sans-serif;display:flex;align-items:center;justify-content:center;height:100vh;margin:0;background:#1e1e2e;color:#cdd6f4'><p>Starting...</p></body></html>");

            // Wait until the backend is ready (up to 30 seconds)
            await WaitForBackendAsync();
        }

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
