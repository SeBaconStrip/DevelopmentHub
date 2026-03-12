using DevelopmentHub.Api.Configuration;
using DevelopmentHub.Api.Models.Dtos;
using Microsoft.Extensions.Caching.Memory;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DevelopmentHub.Api.Services;

public interface IAzureDevOpsService
{
    Task<List<PullRequestDto>> GetOpenPullRequestsAsync();
}

public class AzureDevOpsService(
    IHttpClientFactory httpClientFactory,
    IUserConfigService userConfigService,
    IMemoryCache cache,
    ILogger<AzureDevOpsService> logger) : IAzureDevOpsService
{
    private const string CacheKey = "azdo_pullrequests";

    public async Task<List<PullRequestDto>> GetOpenPullRequestsAsync()
    {
        if (cache.TryGetValue<List<PullRequestDto>>(CacheKey, out var cached) && cached is not null)
            return cached;

        var userConfig = await userConfigService.GetAsync();
        var cfg = userConfig.AzureDevOps;
        var cacheDuration = TimeSpan.FromSeconds(Math.Max(30, userConfig.PrRefreshIntervalSeconds / 2));

        if (string.IsNullOrWhiteSpace(cfg.Organization) ||
            string.IsNullOrWhiteSpace(cfg.Project) ||
            string.IsNullOrWhiteSpace(cfg.Pat))
        {
            logger.LogWarning("Azure DevOps is not fully configured. Skipping PR fetch.");
            return [];
        }

        var client = CreateAuthorizedClient(cfg.Pat);
        var result = await FetchPullRequestsAsync(client, cfg);
        cache.Set(CacheKey, result, cacheDuration);
        return result;
    }

    private HttpClient CreateAuthorizedClient(string pat)
    {
        var client = httpClientFactory.CreateClient("AzureDevOps");
        if (!string.IsNullOrWhiteSpace(pat))
        {
            var encoded = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{pat}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", encoded);
        }
        return client;
    }

    private async Task<List<PullRequestDto>> FetchPullRequestsAsync(HttpClient client, AzureDevOpsSettings cfg)
    {
        try
        {
            var url = $"https://dev.azure.com/{cfg.Organization}/{cfg.Project}/_apis/git/pullrequests?searchCriteria.status=active&api-version=7.1";
            var response = await client.GetStringAsync(url);
            var json = JsonNode.Parse(response);
            var values = json?["value"]?.AsArray();

            if (values is null) return [];

            var userEmail = cfg.UserEmail;
            return values
                .Where(v => v is not null)
                .Select(v => MapPullRequest(v!, cfg, userEmail))
                .OrderByDescending(p => p.CreatedAt)
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch pull requests");
            return [];
        }
    }

    private PullRequestDto MapPullRequest(JsonNode pr, AzureDevOpsSettings cfg, string userEmail)
    {
        var prId = pr["pullRequestId"]?.GetValue<int>() ?? 0;
        var authorUniqueName = pr["createdBy"]?["uniqueName"]?.GetValue<string>() ?? string.Empty;
        var authorMailAddress = pr["createdBy"]?["mailAddress"]?.GetValue<string>() ?? string.Empty;

        var isAuthor = !string.IsNullOrWhiteSpace(userEmail) &&
            (string.Equals(authorUniqueName, userEmail, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(authorMailAddress, userEmail, StringComparison.OrdinalIgnoreCase));

        var isReviewer = !string.IsNullOrWhiteSpace(userEmail) &&
            (pr["reviewers"]?.AsArray().Any(r =>
                string.Equals(r?["uniqueName"]?.GetValue<string>(), userEmail, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(r?["mailAddress"]?.GetValue<string>(), userEmail, StringComparison.OrdinalIgnoreCase)) ?? false);

        return new PullRequestDto
        {
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
            IsReviewer = isReviewer,
            Url = $"https://dev.azure.com/{cfg.Organization}/{cfg.Project}/_git/{pr["repository"]?["name"]?.GetValue<string>()}/pullrequest/{prId}"
        };
    }
}
