using DevelopmentHub.Api.Models.Dtos;
using DevelopmentHub.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace DevelopmentHub.Api.Controllers;

[ApiController]
[Route("api/pullrequests")]
public class PullRequestsController(IPullRequestService pullRequestService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<PullRequestDto>>> GetOpen()
    {
        var prs = await pullRequestService.GetOpenPullRequestsAsync(HttpContext.RequestAborted);
        return Ok(prs);
    }
}
