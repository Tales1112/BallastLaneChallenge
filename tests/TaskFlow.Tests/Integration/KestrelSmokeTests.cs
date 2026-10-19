using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TaskFlow.Application.Models;
using TaskFlow.Tests.Integration.Support;
using Xunit;

namespace TaskFlow.Tests.Integration;

[Trait("Category", "Integration")]
[Trait("Transport", "Kestrel")]
public sealed class KestrelSmokeTests
{
    [Fact]
    public async Task Real_HTTP_server_serves_the_frontend_and_completes_the_authenticated_CRUD_flow()
    {
        var root = FindSolutionDirectory();
        var output = new DirectoryInfo(AppContext.BaseDirectory);
        var webRoot = Path.Combine(root, "src", "TaskFlow.Web");
        var assembly = Path.Combine(webRoot, "bin", output.Parent!.Name, output.Name, "TaskFlow.Web.dll");
        Assert.True(File.Exists(assembly), $"Build the web project before this test: {assembly}");

        var database = Path.Combine(Path.GetTempPath(), $"taskflow-kestrel-{Guid.NewGuid()}.db");
        var logs = new ConcurrentQueue<string>();
        var listening = new TaskCompletionSource<Uri>(TaskCreationOptions.RunContinuationsAsynchronously);
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = webRoot,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add(assembly);
        start.ArgumentList.Add("--urls");
        start.ArgumentList.Add("http://127.0.0.1:0");
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Testing";
        start.Environment["DOTNET_ENVIRONMENT"] = "Testing";
        start.Environment["Database__Path"] = database;
        start.Environment["SeedDemo"] = "true";
        start.Environment["Logging__LogLevel__Default"] = "Information";

        using var process = new Process { StartInfo = start };
        process.OutputDataReceived += (_, args) =>
        {
            if (args.Data is not { } line) return;
            logs.Enqueue(line);
            const string marker = "Now listening on: ";
            var index = line.IndexOf(marker, StringComparison.Ordinal);
            if (index >= 0 && Uri.TryCreate(line[(index + marker.Length)..].Trim(), UriKind.Absolute, out var address))
                listening.TrySetResult(address);
        };
        process.ErrorDataReceived += (_, args) => { if (args.Data is { } line) logs.Enqueue(line); };

        var started = false;
        try
        {
            started = process.Start();
            Assert.True(started);
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            Uri address;
            try { address = await listening.Task.WaitAsync(TimeSpan.FromSeconds(20)); }
            catch (TimeoutException) { throw new TimeoutException($"Kestrel did not start. {string.Join(Environment.NewLine, logs)}"); }

            using var handler = new HttpClientHandler { CookieContainer = new CookieContainer(), AllowAutoRedirect = false };
            using var client = new HttpClient(handler) { BaseAddress = address, Timeout = TimeSpan.FromSeconds(10) };
            using var home = await client.GetAsync("/");
            Assert.Equal(HttpStatusCode.OK, home.StatusCode);
            Assert.Contains("/app/assets/app.js", await home.Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/app/assets/app.js")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/app/assets/app.css")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/public")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/tasks")).StatusCode);

            await ApiClient.Login(client, "demo@taskflow.local", "DemoPass123!");
            Assert.Equal(6, (await client.GetFromJsonAsync<JsonElement[]>("/api/tasks"))!.Length);
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout", null)).StatusCode);

            await ApiClient.Register(client, "real-http@example.com");
            Assert.Empty((await client.GetFromJsonAsync<JsonElement[]>("/api/tasks"))!);
            var location = await ApiClient.CreateTask(client);
            using var update = await client.PutAsJsonAsync(location, new TaskInput("Real HTTP task", "Saved through Kestrel", "done", null));
            Assert.Equal(HttpStatusCode.OK, update.StatusCode);
            var saved = await client.GetFromJsonAsync<JsonElement>(location);
            Assert.Equal("Real HTTP task", saved.GetProperty("title").GetString());
            Assert.Equal("done", saved.GetProperty("status").GetString());

            using var otherHandler = new HttpClientHandler { CookieContainer = new CookieContainer(), AllowAutoRedirect = false };
            using var other = new HttpClient(otherHandler) { BaseAddress = address, Timeout = TimeSpan.FromSeconds(10) };
            await ApiClient.Register(other, "other-http@example.com");
            Assert.Empty((await other.GetFromJsonAsync<JsonElement[]>("/api/tasks"))!);
            Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(location)).StatusCode);

            Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(location)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(location)).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/tasks")).StatusCode);
            Assert.DoesNotContain(logs, line => line.StartsWith("fail:", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (started && !process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            }
            TestApplication.DeleteDatabase(database);
        }
    }

    private static string FindSolutionDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "TaskFlow.sln"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Could not find TaskFlow.sln above the test output directory.");
    }
}
