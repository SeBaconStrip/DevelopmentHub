using DevelopmentHub.Api.Models;
using DevelopmentHub.Api.Models.Dtos;
using Microsoft.Extensions.Caching.Memory;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace DevelopmentHub.Api.Services;

public class AzureDevOpsPullRequestProvider(
    IHttpClientFactory httpClientFactory,
    IMemoryCache cache,
    ILogger<AzureDevOpsPullRequestProvider> logger) : IPullRequestProvider
{
    private const string CacheKey = "pullrequests.azuredevops";

    public string ProviderId => "azureDevOps";

    public async Task<List<PullRequestDto>> GetOpenPullRequestsAsync(UserConfigDao userConfig, CancellationToken cancellationToken = default)
    {
        if (cache.TryGetValue<List<PullRequestDto>>(CacheKey, out var cached) && cached is not null)
        {
            logger.LogDebug("Returning Azure DevOps pull requests from cache. PullRequestCount={PullRequestCount}", cached.Count);
            return cached;
        }

        var cfg = GetSettings(userConfig);
        var cacheDuration = TimeSpan.FromSeconds(Math.Max(30, userConfig.PrRefreshIntervalSeconds / 2));

        if (!IsConfigured(cfg))
        {
            logger.LogWarning("Azure DevOps is not fully configured. Skipping PR fetch.");
            return [];
        }

        var client = CreateAuthorizedClient(cfg.Pat);
        logger.LogInformation(
            "Fetching Azure DevOps pull requests. Organization={Organization} Project={Project} CacheDurationSeconds={CacheDurationSeconds}",
            cfg.Organization,
            cfg.Project,
            cacheDuration.TotalSeconds);
        var result = await FetchPullRequestsAsync(client, cfg, cancellationToken);
        cache.Set(CacheKey, result, cacheDuration);
        return result;
    }

    private static bool IsConfigured(AzureDevOpsProviderSettings cfg) =>
        !string.IsNullOrWhiteSpace(cfg.Organization) &&
        !string.IsNullOrWhiteSpace(cfg.Project) &&
        !string.IsNullOrWhiteSpace(cfg.Pat);

    private static AzureDevOpsProviderSettings GetSettings(UserConfigDao userConfig) =>
        new()
        {
            Organization = userConfig.GetProviderSetting("azureDevOps", "organization"),
            Project = userConfig.GetProviderSetting("azureDevOps", "project"),
            UserEmail = userConfig.GetProviderSetting("azureDevOps", "userEmail"),
            Pat = userConfig.GetProviderSetting("azureDevOps", "pat"),
        };

    private HttpClient CreateAuthorizedClient(string pat)
    {
        var client = httpClientFactory.CreateClient("AzureDevOps");
        var encoded = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{pat}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", encoded);
        return client;
    }

    private async Task<List<PullRequestDto>> FetchPullRequestsAsync(
        HttpClient client,
        AzureDevOpsProviderSettings cfg,
        CancellationToken cancellationToken)
    {
        try
        {
            var url = $"https://dev.azure.com/{cfg.Organization}/{cfg.Project}/_apis/git/pullrequests?searchCriteria.status=active&$top=200&api-version=7.1";
            var response = await client.GetStringAsync(url, cancellationToken);
            var json = JsonNode.Parse(response);
            var values = json?["value"]?.AsArray();

            if (values is null) return [];
            var pullRequests = values
                .Where(v => v is not null)
                .Select(v => MapPullRequest(v!, cfg, cfg.UserEmail))
                .OrderByDescending(p => p.CreatedAt)
                .ToList();

            logger.LogInformation(
                "Fetched Azure DevOps pull requests. Organization={Organization} Project={Project} PullRequestCount={PullRequestCount}",
                cfg.Organization,
                cfg.Project,
                pullRequests.Count);

            return pullRequests;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch Azure DevOps pull requests");
            return [];
        }
    }

    private static PullRequestDto MapPullRequest(JsonNode pr, AzureDevOpsProviderSettings cfg, string userEmail)
    {
        var prId = pr["pullRequestId"]?.GetValue<int>() ?? 0;
        var authorUniqueName = pr["createdBy"]?["uniqueName"]?.GetValue<string>() ?? string.Empty;
        var authorMailAddress = pr["createdBy"]?["mailAddress"]?.GetValue<string>() ?? string.Empty;

        var isAuthor = !string.IsNullOrWhiteSpace(userEmail) &&
            (string.Equals(authorUniqueName, userEmail, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(authorMailAddress, userEmail, StringComparison.OrdinalIgnoreCase));

        var matchedReviewer = !string.IsNullOrWhiteSpace(userEmail)
            ? pr["reviewers"]?.AsArray().FirstOrDefault(r =>
                string.Equals(r?["uniqueName"]?.GetValue<string>(), userEmail, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(r?["mailAddress"]?.GetValue<string>(), userEmail, StringComparison.OrdinalIgnoreCase))
            : null;

        return new PullRequestDto
        {
            ProviderId = "azureDevOps",
            PrId = prId,
            Title = pr["title"]?.GetValue<string>() ?? string.Empty,
            RepositoryName = pr["repository"]?["name"]?.GetValue<string>() ?? string.Empty,
            Status = pr["status"]?.GetValue<string>() ?? string.Empty,
            SourceBranch = (pr["sourceRefName"]?.GetValue<string>() ?? string.Empty).Replace("refs/heads/", ""),
            TargetBranch = (pr["targetRefName"]?.GetValue<string>() ?? string.Empty).Replace("refs/heads/", ""),
            CreatedAt = pr["creationDate"]?.GetValue<DateTime>() ?? DateTime.UtcNow,
            IsDraft = pr["isDraft"]?.GetValue<bool>() ?? false,
            AuthorDisplayName = pr["createdBy"]?["displayName"]?.GetValue<string>() ?? string.Empty,
            CreatedByMe = isAuthor,
            IsReviewer = matchedReviewer is not null,
            ReviewerVote = matchedReviewer?["vote"]?.GetValue<int>() ?? 0,
            Url = $"https://dev.azure.com/{cfg.Organization}/{cfg.Project}/_git/{pr["repository"]?["name"]?.GetValue<string>()}/pullrequest/{prId}"
        };
    }
}

internal sealed class AzureDevOpsProviderSettings
{
    public string Organization { get; init; } = string.Empty;
    public string Project { get; init; } = string.Empty;
    public string UserEmail { get; init; } = string.Empty;
    public string Pat { get; init; } = string.Empty;
}
