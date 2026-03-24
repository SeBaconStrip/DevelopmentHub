namespace DevelopmentHub.Api.Services;

/// <summary>
/// In-process notification bridge: the ConfigController fires this when the hotkey
/// setting changes, and the WPF App.xaml.cs subscribes to re-register the global hotkey.
/// </summary>
public static class HotkeyChangedNotifier
{
    public static event Action<string>? HotkeyChanged;

    public static void Notify(string hotkeyBinding) => HotkeyChanged?.Invoke(hotkeyBinding);
}
