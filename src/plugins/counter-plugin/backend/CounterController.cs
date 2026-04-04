using Microsoft.AspNetCore.Mvc;

namespace ExamplePlugin.Backend.Controllers;

[ApiController]
[Route("api/plugins/counter")]
public class CounterController(CounterService counter) : ControllerBase
{
    [HttpGet("count")]
    public IActionResult GetCount() => Ok(new { count = counter.Value });

    [HttpPost("add")]
    public IActionResult Add([FromQuery] int step = 1)
    {
        var result = counter.Add(step);
        return Ok(new { count = result });
    }
}
