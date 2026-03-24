using Microsoft.AspNetCore.Mvc;

namespace ExamplePlugin.Backend.Controllers;

[ApiController]
[Route("api/plugins/hello")]
public class HelloController : ControllerBase
{
    /// <summary>
    /// Returns a greeting from the plugin backend.
    /// GET /api/plugins/hello/greet?name=World
    /// </summary>
    [HttpGet("greet")]
    public IActionResult Greet([FromQuery] string? name)
    {
        var displayName = string.IsNullOrWhiteSpace(name) ? "World" : name;
        return Ok(new { message = $"Hello, {displayName}! This response came from the Example Plugin backend." });
    }
}
