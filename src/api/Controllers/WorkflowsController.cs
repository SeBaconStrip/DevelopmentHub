using DevelopmentHub.Api.Models.Dtos;
using DevelopmentHub.Api.Services;
using DevelopmentHub.Workflow;
using Microsoft.AspNetCore.Mvc;

namespace DevelopmentHub.Api.Controllers;

[ApiController]
[Route("api/workflows")]
public class WorkflowsController(
    IWorkflowService workflowService,
    ILogger<WorkflowsController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WorkflowDefinition>>> GetDefinitions()
    {
        var workflows = await workflowService.GetDefinitionsAsync();
        return Ok(workflows);
    }

    [HttpGet("executions")]
    public async Task<ActionResult<IReadOnlyList<WorkflowExecutionDto>>> GetExecutions()
    {
        var executions = await workflowService.GetExecutionsAsync();
        return Ok(executions);
    }

    [HttpGet("executions/{executionId}")]
    public async Task<ActionResult<WorkflowExecutionDetailDto>> GetExecution(string executionId)
    {
        var execution = await workflowService.GetExecutionAsync(executionId);
        return execution is null ? NotFound() : Ok(execution);
    }

    [HttpPost("{workflowId}/run")]
    public async Task<ActionResult<WorkflowExecutionDto>> Run(
        string workflowId,
        [FromBody] RunWorkflowRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var execution = await workflowService.RunAsync(workflowId, request, cancellationToken);
            return Ok(execution);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Workflow run rejected. WorkflowId={WorkflowId}", workflowId);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Workflow run failed unexpectedly. WorkflowId={WorkflowId}", workflowId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Workflow execution failed." });
        }
    }
}
