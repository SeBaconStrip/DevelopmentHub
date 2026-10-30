using Microsoft.Web.WebView2.Core;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace DevelopmentHub.App;

public partial class MainWindow : Window
{
    private static readonly bool _isDev =
        Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
    private static readonly string AppUrl =
        _isDev ? "http://localhost:5173" : "http://localhost:7631";
    private const int HotkeyId = 1;
    private const uint MOD_CTRL = 0x0002, MOD_SHIFT = 0x0004, VK_D = 0x44;

    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] static extern bool ReleaseCapture();
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT pt);

    [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);
    [DllImport("user32.dll")] static extern IntPtr MonitorFromRect(ref RECT lprc, uint dwFlags);
    [DllImport("user32.dll")] static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);
    [DllImport("user32.dll")] static extern bool IsZoomed(IntPtr hWnd);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved, ptMaxSize, ptMaxPosition, ptMinTrackSize, ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor, rcWork;
        public uint dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WINDOWPOS
    {
        public IntPtr hwnd, hwndInsertAfter;
        public int x, y, cx, cy;
        public uint flags;
    }

    private const int WM_GETMINMAXINFO = 0x0024;
    private const int WM_WINDOWPOSCHANGING = 0x0046;
    private const uint SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002;
    private const uint MONITOR_DEFAULTTONEAREST = 2;
    private const int WM_NCLBUTTONDOWN = 0xA1;
    private const int HTCAPTION = 2;

    public Action? RequestExit { get; set; }

    /// <summary>Per-process secret injected into every WebView document as <c>window.__devHubToken</c>.</summary>
    public string? ApiToken { get; set; }

    private string _hotkeyBinding = "Ctrl+Shift+D";
    private bool _hotkeyInitialized = false;
    private WindowState _lastNonMinimizedWindowState = WindowState.Maximized;
    private const int SW_RESTORE = 9;
    private const int SW_SHOWMAXIMIZED = 3;

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) =>
        {
            _hotkeyInitialized = true;
            ApplyHotkeyRegistration();
            HwndSource.FromHwnd(new WindowInteropHelper(this).Handle).AddHook(WndProc);
        };
        StateChanged += (_, _) =>
        {
            if (WindowState != WindowState.Minimized)
                _lastNonMinimizedWindowState = WindowState;
            PushWindowState();
        };
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        var msg = e.TryGetWebMessageAsString();
        Dispatcher.Invoke(() =>
        {
            switch (msg)
            {
                case "minimize": WindowState = WindowState.Minimized; break;
                case "maximize": WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; break;
                case "close":
                    Close(); // Closing event in App.xaml.cs handles hide + balloon tip
                    break;
                case "drag":
                    var hwnd = new WindowInteropHelper(this).Handle;
                    if (WindowState == WindowState.Maximized)
                    {
                        // Restore and position so cursor stays at proportional X offset
                        GetCursorPos(out var pt);
                        var restored = RestoreBounds;
                        double ratio = SystemParameters.PrimaryScreenWidth > 0
                            ? (double)pt.X / SystemParameters.PrimaryScreenWidth
                            : 0.5;
                        WindowState = WindowState.Normal;
                        Left = Math.Max(0, pt.X - restored.Width * ratio);
                        Top = Math.Max(0, pt.Y - 20);
                    }
                    ReleaseCapture();
                    SendMessage(hwnd, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
                    break;
            }
        });
    }

    private void PushWindowState()
    {
        if (WebView.CoreWebView2 is null) return;
        var maximized = WindowState == WindowState.Maximized;
        var js = $"window.__windowState={{maximized:{(maximized ? "true" : "false")}}};window.dispatchEvent(new CustomEvent('windowstate',{{detail:{{maximized:{(maximized ? "true" : "false")}}}}}));";
        WebView.CoreWebView2.ExecuteScriptAsync(js);
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
            ToggleFromHotkey();
            handled = true;
        }
        else if (msg == WM_GETMINMAXINFO)
        {
            LimitMaximizedBoundsToWorkArea(hwnd, lParam);
        }
        else if (msg == WM_WINDOWPOSCHANGING)
        {
            ClampMaximizedPositionToWorkArea(hwnd, lParam);
        }
        return IntPtr.Zero;
    }

    // Windows scales ptMaxSize by the size difference to the primary monitor when it covers the
    // primary monitor, so on a larger secondary monitor the maximized window would overshoot again.
    // Clamp the final maximized rect to the work area of the monitor it lands on.
    private static void ClampMaximizedPositionToWorkArea(IntPtr hwnd, IntPtr lParam)
    {
        if (!IsZoomed(hwnd)) return;

        var pos = Marshal.PtrToStructure<WINDOWPOS>(lParam);
        if ((pos.flags & (SWP_NOSIZE | SWP_NOMOVE)) != 0) return;

        var target = new RECT { Left = pos.x, Top = pos.y, Right = pos.x + pos.cx, Bottom = pos.y + pos.cy };
        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(MonitorFromRect(ref target, MONITOR_DEFAULTTONEAREST), ref info)) return;

        pos.x = info.rcWork.Left;
        pos.y = info.rcWork.Top;
        pos.cx = info.rcWork.Right - info.rcWork.Left;
        pos.cy = info.rcWork.Bottom - info.rcWork.Top;
        Marshal.StructureToPtr(pos, lParam, false);
    }

    // A borderless window maximizes to the full monitor plus its resize border, so Windows
    // treats it as fullscreen and drops the taskbar below other windows. Clamp the maximized
    // bounds to the work area of the monitor the window is on.
    private static void LimitMaximizedBoundsToWorkArea(IntPtr hwnd, IntPtr lParam)
    {
        var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        if (monitor == IntPtr.Zero) return;

        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(monitor, ref info)) return;

        var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
        mmi.ptMaxPosition.X = info.rcWork.Left - info.rcMonitor.Left;
        mmi.ptMaxPosition.Y = info.rcWork.Top - info.rcMonitor.Top;
        mmi.ptMaxSize.X = info.rcWork.Right - info.rcWork.Left;
        mmi.ptMaxSize.Y = info.rcWork.Bottom - info.rcWork.Top;
        Marshal.StructureToPtr(mmi, lParam, false);
    }

    public void ToggleFromHotkey()
    {
        if (IsEffectivelyForeground())
        {
            if (WindowState != WindowState.Minimized)
                _lastNonMinimizedWindowState = WindowState;
            Hide();
            return;
        }

        BringToForeground();
    }

    public void BringToForeground()
    {
        if (!IsVisible)
            Show();

        var hwnd = new WindowInteropHelper(this).Handle;
        if (_lastNonMinimizedWindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Maximized;
            ShowWindow(hwnd, SW_SHOWMAXIMIZED);
        }
        else
        {
            ShowWindow(hwnd, SW_RESTORE);
            WindowState = _lastNonMinimizedWindowState;
        }

        Dispatcher.BeginInvoke(() =>
        {
            if (_lastNonMinimizedWindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Maximized;
                ShowWindow(hwnd, SW_SHOWMAXIMIZED);
            }
            else
            {
                WindowState = _lastNonMinimizedWindowState;
            }

            Activate();
            SetForegroundWindow(hwnd);
        }, DispatcherPriority.ApplicationIdle);
        Activate();
        SetForegroundWindow(hwnd);
        Focus();
    }

    private bool IsEffectivelyForeground()
    {
        if (!IsVisible || WindowState == WindowState.Minimized)
            return false;

        var hwnd = new WindowInteropHelper(this).Handle;
        return hwnd != IntPtr.Zero && GetForegroundWindow() == hwnd;
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
        await ClearCacheAfterUpdateAsync(userDataFolder);
        WebView.ZoomFactor = 1.0;

        WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = _isDev;
        WebView.CoreWebView2.Settings.AreDevToolsEnabled = true; // F12 öffnet DevTools auch in Production

        WebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

        // F12 öffnet DevTools auch in der Production-Version
        WebView.KeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.F12)
                WebView.CoreWebView2.OpenDevToolsWindow();
        };

        // Inject the per-process API token into every document before any scripts run.
        // The React app reads window.__devHubToken and includes it in all API requests.
        if (ApiToken is not null)
            await WebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(
                $"window.__devHubToken = '{ApiToken}';");

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

    // The WebView cache of the previous version can keep serving its index.html and bundles, so the
    // UI stays on the old version after an update. Clear it once whenever the app version changes.
    private async Task ClearCacheAfterUpdateAsync(string userDataFolder)
    {
        var version = typeof(MainWindow).Assembly.GetName().Version?.ToString() ?? string.Empty;
        var markerPath = Path.Combine(userDataFolder, "app-version.txt");
        try
        {
            if (File.Exists(markerPath) && File.ReadAllText(markerPath) == version)
                return;

            await WebView.CoreWebView2.Profile.ClearBrowsingDataAsync(
                CoreWebView2BrowsingDataKinds.DiskCache | CoreWebView2BrowsingDataKinds.CacheStorage);
            File.WriteAllText(markerPath, version);
        }
        catch { /* best-effort: a stale cache only delays the new UI */ }
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
