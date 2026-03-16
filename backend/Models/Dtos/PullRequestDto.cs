namespace DevelopmentHub.Api.Models.Dtos;

public class PullRequestDto
{
    public string ProviderId { get; set; } = string.Empty;
    public int PrId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string RepositoryName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public bool CreatedByMe { get; set; }
    public bool IsReviewer { get; set; }
    public int ReviewerVote { get; set; }
    public string SourceBranch { get; set; } = string.Empty;
    public string TargetBranch { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsDraft { get; set; }
    public string AuthorDisplayName { get; set; } = string.Empty;
}
