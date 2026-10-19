using TaskFlow.Application.Interfaces;
using TaskFlow.Application;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Models;
using Xunit;

namespace TaskFlow.Tests;

public sealed class TaskServiceTests
{
    private readonly MemoryTasks repository = new();
    private readonly Guid owner = Guid.NewGuid();

    [Fact]
    public async Task Create_trims_input_and_assigns_owner()
    {
        var service = new TaskService(repository);
        var task = await service.Create(owner, new("  Ship release  ", "  Review changes  ", "todo", "2026-10-31"), default);
        Assert.Equal("Ship release", task.Title);
        Assert.Equal("Review changes", task.Description);
        Assert.Equal(owner, task.UserId);
        Assert.NotEqual(Guid.Empty, task.Id);
        Assert.Single(repository.Items);
    }

    [Theory]
    [InlineData("", "todo", "2026-10-31")]
    [InlineData("  ", "todo", "2026-10-31")]
    [InlineData("Task", "invalid", "2026-10-31")]
    [InlineData("Task", "todo", "2026-02-30")]
    public async Task Invalid_input_is_rejected_before_writing(string title, string status, string due)
    {
        var service = new TaskService(repository);
        await Assert.ThrowsAsync<ValidationException>(() => service.Create(owner, new(title, "", status, due), default));
        Assert.Empty(repository.Items);
    }

    [Fact]
    public async Task Other_users_cannot_read_update_or_delete_a_task()
    {
        var service = new TaskService(repository);
        var task = await service.Create(owner, new("Private", "", "todo", null), default);
        var stranger = Guid.NewGuid();
        Assert.Null(await service.Get(stranger, task.Id, default));
        Assert.Null(await service.Update(stranger, task.Id, new("Stolen", "", "done", null), default));
        Assert.False(await service.Delete(stranger, task.Id, default));
        Assert.Equal("Private", repository.Items.Single().Title);
    }

    [Fact]
    public async Task Update_preserves_identity_and_creation_time()
    {
        var service = new TaskService(repository);
        var original = await service.Create(owner, new("Draft", "", "todo", null), default);
        var updated = await service.Update(owner, original.Id, new("Published", "Ready", "done", null), default);
        Assert.NotNull(updated);
        Assert.Equal(original.Id, updated.Id);
        Assert.Equal(original.CreatedAt, updated.CreatedAt);
        Assert.Equal(WorkStatus.Done, updated.Status);
    }

    private sealed class MemoryTasks : ITaskRepository
    {
        public List<WorkItem> Items { get; } = [];
        public Task<IReadOnlyList<WorkItem>> List(Guid userId, CancellationToken ct) => Task.FromResult<IReadOnlyList<WorkItem>>(Items.Where(x => x.UserId == userId).ToList());
        public Task<WorkItem?> Get(Guid userId, Guid id, CancellationToken ct) => Task.FromResult(Items.Find(x => x.Id == id && x.UserId == userId));
        public Task Add(WorkItem item, CancellationToken ct) { Items.Add(item); return Task.CompletedTask; }
        public Task<bool> Update(WorkItem item, CancellationToken ct) { var i = Items.FindIndex(x => x.Id == item.Id && x.UserId == item.UserId); if (i < 0) return Task.FromResult(false); Items[i] = item; return Task.FromResult(true); }
        public Task<bool> Delete(Guid userId, Guid id, CancellationToken ct) => Task.FromResult(Items.RemoveAll(x => x.Id == id && x.UserId == userId) > 0);
    }
}
