using Microsoft.Data.Sqlite;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Models;
using TaskFlow.Infrastructure;
using Xunit;

namespace TaskFlow.Tests;

[Trait("Category", "Integration")]
public sealed class RepositoryTests : IAsyncLifetime
{
    private readonly string path = Path.Combine(Path.GetTempPath(), $"taskflow-tests-{Guid.NewGuid()}.db");
    private SqliteDatabase database = null!;
    private User user = null!;
    public async Task InitializeAsync()
    {
        database = new(path);
        await database.Initialize();
        user = new(Guid.NewGuid(), "Test User", "test@example.com", "hashed", DateTimeOffset.UtcNow);
        Assert.True(await new SqliteUserRepository(database).TryAdd(user, default));
    }
    public Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        foreach (var suffix in new[] { "", "-wal", "-shm" }) if (File.Exists(path + suffix)) File.Delete(path + suffix);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Tasks_roundtrip_and_persist_across_repository_instances()
    {
        var repository = new SqliteTaskRepository(database);
        var original = new WorkItem(Guid.NewGuid(), user.Id, "O'Brien's plan", "SQL syntax: '; DROP TABLE Tasks; --", WorkStatus.Todo, new(2026, 11, 1), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        await repository.Add(original, default);
        var reopened = new SqliteTaskRepository(new(path));
        Assert.Equal(original, await reopened.Get(user.Id, original.Id, default));
        Assert.Null(await reopened.Get(Guid.NewGuid(), original.Id, default));
        Assert.Empty(await reopened.List(Guid.NewGuid(), default));
        Assert.False(await reopened.Delete(Guid.NewGuid(), original.Id, default));
        var changed = original with { Status = WorkStatus.Done, DueDate = null };
        Assert.True(await reopened.Update(changed, default));
        Assert.Equal(changed, Assert.Single(await reopened.List(user.Id, default)));
        Assert.True(await reopened.Delete(user.Id, original.Id, default));
        Assert.False(await reopened.Delete(user.Id, original.Id, default));
        Assert.Empty(await reopened.List(user.Id, default));
    }

    [Fact]
    public async Task Email_uniqueness_is_case_insensitive_and_does_not_overwrite_user()
    {
        var repository = new SqliteUserRepository(database);
        Assert.False(await repository.TryAdd(user with { Id = Guid.NewGuid(), Email = "TEST@example.com", Name = "Different User" }, default));
        Assert.Equal(user, await repository.FindByEmail("TEST@example.com", default));
        Assert.Equal(user, await repository.FindById(user.Id, default));
        Assert.Null(await repository.FindById(Guid.NewGuid(), default));
    }

    [Fact]
    public async Task Foreign_key_rejects_a_task_without_a_user()
    {
        var item = new WorkItem(Guid.NewGuid(), Guid.NewGuid(), "Orphan", "", WorkStatus.Todo, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        var error = await Assert.ThrowsAsync<SqliteException>(() => new SqliteTaskRepository(database).Add(item, default));
        Assert.Equal(19, error.SqliteErrorCode);
    }

    [Fact]
    public async Task Initialization_is_repeatable_without_destroying_data()
    {
        await database.Initialize();
        Assert.Equal(user, await new SqliteUserRepository(database).FindById(user.Id, default));
    }
}
