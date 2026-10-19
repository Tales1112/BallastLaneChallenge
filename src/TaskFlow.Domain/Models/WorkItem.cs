using TaskFlow.Domain.Enums;

namespace TaskFlow.Domain.Models
{
    public sealed record WorkItem(
        Guid Id,
        Guid UserId,
        string Title,
        string Description,
        WorkStatus Status,
        DateOnly? DueDate,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);
}
