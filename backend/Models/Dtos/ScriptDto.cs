namespace DevelopmentHub.Api.Models.Dtos;

public class ScriptDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public string[] Arguments { get; set; } = [];
}

public class ExecutionDto
{
    public string Id { get; set; } = string.Empty;
    public string ScriptDefinitionId { get; set; } = string.Empty;
    public string ScriptName { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public int? ExitCode { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class ExecutionDetailDto : ExecutionDto
{
    public string OutputLog { get; set; } = string.Empty;
}
