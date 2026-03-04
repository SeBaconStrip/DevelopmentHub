using DevelopmentHub.Api.Models.Dtos;
using DevelopmentHub.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace DevelopmentHub.Api.Controllers;

[ApiController]
[Route("api/scripts")]
public class ScriptsController(IScriptService scriptService) : ControllerBase
{
    [HttpGet]
    public ActionResult<List<ScriptDto>> GetAll()
    {
        return Ok(scriptService.GetAllDefinitions());
    }

    [HttpPost("{scriptId}/execute")]
    public async Task<ActionResult<ExecutionDto>> Execute(string scriptId, CancellationToken cancellationToken)
    {
        try
        {
            var result = await scriptService.ExecuteAsync(scriptId, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = $"Script '{scriptId}' not found." });
        }
    }

    [HttpPost("executions/{executionId}/cancel")]
    public IActionResult Cancel(string executionId)
    {
        var cancelled = scriptService.CancelExecution(executionId);
        return cancelled ? Ok(new { cancelled = true }) : NotFound(new { error = "Execution not running." });
    }

    [HttpGet("executions")]
    public async Task<ActionResult<List<ExecutionDto>>> GetHistory([FromQuery] int limit = 50)
    {
        return Ok(await scriptService.GetExecutionHistoryAsync(limit));
    }

    [HttpGet("executions/{executionId}")]
    public async Task<ActionResult<ExecutionDetailDto>> GetDetail(string executionId)
    {
        var result = await scriptService.GetExecutionDetailAsync(executionId);
        return result is null ? NotFound() : Ok(result);
    }
}
