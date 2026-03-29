namespace DevelopmentHub.Api.Models.Dtos;

public class RunWorkflowRequestDto
{
    public Dictionary<string, string> Inputs { get; set; } = new(StringComparer.OrdinalIgnoreCase);
public List<string> SkippedSteps { get; set; } = [];
}

public class WorkflowExecutionDto
{
    public string Id { get; set; } = string.Empty;
    public string WorkflowId { get; set; } = string.Empty;
    public string WorkflowName { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public int? ExitCode { get; set; }
    public string Summary { get; set; } = string.Empty;
}

public class WorkflowExecutionDetailDto : WorkflowExecutionDto
{
    public List<WorkflowLogLineDto> LogLines { get; set; } = [];
}

public class WorkflowLogLineDto
{
    public string Text { get; set; } = string.Empty;
    public string Stream { get; set; } = "info";
    public DateTime Timestamp { get; set; }
}
