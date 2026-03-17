using DevelopmentHub.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace DevelopmentHub.Api.Controllers;

[ApiController]
[Route("api/launcher")]
public class LauncherController(ILauncherService launcher) : ControllerBase
{
    [HttpPost("open-url")]
    public async Task<IActionResult> OpenUrl([FromBody] OpenUrlRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Url) ||
            !Uri.TryCreate(request.Url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            return BadRequest(new { error = "Invalid URL." });
        }

        var success = await launcher.OpenUrlAsync(request.Url);
        return success ? Ok(new { ok = true }) : StatusCode(StatusCodes.Status502BadGateway, new { ok = false, error = "Failed to open URL." });
    }

    [HttpPost("open-explorer")]
    public async Task<IActionResult> OpenExplorer([FromBody] OpenExplorerRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Target))
            return BadRequest(new { error = "Invalid Explorer target." });

        var target = request.Target.Trim();
        var exists = Directory.Exists(target) || System.IO.File.Exists(target);
        if (!exists)
            return BadRequest(new { error = "Explorer target does not exist." });

        var success = await launcher.OpenWithExplorerAsync(target);
        return success ? Ok(new { ok = true }) : StatusCode(StatusCodes.Status502BadGateway, new { ok = false, error = "Failed to open Explorer target." });
    }
}

public record OpenUrlRequest(string Url);
public record OpenExplorerRequest(string Target);
