using DevelopmentHub.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace DevelopmentHub.Api.Controllers;

[ApiController]
[Route("api/file-picker")]
public class FilePickerController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<object>> Pick([FromQuery] string? filter = null)
    {
        if (FilePickerBridge.Picker is null)
            return StatusCode(501, new { error = "File picker not available." });

        var path = await FilePickerBridge.Picker(filter ?? "All files (*.*)|*.*");
        if (path is null)
            return Ok(new { cancelled = true, path = (string?)null });

        return Ok(new { cancelled = false, path });
    }
}
