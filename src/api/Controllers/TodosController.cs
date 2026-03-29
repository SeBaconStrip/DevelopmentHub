using DevelopmentHub.Api.Models.Dtos;
using DevelopmentHub.Api.Services;
using DevelopmentHub.Api.Services.Todos.Sync;
using Microsoft.AspNetCore.Mvc;

namespace DevelopmentHub.Api.Controllers;

[ApiController]
[Route("api/todos")]
public class TodosController(ITodoService todoService, ITodoSyncService syncService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<TodoItemDto>>> GetAll()
    {
        return Ok(await todoService.GetAllAsync());
    }

    [HttpPost]
    public async Task<ActionResult<TodoItemDto>> Create([FromBody] CreateTodoItemRequest request)
    {
        try
        {
            var created = await todoService.CreateAsync(request);
            FireAndForgetSync(created.Id);
            return Ok(created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<TodoItemDto>> Update(string id, [FromBody] UpdateTodoItemRequest request)
    {
        try
        {
            var updated = await todoService.UpdateAsync(id, request);
            if (updated is null) return NotFound();
            FireAndForgetSync(id);
            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPatch("{id}/complete")]
    public async Task<ActionResult<TodoItemDto>> SetCompleted(string id, [FromQuery] bool completed = true)
    {
        var updated = await todoService.SetCompletedAsync(id, completed);
        if (updated is null) return NotFound();
        FireAndForgetSync(id);
        return Ok(updated);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var deleted = await todoService.DeleteAsync(id);
        if (!deleted) return NotFound();
        // Item is already gone locally; run a full sync to propagate the deletion
        FireAndForgetSync(null);
        return NoContent();
    }

    [HttpDelete("completed")]
    public async Task<IActionResult> ClearCompleted()
    {
        var removed = await todoService.ClearCompletedAsync();
        FireAndForgetSync(null);
        return Ok(new { removed });
    }

    private void FireAndForgetSync(string? id) =>
        Task.Run(() => syncService.PushItemAsync(id, CancellationToken.None));
}
