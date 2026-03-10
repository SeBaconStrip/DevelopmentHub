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
}

public class AzureDevOpsSettings
{
    public string Organization { get; set; } = string.Empty;
    public string Project { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string Pat { get; set; } = string.Empty;
}

