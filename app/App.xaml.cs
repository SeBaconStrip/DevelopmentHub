using Microsoft.AspNetCore.Builder;
using System.Windows;

namespace DevelopmentHub.App;

public partial class App : Application
{
    private WebApplication? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

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
            window.Closed += (_, _) =>
            {
                Task.Run(async () =>
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    try { if (_host != null) await _host.StopAsync(cts.Token); } catch { }
                    try { if (_host != null) await _host.DisposeAsync(); } catch { }
                    Environment.Exit(0);
                });
            };
            window.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString(), "DevelopmentHub – Startfehler", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        base.OnExit(e);
    }
}

