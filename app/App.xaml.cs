using Microsoft.AspNetCore.Builder;
using System.IO;
using System.Windows;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace DevelopmentHub.App;

public partial class App : Application
{
    private WebApplication? _host;
    private System.Windows.Forms.NotifyIcon? _trayIcon;

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

            // Start Kestrel on a background thread so it never blocks the UI thread
            _ = Task.Run(async () =>
            {
                try
                {
                    await _host.StartAsync();
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                        MessageBox.Show(ex.ToString(), "DevelopmentHub – Backend-Fehler", MessageBoxButton.OK, MessageBoxImage.Error));
                }
            });

            var window = new MainWindow();

            // Hide to tray instead of closing
            window.Closing += (_, args) =>
            {
                args.Cancel = true;
                window.Hide();
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
            menu.Items.Add("Öffnen", null, (_, _) => Dispatcher.Invoke(() => { window.Show(); window.Activate(); }));
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            menu.Items.Add("Beenden", null, (_, _) => Dispatcher.Invoke(ExitApp));
            _trayIcon.ContextMenuStrip = menu;
            _trayIcon.DoubleClick += (_, _) => Dispatcher.Invoke(() => { window.Show(); window.Activate(); });

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

