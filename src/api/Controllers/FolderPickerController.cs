using DevelopmentHub.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace DevelopmentHub.Api.Controllers;

[ApiController]
[Route("api/folder-picker")]
public class FolderPickerController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<object>> Pick()
    {
        if (FolderPickerBridge.Picker is null)
            return StatusCode(501, new { error = "Folder picker not available." });

        var path = await FolderPickerBridge.Picker();
        if (path is null)
            return Ok(new { cancelled = true, path = (string?)null });

        return Ok(new { cancelled = false, path });
    }
}
