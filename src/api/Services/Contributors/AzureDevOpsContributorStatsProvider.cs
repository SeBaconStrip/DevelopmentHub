using DevelopmentHub.Api.Models.Dao;
using DevelopmentHub.Api.Models.Dtos;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace DevelopmentHub.Api.Services;

public class AzureDevOpsContributorStatsProvider(
    IHttpClientFactory httpClientFactory,
    ILogger<AzureDevOpsContributorStatsProvider> logger) : IContributorStatsProvider
{
    private const int PageSize = 100;
    private const int MaxPullRequestsPerRepo = 2000;

    // Azure DevOps throttles bursts (HTTP 429 / TF400733), so every fan-out is capped.
    private const int MaxConcurrentRepoRequests = 4;
    private const int MaxConcurrentDiffRequests = 8;
    private const int MaxConcurrentAvatarRequests = 8;

    public string ProviderId => "azureDevOps";

    public async Task<List<RepositoryRefDto>> ListRepositoriesAsync(
        UserConfigDao userConfig,
        CancellationToken cancellationToken = default)
    {
        var cfg = GetSettings(userConfig);
        EnsureConfigured(cfg);

        var client = CreateAuthorizedClient(cfg.Pat);
        var url = $"https://dev.azure.com/{cfg.Organization}/{cfg.Project}/_apis/git/repositories?api-version=7.1";

        var values = (await GetJsonAsync(client, url, "list Azure DevOps repositories", cancellationToken))?["value"]?.AsArray()
            ?? throw new ContributorProviderException("Azure DevOps returned an unexpected repository list.");

        return values
            .Where(v => v is not null)
            .Select(v => new RepositoryRefDto
            {
                ProviderId = ProviderId,
                Id = v!["id"]?.GetValue<string>() ?? string.Empty,
                Name = v["name"]?.GetValue<string>() ?? string.Empty,
                FullName = $"{cfg.Project}/{v["name"]?.GetValue<string>()}",
            })
            .Where(r => !string.IsNullOrEmpty(r.Id))
            .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<PullRequestFetchResult> FetchPullRequestsAsync(
        UserConfigDao userConfig,
        IReadOnlyCollection<RepositoryRefDto> repositories,
        DateTime? since,
        DateTime? until,
        Func<ContributorIdentity, bool>? includeFileChangesFor,
        CancellationToken cancellationToken = default)
    {
        var cfg = GetSettings(userConfig);
        EnsureConfigured(cfg);

        var client = CreateAuthorizedClient(cfg.Pat);
        var result = new PullRequestFetchResult();

        var perRepo = await Throttled.SelectAsync(
            repositories,
            MaxConcurrentRepoRequests,
            async repo => (Repo: repo, Prs: await FetchRepoPullRequestsAsync(client, cfg, repo, since, until, result, cancellationToken)),
            cancellationToken);

        var prs = perRepo
            .SelectMany(r => r.Prs.Select(pr => (r.Repo, Pr: pr, Author: ReadIdentity(pr["createdBy"]))))
            .ToList();

        // Only PRs authored by an included contributor need (relatively expensive) diff stats.
        var needsFiles = includeFileChangesFor is null
            ? []
            : prs.Where(p => p.Author is not null && includeFileChangesFor(p.Author)).ToList();

        var fileStats = await Throttled.SelectAsync(
            needsFiles,
            MaxConcurrentDiffRequests,
            async p => (Key: PrKey(p.Repo.Id, p.Pr), Counts: await FetchPrFileChangeCountsAsync(client, cfg, p.Repo.Id, p.Pr, cancellationToken)),
            cancellationToken);

        var countsByPr = fileStats
            .Where(s => s.Counts is not null)
            .ToDictionary(s => s.Key, s => s.Counts!);

        var missingCounts = fileStats.Count(s => s.Counts is null);
        if (missingCounts > 0)
            result.Warnings.Add($"File counts are unavailable for {missingCounts} pull request(s) and are missing from the totals.");

        foreach (var (repo, pr, author) in prs)
        {
            result.PullRequests.Add(new PullRequestRecord
            {
                RepositoryId = repo.Id,
                Author = author,
                Reviewers = ReadReviewers(pr),
                FileChanges = countsByPr.GetValueOrDefault(PrKey(repo.Id, pr)),
            });
        }

        return result;
    }

    public async Task<IReadOnlyList<string>> ResolveAvatarUrlsAsync(
        UserConfigDao userConfig,
        IReadOnlyList<string> avatarUrls,
        CancellationToken cancellationToken = default)
    {
        // Azure DevOps avatar URLs require PAT authentication, which the WebView cannot
        // provide for <img> requests. Fetch them server-side and inline as data URIs.
        var client = CreateAuthorizedClient(GetSettings(userConfig).Pat);
        return await Throttled.SelectAsync(
            avatarUrls,
            MaxConcurrentAvatarRequests,
            url => FetchAvatarDataUriAsync(client, url, cancellationToken),
            cancellationToken);
    }

    /// <summary>
    /// Reviewers who actually voted. Group reviewers (e.g. "[Project]\Project Team" added
    /// by a branch policy) inherit a member's vote, so they are not people and are skipped.
    /// </summary>
    public static List<ContributorIdentity> ReadReviewers(JsonNode pr)
    {
        var reviewers = new List<ContributorIdentity>();
        foreach (var reviewer in pr["reviewers"]?.AsArray() ?? [])
        {
            if (reviewer is null) continue;
            if (reviewer["isContainer"]?.GetValue<bool>() == true) continue;
            if ((reviewer["vote"]?.GetValue<int>() ?? 0) == 0) continue;
            if (ReadIdentity(reviewer) is { } identity)
                reviewers.Add(identity);
        }

        return reviewers;
    }

    private static ContributorIdentity? ReadIdentity(JsonNode? identity)
    {
        if (identity is null) return null;
        var uniqueName = identity["uniqueName"]?.GetValue<string>();
        var displayName = identity["displayName"]?.GetValue<string>() ?? string.Empty;
        var login = !string.IsNullOrWhiteSpace(uniqueName) ? uniqueName : displayName;
        if (string.IsNullOrWhiteSpace(login)) return null;

        return new ContributorIdentity(
            login,
            string.IsNullOrWhiteSpace(displayName) ? login : displayName,
            identity["imageUrl"]?.GetValue<string>() ?? string.Empty);
    }

    private async Task<string> FetchAvatarDataUriAsync(
        HttpClient client,
        string avatarUrl,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(avatarUrl))
            return string.Empty;

        try
        {
            using var response = await client.GetAsync(avatarUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return string.Empty;

            var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/png";
            if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            if (bytes.Length == 0)
                return string.Empty;

            return $"data:{contentType};base64,{Convert.ToBase64String(bytes)}";
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogDebug(ex, "Failed to fetch Azure DevOps avatar from {Url}", avatarUrl);
            return string.Empty;
        }
    }

    /// <returns><c>null</c> when the counts could not be determined.</returns>
    private async Task<FileChangeCounts?> FetchPrFileChangeCountsAsync(
        HttpClient client,
        AzureDevOpsProviderSettings cfg,
        string repoId,
        JsonNode pr,
        CancellationToken cancellationToken)
    {
        var baseCommit = pr["lastMergeTargetCommit"]?["commitId"]?.GetValue<string>();
        var headCommit = pr["lastMergeSourceCommit"]?["commitId"]?.GetValue<string>();
        if (string.IsNullOrEmpty(baseCommit) || string.IsNullOrEmpty(headCommit))
            return null;

        var url =
            $"https://dev.azure.com/{cfg.Organization}/{cfg.Project}/_apis/git/repositories/{repoId}/diffs/commits" +
            $"?baseVersion={baseCommit}&baseVersionType=commit&targetVersion={headCommit}&targetVersionType=commit&api-version=7.1";

        try
        {
            var response = await client.GetStringAsync(url, cancellationToken);
            var changeCounts = JsonNode.Parse(response)?["changeCounts"];
            if (changeCounts is null)
                return null;

            int Count(string key) => changeCounts[key]?.GetValue<int>() ?? 0;
            return new FileChangeCounts(Count("Add"), Count("Edit"), Count("Delete"));
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogDebug(ex, "Failed to fetch PR file change counts for repository {RepoId}", repoId);
            return null;
        }
    }

    private static string PrKey(string repoId, JsonNode pr) =>
        $"{repoId}/{pr["pullRequestId"]?.GetValue<int>() ?? 0}";

    private async Task<List<JsonNode>> FetchRepoPullRequestsAsync(
        HttpClient client,
        AzureDevOpsProviderSettings cfg,
        RepositoryRefDto repo,
        DateTime? since,
        DateTime? until,
        PullRequestFetchResult result,
        CancellationToken cancellationToken)
    {
        var collected = new List<JsonNode>();
        var skip = 0;

        while (true)
        {
            var url = BuildPullRequestUrl(cfg, repo.Id, since, until, skip);
            var values = (await GetJsonAsync(client, url, $"fetch pull requests for {repo.Name}", cancellationToken))?["value"]?.AsArray()
                ?? throw new ContributorProviderException($"Azure DevOps returned an unexpected pull request list for {repo.Name}.");

            collected.AddRange(values.Where(v => v is not null).Select(v => v!));

            if (values.Count < PageSize)
                break;

            skip += PageSize;
            if (skip >= MaxPullRequestsPerRepo)
            {
                lock (result.Warnings)
                    result.Warnings.Add($"{repo.Name}: only the newest {MaxPullRequestsPerRepo} pull requests were counted.");
                break;
            }
        }

        return collected;
    }

    private async Task<JsonNode?> GetJsonAsync(
        HttpClient client,
        string url,
        string operation,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Azure DevOps request failed. Operation={Operation} Status={Status}", operation, (int)response.StatusCode);
                throw new ContributorProviderException(response.StatusCode switch
                {
                    System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden =>
                        $"Azure DevOps rejected the access token ({(int)response.StatusCode}). Check the key in Settings → Integrations.",
                    System.Net.HttpStatusCode.TooManyRequests =>
                        "Azure DevOps is throttling requests. Try again in a few minutes or select fewer repositories.",
                    _ => $"Failed to {operation} (HTTP {(int)response.StatusCode}).",
                });
            }

            return JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        }
        catch (Exception ex) when (ex is not ContributorProviderException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Azure DevOps request failed. Operation={Operation}", operation);
            throw new ContributorProviderException($"Failed to {operation}: {ex.Message}", ex);
        }
    }

    private static string BuildPullRequestUrl(
        AzureDevOpsProviderSettings cfg,
        string repoId,
        DateTime? since,
        DateTime? until,
        int skip)
    {
        var url =
            $"https://dev.azure.com/{cfg.Organization}/{cfg.Project}/_apis/git/repositories/{repoId}/pullrequests" +
            $"?searchCriteria.status=all&$top={PageSize}&$skip={skip}&api-version=7.1";

        if (since.HasValue || until.HasValue)
            url += "&searchCriteria.queryTimeRangeType=Created";
        if (since.HasValue)
            url += $"&searchCriteria.minTime={Uri.EscapeDataString(since.Value.ToUniversalTime().ToString("o"))}";
        if (until.HasValue)
            url += $"&searchCriteria.maxTime={Uri.EscapeDataString(until.Value.ToUniversalTime().ToString("o"))}";

        return url;
    }

    private static void EnsureConfigured(AzureDevOpsProviderSettings cfg)
    {
        if (string.IsNullOrWhiteSpace(cfg.Organization) ||
            string.IsNullOrWhiteSpace(cfg.Project) ||
            string.IsNullOrWhiteSpace(cfg.Pat))
        {
            throw new ContributorProviderException(
                "Azure DevOps is not configured. Set organization, project and key in Settings → Integrations.");
        }
    }

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
}
