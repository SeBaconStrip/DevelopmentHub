using DevelopmentHub.Api.Models;
using DevelopmentHub.Api.Models.Dtos;

namespace DevelopmentHub.Api.Services;

public interface IPullRequestProvider
{
    string ProviderId { get; }
    Task<List<PullRequestDto>> GetOpenPullRequestsAsync(UserConfigDao userConfig, CancellationToken cancellationToken = default);
}
