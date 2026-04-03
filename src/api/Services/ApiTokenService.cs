using System.Security.Cryptography;

namespace DevelopmentHub.Api.Services;

/// <summary>
/// Holds a random per-process secret that the embedded WebView injects into every page as
/// <c>window.__devHubToken</c>. All <c>/api/</c> and <c>/hubs/</c> requests must carry this
/// value in the <c>X-Dev-Hub-Token</c> header (or as a Bearer token / query param for SignalR).
/// This prevents other local processes from calling the API without the user's knowledge.
/// </summary>
public sealed class ApiTokenService
{
    public string Token { get; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
}
