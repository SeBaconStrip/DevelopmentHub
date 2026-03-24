namespace DevelopmentHub.Api.Configuration;

/// <summary>
/// Infrastructure-only settings read from appsettings.json on startup.
/// User-configurable settings (repositories, Azure DevOps) are stored in LiteDB — see <see cref="DevelopmentHub.Api.Models.UserConfigDao"/>.
/// </summary>
public class AppSettings
{
    /// <summary>
    /// Path to the LiteDB database file.
    /// Defaults to %LOCALAPPDATA%\DevelopmentHub\developmenthub.db when empty.
    /// </summary>
    public string LiteDbPath { get; set; } = string.Empty;

    /// <summary>
    /// Directory containing plugin subdirectories.
    /// Defaults to %LOCALAPPDATA%\DevelopmentHub\plugins when empty.
    /// </summary>
    public string PluginsPath { get; set; } = string.Empty;
}

