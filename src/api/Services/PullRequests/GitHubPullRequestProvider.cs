using DevelopmentHub.Api.Models.Dao;
using DevelopmentHub.Api.Models.Dtos;
using Microsoft.Extensions.Caching.Memory;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;

namespace DevelopmentHub.Api.Services;

public class GitHubPullRequestProvider(
    IHttpClientFactory httpClientFactory,
    IMemoryCache cache,
    ILogger<GitHubPullRequestProvider> logger) : IPullRequestProvider
{
    private const string CacheKey = "pullrequests.github";
    private const int MaxPullRequests = 50;

    public string ProviderId => "github";

    public async Task<List<PullRequestDto>> GetOpenPullRequestsAsync(UserConfigDao userConfig, CancellationToken cancellationToken = default)
    {
        if (cache.TryGetValue<List<PullRequestDto>>(CacheKey, out var cached) && cached is not null)
        {
            logger.LogDebug("Returning GitHub pull requests from cache. PullRequestCount={PullRequestCount}", cached.Count);
            return cached;
        }

        var cfg = GetSettings(userConfig);
        var cacheDuration = TimeSpan.FromSeconds(Math.Max(30, userConfig.PrRefreshIntervalSeconds / 2));

        if (!IsConfigured(cfg))
        {
            logger.LogWarning("GitHub is not fully configured. Skipping PR fetch.");
            return [];
        }

        var client = CreateAuthorizedClient(cfg.Pat);
        logger.LogInformation(
            "Fetching GitHub pull requests. UserLogin={UserLogin} HasSearchQuery={HasSearchQuery} CacheDurationSeconds={CacheDurationSeconds}",
            cfg.UserLogin,
            !string.IsNullOrWhiteSpace(cfg.SearchQuery),
            cacheDuration.TotalSeconds);
        var result = await FetchPullRequestsAsync(client, cfg, cancellationToken);
        cache.Set(CacheKey, result, cacheDuration);
        return result;
    }

    private static bool IsConfigured(GitHubProviderSettings cfg) =>
        !string.IsNullOrWhiteSpace(cfg.UserLogin) &&
        !string.IsNullOrWhiteSpace(cfg.Pat);

    private static GitHubProviderSettings GetSettings(UserConfigDao userConfig) =>
        new()
        {
            UserLogin = userConfig.GetProviderSetting("github", "userLogin"),
            SearchQuery = userConfig.GetProviderSetting("github", "searchQuery"),
            Pat = userConfig.GetProviderSetting("github", "pat"),
        };

    private HttpClient CreateAuthorizedClient(string pat)
    {
        var client = httpClientFactory.CreateClient("GitHub");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", pat);
        return client;
    }

    private async Task<List<PullRequestDto>> FetchPullRequestsAsync(
        HttpClient client,
        GitHubProviderSettings cfg,
        CancellationToken cancellationToken)
    {
        try
        {
            var searchUrl = BuildSearchUrl(cfg);
            var searchResponse = await client.GetStringAsync(searchUrl, cancellationToken);
            var values = JsonNode.Parse(searchResponse)?["items"]?.AsArray();

            if (values is null) return [];

            var detailTasks = values
                .Where(v => v is not null)
                .Select(v => v!)
                .Where(v => v["pull_request"]?["url"] is not null)
                .Take(MaxPullRequests)
                .Select(v => FetchPullRequestDetailsAsync(
                    client,
                    v["pull_request"]!["url"]!.GetValue<string>(),
                    cfg,
                    cancellationToken));

            var pullRequests = await Task.WhenAll(detailTasks);
            var mapped = pullRequests
                .Where(pr => pr is not null)
                .Cast<PullRequestDto>()
                .OrderByDescending(p => p.CreatedAt)
                .ToList();

            logger.LogInformation(
                "Fetched GitHub pull requests. UserLogin={UserLogin} PullRequestCount={PullRequestCount}",
                cfg.UserLogin,
                mapped.Count);

            return mapped;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch GitHub pull requests");
            return [];
        }
    }

    private static string BuildSearchUrl(GitHubProviderSettings cfg)
    {
        var query = $"is:pr state:open archived:false involves:{cfg.UserLogin}";
        if (!string.IsNullOrWhiteSpace(cfg.SearchQuery))
        {
            query = $"{query} {cfg.SearchQuery.Trim()}";
        }

        return $"https://api.github.com/search/issues?q={Uri.EscapeDataString(query)}&sort=updated&order=desc&per_page={MaxPullRequests}";
    }

    private async Task<PullRequestDto?> FetchPullRequestDetailsAsync(
        HttpClient client,
        string detailUrl,
        GitHubProviderSettings cfg,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await client.GetStringAsync(detailUrl, cancellationToken);
            var pr = JsonNode.Parse(response);
            return pr is null ? null : MapPullRequest(pr, cfg);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch GitHub pull request details from {Url}", detailUrl);
            return null;
        }
    }

    private static PullRequestDto MapPullRequest(JsonNode pr, GitHubProviderSettings cfg)
    {
        var userLogin = cfg.UserLogin;
        var authorLogin = pr["user"]?["login"]?.GetValue<string>() ?? string.Empty;
        var requestedReviewers = pr["requested_reviewers"]?.AsArray() ?? [];
        var isReviewer = !string.IsNullOrWhiteSpace(userLogin) &&
            requestedReviewers.Any(reviewer =>
                string.Equals(
                    reviewer?["login"]?.GetValue<string>(),
                    userLogin,
                    StringComparison.OrdinalIgnoreCase));

        return new PullRequestDto
        {
            ProviderId = "github",
            PrId = pr["number"]?.GetValue<int>() ?? 0,
            Title = pr["title"]?.GetValue<string>() ?? string.Empty,
            RepositoryName = pr["base"]?["repo"]?["full_name"]?.GetValue<string>() ?? string.Empty,
            Status = (pr["state"]?.GetValue<string>() ?? "open").ToLowerInvariant(),
            SourceBranch = pr["head"]?["ref"]?.GetValue<string>() ?? string.Empty,
            TargetBranch = pr["base"]?["ref"]?.GetValue<string>() ?? string.Empty,
            CreatedAt = pr["created_at"]?.GetValue<DateTime>() ?? DateTime.UtcNow,
            IsDraft = pr["draft"]?.GetValue<bool>() ?? false,
            AuthorDisplayName = pr["user"]?["login"]?.GetValue<string>() ?? string.Empty,
            CreatedByMe = !string.IsNullOrWhiteSpace(userLogin) &&
                string.Equals(authorLogin, userLogin, StringComparison.OrdinalIgnoreCase),
            IsReviewer = isReviewer,
            ReviewerVote = 0,
            Url = pr["html_url"]?.GetValue<string>() ?? string.Empty
        };
    }
}

internal sealed class GitHubProviderSettings
{
    public string UserLogin { get; init; } = string.Empty;
    public string SearchQuery { get; init; } = string.Empty;
    public string Pat { get; init; } = string.Empty;
}
