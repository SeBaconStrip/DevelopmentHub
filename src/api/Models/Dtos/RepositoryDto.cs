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
    public EntryPointType Type { get; set; }
}

public enum EntryPointType
{
    Solution,
    CodeWorkspace,
    Folder
}

public class OpenRepositoryRequest
{
    public string? EntryPointPath { get; set; }
    public OpenWith OpenWith { get; set; } = OpenWith.VsCode;
}

public class UpdateTagsRequest
{
    public List<string> Tags { get; set; } = [];
}

public class OpenMultiWorkspaceRequest
{
    public List<string> RepositoryIds { get; set; } = [];
}

public enum OpenWith
{
    VisualStudio,
    VsCode,
    Explorer
}
