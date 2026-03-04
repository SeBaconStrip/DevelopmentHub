using DevelopmentHub.Api.Models.Dtos;
using DevelopmentHub.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace DevelopmentHub.Api.Controllers;

[ApiController]
[Route("api/repositories")]
public class RepositoriesController(IRepositoryService repositoryService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<RepositoryDto>>> GetAll()
    {
        return Ok(await repositoryService.GetAllAsync());
    }

    [HttpPost("scan")]
    public async Task<ActionResult<List<RepositoryDto>>> Scan()
    {
        var result = await repositoryService.ScanAsync();
        return Ok(result);
    }

    [HttpPatch("{id}/favorite")]
    public async Task<ActionResult<RepositoryDto>> ToggleFavorite(string id)
    {
        var result = await repositoryService.ToggleFavoriteAsync(id);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{id}/open")]
    public async Task<ActionResult<RepositoryDto>> Open(string id, [FromBody] OpenRepositoryRequest request)
    {
        var result = await repositoryService.OpenAsync(id, request);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{id}/sync")]
    public async Task<IActionResult> Sync(string id, CancellationToken cancellationToken)
    {
        var (success, output) = await repositoryService.SyncAsync(id, cancellationToken);
        return Ok(new { success, output });
    }
}
