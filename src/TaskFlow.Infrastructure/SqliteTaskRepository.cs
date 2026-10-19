using Microsoft.Data.Sqlite;
using System.Globalization;
using TaskFlow.Application.Interfaces;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Models;

namespace TaskFlow.Infrastructure;

public sealed class SqliteTaskRepository(SqliteDatabase database) : ITaskRepository
{
    public async Task<IReadOnlyList<WorkItem>> List(Guid userId, CancellationToken ct)
    {
        await using var connection = await database.Open(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Tasks WHERE UserId=$user ORDER BY CreatedAt DESC, Id";
        command.Parameters.AddWithValue("$user", userId.ToString());
        await using var reader = await command.ExecuteReaderAsync(ct);
        var items = new List<WorkItem>();
        while (await reader.ReadAsync(ct)) items.Add(Read(reader));
        return items;
    }

    public async Task<WorkItem?> Get(Guid userId, Guid id, CancellationToken ct)
    {
        await using var connection = await database.Open(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Tasks WHERE Id=$id AND UserId=$user";
        command.Parameters.AddWithValue("$id", id.ToString());
        command.Parameters.AddWithValue("$user", userId.ToString());
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? Read(reader) : null;
    }

    public async Task Add(WorkItem item, CancellationToken ct)
    {
        await using var connection = await database.Open(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO Tasks (Id,UserId,Title,Description,Status,DueDate,CreatedAt,UpdatedAt) VALUES ($id,$user,$title,$description,$status,$due,$created,$updated)";
        Bind(command, item);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<bool> Update(WorkItem item, CancellationToken ct)
    {
        await using var connection = await database.Open(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Tasks SET Title=$title,Description=$description,Status=$status,DueDate=$due,UpdatedAt=$updated WHERE Id=$id AND UserId=$user";
        Bind(command, item);
        return await command.ExecuteNonQueryAsync(ct) == 1;
    }

    public async Task<bool> Delete(Guid userId, Guid id, CancellationToken ct)
    {
        await using var connection = await database.Open(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Tasks WHERE Id=$id AND UserId=$user";
        command.Parameters.AddWithValue("$id", id.ToString());
        command.Parameters.AddWithValue("$user", userId.ToString());
        return await command.ExecuteNonQueryAsync(ct) == 1;
    }

    private static void Bind(SqliteCommand command, WorkItem item)
    {
        command.Parameters.AddWithValue("$id", item.Id.ToString());
        command.Parameters.AddWithValue("$user", item.UserId.ToString());
        command.Parameters.AddWithValue("$title", item.Title);
        command.Parameters.AddWithValue("$description", item.Description);
        command.Parameters.AddWithValue("$status", (int)item.Status);
        command.Parameters.AddWithValue("$due", (object?)item.DueDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? DBNull.Value);
        command.Parameters.AddWithValue("$created", item.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$updated", item.UpdatedAt.ToString("O"));
    }

    private static WorkItem Read(SqliteDataReader r) => new(
        Guid.Parse(r.GetString(0)), Guid.Parse(r.GetString(1)), r.GetString(2), r.GetString(3),
        (WorkStatus)r.GetInt32(4), r.IsDBNull(5) ? null : DateOnly.ParseExact(r.GetString(5), "yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTimeOffset.Parse(r.GetString(6), CultureInfo.InvariantCulture), DateTimeOffset.Parse(r.GetString(7), CultureInfo.InvariantCulture));
}
