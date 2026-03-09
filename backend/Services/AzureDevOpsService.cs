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
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(60);

    public async Task<List<PullRequestDto>> GetOpenPullRequestsAsync()
    {
        if (cache.TryGetValue<List<PullRequestDto>>(CacheKey, out var cached) && cached is not null)
            return cached;

        var userConfig = await userConfigService.GetAsync();
        var cfg = userConfig.AzureDevOps;

        if (string.IsNullOrWhiteSpace(cfg.Organization) ||
            string.IsNullOrWhiteSpace(cfg.Project) ||
            string.IsNullOrWhiteSpace(cfg.Pat))
        {
            logger.LogWarning("Azure DevOps is not fully configured. Skipping PR fetch.");
            return [];
        }

        var client = CreateAuthorizedClient(cfg.Pat);
        var userId = await ResolveUserIdAsync(cfg, client);
        if (userId is null)
        {
            logger.LogWarning("Could not resolve Azure DevOps user ID for {Email}", cfg.UserEmail);
            return [];
        }

        var myPrs = await FetchPullRequestsAsync(client, cfg, $"searchCriteria.status=active&searchCriteria.creatorId={userId}");
        var reviewerPrs = await FetchPullRequestsAsync(client, cfg, $"searchCriteria.status=active&searchCriteria.reviewerId={userId}");

        // Merge and deduplicate
        var all = new Dictionary<int, PullRequestDto>();

        foreach (var pr in myPrs)
        {
            pr.CreatedByMe = true;
            all[pr.PrId] = pr;
        }

        foreach (var pr in reviewerPrs)
        {
            if (all.TryGetValue(pr.PrId, out var existing))
            {
                existing.IsReviewer = true;
            }
            else
            {
                pr.IsReviewer = true;
                all[pr.PrId] = pr;
            }
        }

        var result = all.Values.OrderByDescending(p => p.CreatedAt).ToList();
        cache.Set(CacheKey, result, CacheDuration);
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

    private async Task<string?> ResolveUserIdAsync(AzureDevOpsSettings cfg, HttpClient client)
    {
        if (string.IsNullOrWhiteSpace(cfg.UserEmail)) return null;

        try
        {
            var url = $"https://vssps.dev.azure.com/{cfg.Organization}/_apis/identities?searchFilter=MailAddress&filterValue={Uri.EscapeDataString(cfg.UserEmail)}&api-version=7.1";
            var response = await client.GetStringAsync(url);
            var json = JsonNode.Parse(response);
            var id = json?["value"]?[0]?["id"]?.GetValue<string>();
            return id;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to resolve user ID from Azure DevOps");
            return null;
        }
    }

    private async Task<List<PullRequestDto>> FetchPullRequestsAsync(
        HttpClient client, AzureDevOpsSettings cfg, string query)
    {
        try
        {
            var url = $"https://dev.azure.com/{cfg.Organization}/{cfg.Project}/_apis/git/pullrequests?{query}&api-version=7.1";
            var response = await client.GetStringAsync(url);
            var json = JsonNode.Parse(response);
            var values = json?["value"]?.AsArray();

            if (values is null) return [];

            return values
                .Where(v => v is not null)
                .Select(v => MapPullRequest(v!, cfg))
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch pull requests");
            return [];
        }
    }

    private PullRequestDto MapPullRequest(JsonNode pr, AzureDevOpsSettings cfg)
    {
        var prId = pr["pullRequestId"]?.GetValue<int>() ?? 0;
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
            Url = $"https://dev.azure.com/{cfg.Organization}/{cfg.Project}/_git/{pr["repository"]?["name"]?.GetValue<string>()}/pullrequest/{prId}"
        };
    }
}
