namespace DevelopmentHub.Api.Services;

/// <summary>
/// In-process bridge: the WPF app sets the Picker delegate so the backend
/// controller can trigger a native folder-browser dialog on the UI thread.
/// </summary>
public static class FolderPickerBridge
{
    /// <summary>
    /// Set by the WPF host. Returns the selected folder path, or null if cancelled.
    /// </summary>
    public static Func<Task<string?>>? Picker { get; set; }
}
