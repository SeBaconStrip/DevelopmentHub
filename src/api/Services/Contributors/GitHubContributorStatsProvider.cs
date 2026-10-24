using DevelopmentHub.Api.Models.Dao;
using DevelopmentHub.Api.Models.Dtos;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DevelopmentHub.Api.Services;

/// <summary>
/// Contributor statistics backed by the GitHub GraphQL API. A single query per
/// repository page returns pull requests, their reviews and (for coverage) the
/// changed files, which keeps the request count far below the REST equivalent
/// of two extra calls per pull request. Pull requests with more reviews or files
/// than fit in one page get follow-up queries for the rest.
/// </summary>
public class GitHubContributorStatsProvider(
    IHttpClientFactory httpClientFactory,
    ILogger<GitHubContributorStatsProvider> logger) : IContributorStatsProvider
{
    private const string GraphQlUrl = "https://api.github.com/graphql";
    private const int RepoPageSize = 100;
    private const int PullRequestPageSize = 50;
    private const int NestedPageSize = 100;
    private const int MaxPullRequestsPerRepo = 2000;
    private const int MaxConcurrentRepoQueries = 4;

    private const string IdentityFields = "login avatarUrl ... on User { name }";
    private const string ReviewFields = $"state author {{ {IdentityFields} }}";
    private const string FileFields = "changeType";

    public string ProviderId => "github";

    public async Task<List<RepositoryRefDto>> ListRepositoriesAsync(
        UserConfigDao userConfig,
        CancellationToken cancellationToken = default)
    {
        var client = CreateAuthorizedClient(GetPat(userConfig));
        var repos = new List<RepositoryRefDto>();
        string? cursor = null;

        const string query = """
            query($after: String, $first: Int!) {
              viewer {
                repositories(
                  first: $first,
                  after: $after,
                  affiliations: [OWNER, COLLABORATOR, ORGANIZATION_MEMBER],
                  orderBy: { field: NAME, direction: ASC }
                ) {
                  pageInfo { hasNextPage endCursor }
                  nodes { name owner { login } }
                }
              }
            }
            """;

        while (true)
        {
            var payload = await PostGraphQlAsync(
                client,
                query,
                new Dictionary<string, object?> { ["after"] = cursor, ["first"] = RepoPageSize },
                "list GitHub repositories",
                cancellationToken);

            var connection = Require(payload?["data"]?["viewer"]?["repositories"], payload, "list GitHub repositories");

            foreach (var node in connection["nodes"]?.AsArray() ?? [])
            {
                if (node is null) continue;
                var name = node["name"]?.GetValue<string>() ?? string.Empty;
                var owner = node["owner"]?["login"]?.GetValue<string>() ?? string.Empty;
                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(owner)) continue;

                // "owner/name" doubles as the identifier the later queries need.
                repos.Add(new RepositoryRefDto
                {
                    ProviderId = ProviderId,
                    Id = $"{owner}/{name}",
                    Name = name,
                    FullName = $"{owner}/{name}",
                });
            }

            if (!TryGetNextCursor(connection, out cursor)) break;
        }

        return repos
            .OrderBy(r => r.FullName, StringComparer.OrdinalIgnoreCase)
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
        var client = CreateAuthorizedClient(GetPat(userConfig));
        var result = new PullRequestFetchResult();

        var perRepo = await Throttled.SelectAsync(
            repositories,
            MaxConcurrentRepoQueries,
            repo => FetchRepoPullRequestsAsync(client, repo, since, until, includeFileChangesFor, cancellationToken),
            cancellationToken);

        foreach (var (records, warning) in perRepo)
        {
            result.PullRequests.AddRange(records);
            if (warning is not null)
                result.Warnings.Add(warning);
        }

        return result;
    }

    // GitHub avatar URLs are public, so the WebView can load them directly.
    public Task<IReadOnlyList<string>> ResolveAvatarUrlsAsync(
        UserConfigDao userConfig,
        IReadOnlyList<string> avatarUrls,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(avatarUrls);

    // ── GraphQL fetching ────────────────────────────────────────────────────

    private async Task<(List<PullRequestRecord> Records, string? Warning)> FetchRepoPullRequestsAsync(
        HttpClient client,
        RepositoryRefDto repo,
        DateTime? since,
        DateTime? until,
        Func<ContributorIdentity, bool>? includeFileChangesFor,
        CancellationToken cancellationToken)
    {
        var records = new List<PullRequestRecord>();

        var slash = repo.Id.IndexOf('/');
        if (slash <= 0 || slash == repo.Id.Length - 1)
            throw new ContributorProviderException($"Unexpected GitHub repository id '{repo.Id}'.");

        var owner = repo.Id[..slash];
        var name = repo.Id[(slash + 1)..];
        var includeFiles = includeFileChangesFor is not null;
        var query = BuildPullRequestQuery(includeFiles);
        var operation = $"fetch pull requests for {repo.FullName}";
        string? cursor = null;

        while (true)
        {
            var payload = await PostGraphQlAsync(
                client,
                query,
                new Dictionary<string, object?>
                {
                    ["owner"] = owner,
                    ["name"] = name,
                    ["after"] = cursor,
                    ["first"] = PullRequestPageSize,
                },
                operation,
                cancellationToken);

            var connection = Require(payload?["data"]?["repository"]?["pullRequests"], payload, operation);
            var nodes = connection["nodes"]?.AsArray() ?? [];

            // Newest first, so once a page reaches past the window start we stop.
            var reachedWindowStart = false;
            foreach (var node in nodes)
            {
                if (node is null) continue;
                var createdAt = node["createdAt"]?.GetValue<DateTime>();
                if (createdAt is null) continue;

                if (since.HasValue && createdAt.Value.ToUniversalTime() < since.Value.ToUniversalTime())
                {
                    reachedWindowStart = true;
                    continue;
                }

                if (until.HasValue && createdAt.Value.ToUniversalTime() > until.Value.ToUniversalTime())
                    continue;

                records.Add(await ToRecordAsync(client, owner, name, repo.Id, node, includeFileChangesFor, cancellationToken));
            }

            if (reachedWindowStart || !TryGetNextCursor(connection, out cursor)) break;

            if (records.Count >= MaxPullRequestsPerRepo)
                return (records, $"{repo.FullName}: only the newest {MaxPullRequestsPerRepo} pull requests were counted.");
        }

        return (records, null);
    }

    private async Task<PullRequestRecord> ToRecordAsync(
        HttpClient client,
        string owner,
        string name,
        string repositoryId,
        JsonNode pr,
        Func<ContributorIdentity, bool>? includeFileChangesFor,
        CancellationToken cancellationToken)
    {
        var number = pr["number"]?.GetValue<int>() ?? 0;
        var author = ReadIdentity(pr["author"]);

        var reviews = await ReadAllNodesAsync(
            client, owner, name, number, pr, "reviews", ReviewFields, cancellationToken);

        FileChangeCounts? files = null;
        if (includeFileChangesFor is not null && author is not null && includeFileChangesFor(author))
        {
            var fileNodes = await ReadAllNodesAsync(
                client, owner, name, number, pr, "files", FileFields, cancellationToken);
            files = CountFileChanges(fileNodes);
        }

        return new PullRequestRecord
        {
            RepositoryId = repositoryId,
            Author = author,
            Reviewers = DistinctReviewers(reviews, author?.Login),
            FileChanges = files,
        };
    }

    /// <summary>
    /// Returns every node of a nested pull-request connection, fetching the pages
    /// beyond the first one embedded in the pull-request query.
    /// </summary>
    private async Task<List<JsonNode>> ReadAllNodesAsync(
        HttpClient client,
        string owner,
        string name,
        int number,
        JsonNode pr,
        string field,
        string nodeFields,
        CancellationToken cancellationToken)
    {
        var connection = pr[field];
        var nodes = new List<JsonNode>();
        var operation = $"fetch {field} of {owner}/{name}#{number}";

        while (connection is not null)
        {
            nodes.AddRange((connection["nodes"]?.AsArray() ?? []).Where(n => n is not null).Select(n => n!));
            if (!TryGetNextCursor(connection, out var cursor)) break;

            var query = $$"""
                query($owner: String!, $name: String!, $number: Int!, $after: String) {
                  repository(owner: $owner, name: $name) {
                    pullRequest(number: $number) {
                      {{field}}(first: {{NestedPageSize}}, after: $after) {
                        pageInfo { hasNextPage endCursor }
                        nodes { {{nodeFields}} }
                      }
                    }
                  }
                }
                """;

            var payload = await PostGraphQlAsync(
                client,
                query,
                new Dictionary<string, object?>
                {
                    ["owner"] = owner,
                    ["name"] = name,
                    ["number"] = number,
                    ["after"] = cursor,
                },
                operation,
                cancellationToken);

            connection = Require(payload?["data"]?["repository"]?["pullRequest"]?[field], payload, operation);
        }

        return nodes;
    }

    private static string BuildPullRequestQuery(bool includeFiles)
    {
        var files = includeFiles
            ? $"files(first: {NestedPageSize}) {{ pageInfo {{ hasNextPage endCursor }} nodes {{ {FileFields} }} }}"
            : string.Empty;

        return $$"""
            query($owner: String!, $name: String!, $after: String, $first: Int!) {
              repository(owner: $owner, name: $name) {
                pullRequests(
                  first: $first,
                  after: $after,
                  states: [OPEN, CLOSED, MERGED],
                  orderBy: { field: CREATED_AT, direction: DESC }
                ) {
                  pageInfo { hasNextPage endCursor }
                  nodes {
                    number
                    createdAt
                    author { {{IdentityFields}} }
                    reviews(first: {{NestedPageSize}}) {
                      pageInfo { hasNextPage endCursor }
                      nodes { {{ReviewFields}} }
                    }
                    {{files}}
                  }
                }
              }
            }
            """;
    }

    private async Task<JsonNode?> PostGraphQlAsync(
        HttpClient client,
        string query,
        IDictionary<string, object?> variables,
        string operation,
        CancellationToken cancellationToken)
    {
        try
        {
            var body = JsonSerializer.Serialize(new { query, variables });
            using var content = new StringContent(body, Encoding.UTF8, "application/json");
            using var response = await client.PostAsync(GraphQlUrl, content, cancellationToken);

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "GitHub GraphQL request failed. Operation={Operation} Status={Status} Body={Body}",
                    operation,
                    (int)response.StatusCode,
                    Truncate(json));

                throw new ContributorProviderException(response.StatusCode switch
                {
                    HttpStatusCode.Unauthorized =>
                        "GitHub rejected the access token (401). Check the key in Settings → Integrations.",
                    HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests =>
                        "GitHub refused the request, most likely because of a rate limit. Try again in a few minutes or select fewer repositories.",
                    _ => $"Failed to {operation} (HTTP {(int)response.StatusCode}).",
                });
            }

            var payload = JsonNode.Parse(json);

            // GraphQL reports partial failures with HTTP 200 and an "errors" array.
            // Callers fail if the data they need is missing; otherwise the errors are only logged.
            var errors = payload?["errors"]?.AsArray();
            if (errors is not null && errors.Count > 0)
                logger.LogWarning("GitHub GraphQL returned errors: {Errors}", Truncate(errors.ToJsonString()));

            return payload;
        }
        catch (Exception ex) when (ex is not ContributorProviderException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "GitHub GraphQL request failed. Operation={Operation}", operation);
            throw new ContributorProviderException($"Failed to {operation}: {ex.Message}", ex);
        }
    }

    private static JsonNode Require(JsonNode? node, JsonNode? payload, string operation)
    {
        if (node is not null) return node;

        var message = payload?["errors"]?.AsArray().FirstOrDefault()?["message"]?.GetValue<string>();
        throw new ContributorProviderException(
            string.IsNullOrEmpty(message)
                ? $"Failed to {operation}: GitHub returned no data."
                : $"Failed to {operation}: {message}");
    }

    private static bool TryGetNextCursor(JsonNode connection, out string? cursor)
    {
        cursor = null;
        if (connection["pageInfo"]?["hasNextPage"]?.GetValue<bool>() != true) return false;
        cursor = connection["pageInfo"]?["endCursor"]?.GetValue<string>();
        return !string.IsNullOrEmpty(cursor);
    }

    private static string Truncate(string value) =>
        value.Length <= 500 ? value : value[..500] + "…";

    // ── Normalization ───────────────────────────────────────────────────────

    /// <summary>
    /// One entry per person who submitted a review. Pending (not yet submitted)
    /// reviews do not count, and neither does the author: GitHub records the
    /// author's replies to review threads as COMMENTED reviews.
    /// </summary>
    public static List<ContributorIdentity> DistinctReviewers(IEnumerable<JsonNode> reviews, string? authorLogin)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(authorLogin))
            seen.Add(authorLogin);

        var reviewers = new List<ContributorIdentity>();
        foreach (var review in reviews)
        {
            var state = review["state"]?.GetValue<string>() ?? string.Empty;
            if (string.Equals(state, "PENDING", StringComparison.OrdinalIgnoreCase)) continue;

            if (ReadIdentity(review["author"]) is not { } reviewer) continue;
            if (!seen.Add(reviewer.Login)) continue;

            reviewers.Add(reviewer);
        }

        return reviewers;
    }

    /// <summary>
    /// Maps GitHub's per-file change types onto the added/edited/deleted counts the
    /// report uses. Renames, copies and mode changes count as edits.
    /// </summary>
    public static FileChangeCounts CountFileChanges(IEnumerable<JsonNode> files)
    {
        var added = 0;
        var edited = 0;
        var deleted = 0;

        foreach (var file in files)
        {
            var changeType = file["changeType"]?.GetValue<string>();
            if (string.IsNullOrEmpty(changeType)) continue;

            switch (changeType.ToUpperInvariant())
            {
                case "ADDED":
                    added++;
                    break;
                case "DELETED":
                    deleted++;
                    break;
                default:
                    edited++;
                    break;
            }
        }

        return new FileChangeCounts(added, edited, deleted);
    }

    private static ContributorIdentity? ReadIdentity(JsonNode? identity)
    {
        // Deleted accounts come back as a null author.
        if (identity is null) return null;
        var login = identity["login"]?.GetValue<string>() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(login)) return null;

        var name = identity["name"]?.GetValue<string>();
        return new ContributorIdentity(
            login,
            string.IsNullOrWhiteSpace(name) ? login : name,
            identity["avatarUrl"]?.GetValue<string>() ?? string.Empty);
    }

    private static string GetPat(UserConfigDao userConfig)
    {
        var pat = userConfig.GetProviderSetting("github", "pat");
        if (string.IsNullOrWhiteSpace(pat))
            throw new ContributorProviderException("GitHub is not configured. Add a key in Settings → Integrations.");
        return pat;
    }

    private HttpClient CreateAuthorizedClient(string pat)
    {
        var client = httpClientFactory.CreateClient("GitHub");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", pat);
        return client;
    }
}
