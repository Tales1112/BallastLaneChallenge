using TaskFlow.Domain.Models;

namespace TaskFlow.Application.Interfaces;

public interface ITaskRepository
{
    Task<IReadOnlyList<WorkItem>> List(Guid userId, CancellationToken ct);
    Task<WorkItem?> Get(Guid userId, Guid id, CancellationToken ct);
    Task Add(WorkItem item, CancellationToken ct);
    Task<bool> Update(WorkItem item, CancellationToken ct);
    Task<bool> Delete(Guid userId, Guid id, CancellationToken ct);
}
