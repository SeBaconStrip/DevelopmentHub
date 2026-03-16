namespace DevelopmentHub.Api.Models.Dtos;

public class WorkflowDefinitionDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool RequiresConfirmation { get; set; }
    public List<WorkflowInputDto> Inputs { get; set; } = [];
    public List<WorkflowStepDto> Steps { get; set; } = [];
}

public class WorkflowInputDto
{
    public string Name { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Type { get; set; } = "text";
    public string DefaultValue { get; set; } = string.Empty;
}

public class WorkflowStepDto
{
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string Repository { get; set; } = string.Empty;
    public string ReleaseTag { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public string Organization { get; set; } = string.Empty;
    public string Project { get; set; } = string.Empty;
    public string PipelineId { get; set; } = string.Empty;
    public string RunId { get; set; } = string.Empty;
    public string BuildId { get; set; } = string.Empty;
    public string Pat { get; set; } = string.Empty;
    public string TargetPath { get; set; } = string.Empty;
    public bool Overwrite { get; set; }
    public bool RunElevated { get; set; }
    public string ArchivePath { get; set; } = string.Empty;
    public string DestinationPath { get; set; } = string.Empty;
    public bool CleanDestination { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string[] Arguments { get; set; } = [];
    public bool WaitForExit { get; set; } = true;
    public int[] SuccessExitCodes { get; set; } = [0];
    public List<JsonPatchOperationDto> Operations { get; set; } = [];
    public string ServiceName { get; set; } = string.Empty;
    public bool WaitForRunning { get; set; } = true;
    public int TimeoutSeconds { get; set; } = 60;
}

public class JsonPatchOperationDto
{
    public string Op { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public object? Value { get; set; }
}

public class RunWorkflowRequestDto
{
    public Dictionary<string, string> Inputs { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public bool Confirmed { get; set; }
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
