using Microsoft.Data.Sqlite;

namespace TaskFlow.Infrastructure;

public sealed class SqliteDatabase(string path)
{
    private readonly string connectionString = new SqliteConnectionStringBuilder
    {
        DataSource = path, ForeignKeys = true, DefaultTimeout = 10
    }.ToString();

    public async Task<SqliteConnection> Open(CancellationToken ct)
    {
        var connection = new SqliteConnection(connectionString);
        try { await connection.OpenAsync(ct); return connection; }
        catch { await connection.DisposeAsync(); throw; }
    }

    public async Task Initialize(CancellationToken ct = default)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        Directory.CreateDirectory(directory!);
        await using var connection = await Open(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode=WAL;
            CREATE TABLE IF NOT EXISTS Users (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL CHECK(length(Name) BETWEEN 2 AND 80),
                Email TEXT NOT NULL UNIQUE COLLATE NOCASE,
                PasswordHash TEXT NOT NULL,
                CreatedAt TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS Tasks (
                Id TEXT PRIMARY KEY,
                UserId TEXT NOT NULL REFERENCES Users(Id) ON DELETE CASCADE,
                Title TEXT NOT NULL CHECK(length(Title) BETWEEN 1 AND 120),
                Description TEXT NOT NULL CHECK(length(Description) <= 2000),
                Status INTEGER NOT NULL CHECK(Status IN (0,1,2)),
                DueDate TEXT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_Tasks_UserId_CreatedAt ON Tasks(UserId, CreatedAt DESC);
            """;
        await command.ExecuteNonQueryAsync(ct);
    }
}
