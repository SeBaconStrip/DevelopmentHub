using DevelopmentHub.Api.Models.Dtos;
using DevelopmentHub.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace DevelopmentHub.Api.Controllers;

[ApiController]
[Route("api/todos")]
public class TodosController(ITodoService todoService) : ControllerBase
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
            return updated is null ? NotFound() : Ok(updated);
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
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        return await todoService.DeleteAsync(id) ? NoContent() : NotFound();
    }

    [HttpDelete("completed")]
    public async Task<IActionResult> ClearCompleted()
    {
        var removed = await todoService.ClearCompletedAsync();
        return Ok(new { removed });
    }
}
