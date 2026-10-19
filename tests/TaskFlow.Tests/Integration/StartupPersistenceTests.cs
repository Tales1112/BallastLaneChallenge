using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using TaskFlow.Application.Models;
using TaskFlow.Tests.Integration.Support;
using Xunit;

namespace TaskFlow.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class StartupPersistenceTests
{
    [Fact]
    public async Task User_credentials_and_task_updates_survive_a_full_host_restart()
    {
        var path = TemporaryDatabase();
        Uri location;
        string userId;
        try
        {
            await using (var first = new TestApplication(path))
            using (var client = ApiClient.Create(first))
            {
                var user = await ApiClient.Register(client);
                userId = user.GetProperty("id").GetString()!;
                location = await ApiClient.CreateTask(client);
                using var update = await client.PutAsJsonAsync(location, new TaskInput("Persistent task", "Saved before restart", "inProgress", "2026-12-31"));
                Assert.Equal(HttpStatusCode.OK, update.StatusCode);
                Assert.True(File.Exists(path));
            }

            await using (var second = new TestApplication(path))
            using (var client = ApiClient.Create(second))
            {
                Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/tasks")).StatusCode);
                var user = await ApiClient.Login(client, "integration@example.com", "Password123!");
                Assert.Equal(userId, user.GetProperty("id").GetString());
                var item = await client.GetFromJsonAsync<JsonElement>(location);
                Assert.Equal("Persistent task", item.GetProperty("title").GetString());
                Assert.Equal("Saved before restart", item.GetProperty("description").GetString());
                Assert.Equal("inProgress", item.GetProperty("status").GetString());
                Assert.Equal("2026-12-31", item.GetProperty("dueDate").GetString());
                Assert.Equal(userId, item.GetProperty("userId").GetString());
                Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(location)).StatusCode);
            }

            await using (var third = new TestApplication(path))
            using (var client = ApiClient.Create(third))
            {
                await ApiClient.Login(client, "integration@example.com", "Password123!");
                Assert.Empty((await client.GetFromJsonAsync<JsonElement[]>("/api/tasks"))!);
                Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(location)).StatusCode);
            }
        }
        finally { TestApplication.DeleteDatabase(path); }
    }

    [Fact]
    public async Task Seeding_is_repeatable_and_preserves_existing_user_changes()
    {
        var path = TemporaryDatabase();
        Uri location;
        try
        {
            await using (var first = new TestApplication(path, seedDemo: true))
            using (var client = ApiClient.Create(first))
            {
                await ApiClient.Login(client, "demo@taskflow.local", "DemoPass123!");
                var tasks = await client.GetFromJsonAsync<JsonElement[]>("/api/tasks");
                Assert.Equal(6, tasks!.Length);
                Assert.Contains(tasks, task => task.GetProperty("status").GetString() == "done");
                location = new Uri($"/api/tasks/{tasks[0].GetProperty("id").GetString()}", UriKind.Relative);
                using var edit = await client.PutAsJsonAsync(location, new TaskInput("Edited demo task", "Keep this edit", "done", null));
                Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
                await ApiClient.CreateTask(client, new("My own task", "", "todo", null));
            }

            await using (var second = new TestApplication(path, seedDemo: true))
            using (var client = ApiClient.Create(second))
            {
                await ApiClient.Login(client, "demo@taskflow.local", "DemoPass123!");
                Assert.Equal(7, (await client.GetFromJsonAsync<JsonElement[]>("/api/tasks"))!.Length);
                var task = await client.GetFromJsonAsync<JsonElement>(location);
                Assert.Equal("Edited demo task", task.GetProperty("title").GetString());
                Assert.Equal("Keep this edit", task.GetProperty("description").GetString());
                Assert.Equal(1L, await Count(path, "Users"));
            }
        }
        finally { TestApplication.DeleteDatabase(path); }
    }

    [Fact]
    public async Task Seed_disabled_creates_an_empty_schema_in_the_configured_database()
    {
        var application = new TestApplication();
        try
        {
            using var client = ApiClient.Create(application);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/public")).StatusCode);
            Assert.True(File.Exists(application.DatabasePath));
            Assert.Equal(0L, await Count(application.DatabasePath, "Users"));
            Assert.Equal(0L, await Count(application.DatabasePath, "Tasks"));
            await ApiClient.RefreshToken(client);
            using var login = await client.PostAsJsonAsync("/api/auth/login", new LoginInput("demo@taskflow.local", "DemoPass123!"));
            Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        }
        finally { await application.DisposeAsync(); application.Cleanup(); }
    }

    [Fact]
    public async Task Separate_hosts_use_separate_database_configuration()
    {
        var first = new TestApplication();
        var second = new TestApplication();
        try
        {
            using var firstClient = ApiClient.Create(first);
            using var secondClient = ApiClient.Create(second);
            var firstUser = await ApiClient.Register(firstClient);
            await ApiClient.CreateTask(firstClient);
            var secondUser = await ApiClient.Register(secondClient);

            Assert.NotEqual(firstUser.GetProperty("id").GetString(), secondUser.GetProperty("id").GetString());
            Assert.Empty((await secondClient.GetFromJsonAsync<JsonElement[]>("/api/tasks"))!);
            Assert.Single((await firstClient.GetFromJsonAsync<JsonElement[]>("/api/tasks"))!);
            Assert.NotEqual(first.DatabasePath, second.DatabasePath);
            Assert.Equal(1L, await Count(first.DatabasePath, "Tasks"));
            Assert.Equal(0L, await Count(second.DatabasePath, "Tasks"));
        }
        finally
        {
            await first.DisposeAsync();
            await second.DisposeAsync();
            first.Cleanup();
            second.Cleanup();
        }
    }

    [Fact]
    public async Task Production_uses_secure_cookies_and_redirects_HTTP_to_HTTPS()
    {
        var application = new TestApplication(environment: "Production");
        try
        {
            using var https = ApiClient.Create(application, https: true);
            await ApiClient.RefreshToken(https);
            using var response = await https.PostAsJsonAsync("/api/auth/register", new RegisterInput("Production User", "production@example.com", "Password123!"));
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var session = Assert.Single(response.Headers.GetValues("Set-Cookie"), x => x.StartsWith("TaskFlow.Session="));
            Assert.Contains("; secure", session, StringComparison.OrdinalIgnoreCase);

            Assert.Equal(HttpStatusCode.OK, (await https.GetAsync("/api/auth/me")).StatusCode);

            // The test host supplies port 443, as a production deployment would.
            using var http = ApiClient.Create(application);
            using var redirect = await http.GetAsync("/api/auth/public");
            Assert.Equal(HttpStatusCode.TemporaryRedirect, redirect.StatusCode);
            Assert.Equal("https://localhost/api/auth/public", redirect.Headers.Location?.ToString());
        }
        finally { await application.DisposeAsync(); application.Cleanup(); }
    }

    private static string TemporaryDatabase() => Path.Combine(Path.GetTempPath(), $"taskflow-restart-{Guid.NewGuid()}.db");

    private static async Task<long> Count(string path, string table)
    {
        if (table is not ("Users" or "Tasks")) throw new ArgumentOutOfRangeException(nameof(table));
        await using var connection = new SqliteConnection($"Data Source={path}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {table}";
        return (long)(await command.ExecuteScalarAsync())!;
    }
}
