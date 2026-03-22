using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Windows;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using DevelopmentHub.Api.Services;

namespace DevelopmentHub.App;

public partial class App : Application
{
    private WebApplication? _host;
    private System.Windows.Forms.NotifyIcon? _trayIcon;
    private string _currentHotkey = "Ctrl+Shift+D";

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Keep app alive when all windows are hidden
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        DispatcherUnhandledException += (_, ex) =>
        {
            MessageBox.Show(ex.Exception.ToString(), "DevelopmentHub – Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            ex.Handled = true;
        };

        try
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")))
                Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production");

            _host = DevelopmentHub.Api.BackendHost.Create([]);

            // Wire native folder picker so the frontend can call /api/folder-picker
            DevelopmentHub.Api.Services.FolderPickerBridge.Picker = () =>
            {
                var tcs = new TaskCompletionSource<string?>();
                Dispatcher.Invoke(() =>
                {
                    using var dlg = new System.Windows.Forms.FolderBrowserDialog
                    {
                        Description = "Select repository root directory",
                        UseDescriptionForTitle = true,
                        ShowNewFolderButton = false
                    };
                    tcs.SetResult(
                        dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK
                            ? dlg.SelectedPath
                            : null);
                });
                return tcs.Task;
            };

            // Wire native file picker so the frontend can call /api/file-picker
            DevelopmentHub.Api.Services.FilePickerBridge.Picker = (filter) =>
            {
                var tcs = new TaskCompletionSource<string?>();
                Dispatcher.Invoke(() =>
                {
                    using var dlg = new System.Windows.Forms.OpenFileDialog
                    {
                        Filter = filter,
                        CheckFileExists = true,
                    };
                    tcs.SetResult(
                        dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK
                            ? dlg.FileName
                            : null);
                });
                return tcs.Task;
            };

            var window = new MainWindow();

            // Start Kestrel on a background thread so it never blocks the UI thread
            _ = Task.Run(async () =>
            {
                try
                {
                    await _host.StartAsync();

                    // Load saved hotkey from config and apply it
                    var configSvc = _host.Services.GetRequiredService<IUserConfigService>();
                    var cfg = await configSvc.GetAsync();
                    Dispatcher.Invoke(() =>
                    {
                        _currentHotkey = cfg.HotkeyBinding;
                        window.UpdateHotkey(cfg.HotkeyBinding);
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                        MessageBox.Show(ex.ToString(), "DevelopmentHub – Backend-Fehler", MessageBoxButton.OK, MessageBoxImage.Error));
                }
            });

            // When hotkey changes via settings, re-register it live
            HotkeyChangedNotifier.HotkeyChanged += binding => Dispatcher.Invoke(() =>
            {
                _currentHotkey = binding;
                window.UpdateHotkey(binding);
            });

            // Hide to tray instead of closing
            var firstHide = true;
            window.Closing += (_, args) =>
            {
                args.Cancel = true;
                window.Hide();
                if (firstHide)
                {
                    firstHide = false;
                    _trayIcon!.BalloonTipIcon = System.Windows.Forms.ToolTipIcon.Info;
                    _trayIcon.BalloonTipTitle = "DevelopmentHub";
                    _trayIcon.BalloonTipText = $"Läuft im Hintergrund. {_currentHotkey} drücken zum Öffnen.";
                    _trayIcon.ShowBalloonTip(5000);
                }
            };

            // Tray icon
            var iconPath = Path.Combine(AppContext.BaseDirectory, "DeveloperHubIcon.ico");
            _trayIcon = new System.Windows.Forms.NotifyIcon
            {
                Icon = File.Exists(iconPath)
                    ? new System.Drawing.Icon(iconPath)
                    : System.Drawing.SystemIcons.Application,
                Visible = true,
                Text = "DevelopmentHub"
            };

            var menu = new System.Windows.Forms.ContextMenuStrip();
            menu.Items.Add("Öffnen", null, (_, _) => Dispatcher.Invoke(window.BringToForeground));
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            menu.Items.Add("Beenden", null, (_, _) => Dispatcher.Invoke(ExitApp));
            _trayIcon.ContextMenuStrip = menu;
            _trayIcon.DoubleClick += (_, _) => Dispatcher.Invoke(window.BringToForeground);

            window.RequestExit = ExitApp;
            window.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString(), "DevelopmentHub – Startfehler", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void ExitApp()
    {
        _trayIcon?.Dispose();
        Task.Run(async () =>
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try { if (_host != null) await _host.StopAsync(cts.Token); } catch { }
            try { if (_host != null) await _host.DisposeAsync(); } catch { }
            Environment.Exit(0);
        });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}

