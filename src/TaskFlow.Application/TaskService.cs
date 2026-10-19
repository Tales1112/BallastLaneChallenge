using TaskFlow.Application.Interfaces;
using System.Globalization;
using TaskFlow.Application.Models;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Models;

namespace TaskFlow.Application;

public sealed class TaskService(ITaskRepository repository)
{
    public Task<IReadOnlyList<WorkItem>> List(Guid userId, CancellationToken ct) => repository.List(userId, ct);
    public Task<WorkItem?> Get(Guid userId, Guid id, CancellationToken ct) => repository.Get(userId, id, ct);
    public Task<bool> Delete(Guid userId, Guid id, CancellationToken ct) => repository.Delete(userId, id, ct);

    public async Task<WorkItem> Create(Guid userId, TaskInput input, CancellationToken ct)
    {
        var (title, description, status, due) = Validate(input);
        var now = DateTimeOffset.UtcNow;
        var item = new WorkItem(Guid.NewGuid(), userId, title, description, status, due, now, now);
        await repository.Add(item, ct);
        return item;
    }

    public async Task<WorkItem?> Update(Guid userId, Guid id, TaskInput input, CancellationToken ct)
    {
        var existing = await repository.Get(userId, id, ct);
        if (existing is null) return null;
        var (title, description, status, due) = Validate(input);
        var item = existing with { Title = title, Description = description, Status = status, DueDate = due, UpdatedAt = DateTimeOffset.UtcNow };
        return await repository.Update(item, ct) ? item : null;
    }

    private static (string, string, WorkStatus, DateOnly?) Validate(TaskInput input)
    {
        var title = input.Title?.Trim() ?? "";
        var description = input.Description?.Trim() ?? "";
        if (title.Length is < 1 or > 120) throw new ValidationException("Title must contain 1 to 120 characters.");
        if (description.Length > 2000) throw new ValidationException("Description must contain at most 2000 characters.");
        var status = input.Status switch
        {
            "todo" => WorkStatus.Todo,
            "inProgress" => WorkStatus.InProgress,
            "done" => WorkStatus.Done,
            _ => throw new ValidationException("Status must be todo, inProgress or done.")
        };
        DateOnly? due = null;
        if (!string.IsNullOrEmpty(input.DueDate))
        {
            if (!DateOnly.TryParseExact(input.DueDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                throw new ValidationException("Due date must be a valid date in yyyy-MM-dd format.");
            due = parsed;
        }
        return (title, description, status, due);
    }
}
