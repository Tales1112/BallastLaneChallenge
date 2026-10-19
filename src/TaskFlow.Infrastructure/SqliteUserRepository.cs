using System.Globalization;
using TaskFlow.Application.Interfaces;
using TaskFlow.Domain.Models;

namespace TaskFlow.Infrastructure;

public sealed class SqliteUserRepository(SqliteDatabase database) : IUserRepository
{
    public Task<User?> FindByEmail(string email, CancellationToken ct) => Find("Email", email, ct);
    public Task<User?> FindById(Guid id, CancellationToken ct) => Find("Id", id.ToString(), ct);

    private async Task<User?> Find(string column, string value, CancellationToken ct)
    {
        await using var connection = await database.Open(ct);
        await using var command = connection.CreateCommand();

        command.CommandText = $"SELECT Id,Name,Email,PasswordHash,CreatedAt FROM Users WHERE {column}=$value";
        command.Parameters.AddWithValue("$value", value);
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct)
            ? new User(Guid.Parse(reader.GetString(0)), reader.GetString(1), reader.GetString(2), reader.GetString(3), DateTimeOffset.Parse(reader.GetString(4), CultureInfo.InvariantCulture))
            : null;
    }

    public async Task<bool> TryAdd(User user, CancellationToken ct)
    {
        await using var connection = await database.Open(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO Users (Id,Name,Email,PasswordHash,CreatedAt) VALUES ($id,$name,$email,$hash,$created) ON CONFLICT(Email) DO NOTHING";
        command.Parameters.AddWithValue("$id", user.Id.ToString());
        command.Parameters.AddWithValue("$name", user.Name);
        command.Parameters.AddWithValue("$email", user.Email);
        command.Parameters.AddWithValue("$hash", user.PasswordHash);
        command.Parameters.AddWithValue("$created", user.CreatedAt.ToString("O"));
        return await command.ExecuteNonQueryAsync(ct) == 1;
    }
}
