namespace DevelopmentHub.Api.Models.Dtos;

public class RepositoryDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public bool IsFavorite { get; set; }
    public string? CurrentBranch { get; set; }
    public int AheadBy { get; set; }
    public int BehindBy { get; set; }
    public List<EntryPointDto> EntryPoints { get; set; } = [];
    public int OpenCount { get; set; }
    public DateTime? LastOpenedAt { get; set; }
    public DateTime? LastSyncedAt { get; set; }
    public double UsageScore { get; set; }
    public string? ScanIssueCode { get; set; }
    public string? ScanIssueMessage { get; set; }
    public List<string> Tags { get; set; } = [];
}

public class EntryPointDto
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
}

public class RepositoryOpenerDto
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string FileExtension { get; set; } = string.Empty;
    public string ProgramPath { get; set; } = string.Empty;
    public string IconType { get; set; } = "custom";
    public string IconPath { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public class OpenRepositoryRequest
{
    /// <summary>ID of the configured opener. Null or empty = open with Explorer.</summary>
    public string? OpenerId { get; set; }
    /// <summary>Optional specific entry-point file override.</summary>
    public string? EntryPointPath { get; set; }
}

public class UpdateTagsRequest
{
    public List<string> Tags { get; set; } = [];
}

public class OpenMultiWorkspaceRequest
{
    public List<string> RepositoryIds { get; set; } = [];
}
