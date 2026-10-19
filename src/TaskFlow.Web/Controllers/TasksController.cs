using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Application;
using TaskFlow.Application.Models;

namespace TaskFlow.Web.Controllers;

[ApiController, Authorize, Route("api/tasks")]
public sealed class TasksController(TaskService service) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await service.List(UserId, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var item = await service.Get(UserId, id, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create(TaskInput input, CancellationToken ct)
    {
        var item = await service.Create(UserId, input, ct);
        return CreatedAtAction(nameof(Get), new { id = item.Id }, item);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, TaskInput input, CancellationToken ct)
    {
        var item = await service.Update(UserId, id, input, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) => await service.Delete(UserId, id, ct) ? NoContent() : NotFound();
}
