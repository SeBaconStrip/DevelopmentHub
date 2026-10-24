using DevelopmentHub.Api.Models.Dtos;
using DevelopmentHub.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace DevelopmentHub.Api.Controllers;

[ApiController]
[Route("api/contributors")]
public class ContributorsController(IContributorStatsService contributorStatsService) : ControllerBase
{
    [HttpGet("repositories")]
    public async Task<ActionResult<List<RepositoryRefDto>>> GetRepositories([FromQuery] string provider)
    {
        if (string.IsNullOrWhiteSpace(provider))
            return BadRequest(new { error = "Query parameter 'provider' is required." });

        try
        {
            return Ok(await contributorStatsService.ListRepositoriesAsync(provider, HttpContext.RequestAborted));
        }
        catch (ContributorProviderException ex)
        {
            return ProviderError(ex);
        }
    }

    [HttpPost("stats")]
    public async Task<ActionResult<ContributorStatsResultDto>> GetStats([FromBody] ContributorStatsRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Provider))
            return BadRequest(new { error = "'provider' is required." });
        if (request.RepositoryIds is null || request.RepositoryIds.Count == 0)
            return BadRequest(new { error = "At least one repository must be selected." });

        try
        {
            return Ok(await contributorStatsService.GetContributorStatsAsync(request, HttpContext.RequestAborted));
        }
        catch (ContributorProviderException ex)
        {
            return ProviderError(ex);
        }
    }

    [HttpPost("repository-coverage")]
    public async Task<ActionResult<RepositoryCoverageResultDto>> GetRepositoryCoverage(
        [FromBody] RepositoryCoverageRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Provider))
            return BadRequest(new { error = "'provider' is required." });
        if (request.RepositoryIds is null || request.RepositoryIds.Count == 0)
            return BadRequest(new { error = "At least one repository must be selected." });

        try
        {
            return Ok(await contributorStatsService.GetRepositoryCoverageAsync(request, HttpContext.RequestAborted));
        }
        catch (ContributorProviderException ex)
        {
            return ProviderError(ex);
        }
    }

    private ObjectResult ProviderError(ContributorProviderException ex) =>
        StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Message });
}
