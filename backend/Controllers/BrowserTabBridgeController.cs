using DevelopmentHub.Api.Services;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace DevelopmentHub.Api.Controllers;

[ApiController]
[EnableCors("BrowserExtension")]
[Route("api/browser-tab-bridge")]
public class BrowserTabBridgeController(IBrowserTabCommandBridge bridge) : ControllerBase
{
    [HttpGet("next")]
    public ActionResult<BrowserTabCommand> Next()
    {
        var command = bridge.DequeueNext();
        return command is null ? NoContent() : Ok(command);
    }

    [HttpPost("complete")]
    public IActionResult Complete([FromBody] BrowserTabCommandResult result)
    {
        if (string.IsNullOrWhiteSpace(result.CommandId))
            return BadRequest(new { error = "CommandId is required." });

        bridge.Complete(result.CommandId, result.Handled);
        return Ok();
    }
}

public sealed record BrowserTabCommandResult(string CommandId, bool Handled);
