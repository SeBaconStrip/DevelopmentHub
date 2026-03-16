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
}

public record OpenUrlRequest(string Url);
