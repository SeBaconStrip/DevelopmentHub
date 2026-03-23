namespace DevelopmentHub.Api.Services;

/// <summary>
/// In-process bridge: the WPF app sets the Picker delegate so the backend
/// controller can trigger a native open-file dialog on the UI thread.
/// </summary>
public static class FilePickerBridge
{
    /// <summary>
    /// Set by the WPF host. Returns the selected file path, or null if cancelled.
    /// </summary>
    public static Func<string, Task<string?>>? Picker { get; set; }
}
