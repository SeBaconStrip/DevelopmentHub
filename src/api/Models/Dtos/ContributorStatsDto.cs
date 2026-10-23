namespace DevelopmentHub.Api.Models.Dtos;

/// <summary>A repository that can be selected for a contributor-stats report.</summary>
public class RepositoryRefDto
{
    public string ProviderId { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
}

/// <summary>Aggregated pull-request contribution counts for a single person.</summary>
public class ContributorStatDto
{
    public string Login { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public int AuthoredCount { get; set; }
    public int ReviewedCount { get; set; }
}

/// <summary>Request body for POST /api/contributors/stats.</summary>
public class ContributorStatsRequestDto
{
    public string Provider { get; set; } = string.Empty;
    public List<string> RepositoryIds { get; set; } = [];
    public DateTime? Since { get; set; }
    public DateTime? Until { get; set; }
}

/// <summary>Result of a contributor-stats report.</summary>
public class ContributorStatsResultDto
{
    public string ProviderId { get; set; } = string.Empty;
    public List<RepositoryRefDto> Repositories { get; set; } = [];
    public List<ContributorStatDto> Contributors { get; set; } = [];

    /// <summary>Data that was left out of the counts (limits reached, counts unavailable).</summary>
    public List<string> Warnings { get; set; } = [];
    public DateTime FetchedAt { get; set; }
}

/// <summary>Request body for POST /api/contributors/repository-coverage.</summary>
public class RepositoryCoverageRequestDto
{
    public string Provider { get; set; } = string.Empty;
    public List<string> RepositoryIds { get; set; } = [];

    /// <summary>Logins or display names to include. Empty means every contributor found.</summary>
    public List<string> Contributors { get; set; } = [];
    public DateTime? Since { get; set; }
    public DateTime? Until { get; set; }
}

/// <summary>A single contributor's pull-request activity within one repository.</summary>
public class RepositoryContributionDto
{
    public string RepositoryId { get; set; } = string.Empty;
    public string RepositoryName { get; set; } = string.Empty;
    public int AuthoredCount { get; set; }
    public int ReviewedCount { get; set; }

    /// <summary>Files added across the contributor's authored PRs in this repository.</summary>
    public int FilesAdded { get; set; }

    /// <summary>Files edited across the contributor's authored PRs in this repository.</summary>
    public int FilesEdited { get; set; }

    /// <summary>Files deleted across the contributor's authored PRs in this repository.</summary>
    public int FilesDeleted { get; set; }
}

/// <summary>The set of repositories a single contributor was active in.</summary>
public class ContributorCoverageDto
{
    public string Login { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public int TotalAuthored { get; set; }
    public int TotalReviewed { get; set; }
    public int TotalFilesAdded { get; set; }
    public int TotalFilesEdited { get; set; }
    public int TotalFilesDeleted { get; set; }
    public List<RepositoryContributionDto> Repositories { get; set; } = [];
}

/// <summary>Result of a repository-coverage report.</summary>
public class RepositoryCoverageResultDto
{
    public string ProviderId { get; set; } = string.Empty;
    public List<RepositoryRefDto> Repositories { get; set; } = [];
    public List<ContributorCoverageDto> Contributors { get; set; } = [];

    /// <summary>Data that was left out of the counts (limits reached, counts unavailable).</summary>
    public List<string> Warnings { get; set; } = [];
    public DateTime FetchedAt { get; set; }
}
