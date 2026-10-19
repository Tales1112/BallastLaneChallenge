using TaskFlow.Application.Models;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
using TaskFlow.Tests.Integration.Support;

namespace TaskFlow.Tests;

[Trait("Category", "Integration")]
public sealed class ApiTests : IAsyncLifetime
{
    private readonly TestApplication application = new();
    private HttpClient client = null!;
    public async Task InitializeAsync()
    {
        client = application.CreateClient(new() { AllowAutoRedirect = false });
        await RefreshToken(client);
    }
    public async Task DisposeAsync() { client.Dispose(); await application.DisposeAsync(); application.Cleanup(); }

    private static async Task RefreshToken(HttpClient target)
    {
        var response = await target.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        target.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        target.DefaultRequestHeaders.Add("X-CSRF-TOKEN", response.GetProperty("token").GetString());
    }
    private static async Task Register(HttpClient target, string email = "alex@example.com")
    {
        var response = await target.PostAsJsonAsync("/api/auth/register", new RegisterInput("Alex Morgan", email, "Password123!"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
        await RefreshToken(target);
    }

    [Fact]
    public async Task Public_endpoint_is_anonymous_but_tasks_and_me_require_authentication()
    {
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/public")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/tasks")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Full_crud_flow_has_correct_status_codes_and_location()
    {
        await Register(client);
        var response = await client.PostAsJsonAsync("/api/tasks", new TaskInput("Write tests", "Cover the API", "todo", "2026-11-01"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var location = response.Headers.Location;
        var created = await client.GetFromJsonAsync<JsonElement>(location);
        Assert.Equal("Write tests", created.GetProperty("title").GetString());
        Assert.Single(await client.GetFromJsonAsync<JsonElement[]>("/api/tasks") ?? []);
        response = await client.PutAsJsonAsync(location, new TaskInput("Tests written", "", "done", null));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("done", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(location)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(location)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync(location)).StatusCode);
    }

    [Fact]
    public async Task Ownership_is_enforced_on_list_read_update_and_delete()
    {
        await Register(client);
        var created = await client.PostAsJsonAsync("/api/tasks", new TaskInput("Private", "", "todo", null));
        var location = created.Headers.Location!;
        using var other = application.CreateClient(new() { AllowAutoRedirect = false });
        await RefreshToken(other);
        await Register(other, "other@example.com");
        Assert.Empty(await other.GetFromJsonAsync<JsonElement[]>("/api/tasks") ?? []);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(location)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync(location, new TaskInput("Attack", "", "done", null))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync(location)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(location)).StatusCode);
    }

    [Fact]
    public async Task Login_logout_wrong_password_and_duplicate_registration()
    {
        await Register(client);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        await RefreshToken(client);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new LoginInput("alex@example.com", "wrong"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new LoginInput("alex@example.com", "Password123!"))).StatusCode);
        await RefreshToken(client);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/auth/register", new RegisterInput("Alex", "ALEX@example.com", "Password123!"))).StatusCode);
    }

    [Fact]
    public async Task Missing_csrf_token_is_rejected_even_for_registration()
    {
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/register", new RegisterInput("Alex", "alex@example.com", "Password123!"))).StatusCode);
    }

    [Theory]
    [InlineData("", "todo", null)]
    [InlineData("Task", "unknown", null)]
    [InlineData("Task", "todo", "2026-02-30")]
    public async Task Invalid_tasks_return_problem_details_without_writing(string title, string status, string? due)
    {
        await Register(client);
        var response = await client.PostAsJsonAsync("/api/tasks", new TaskInput(title, "", status, due));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Empty(await client.GetFromJsonAsync<JsonElement[]>("/api/tasks") ?? []);
    }

    [Fact]
    public async Task Malformed_json_is_rejected()
    {
        await Register(client);
        var response = await client.PostAsync("/api/tasks", new StringContent("{bad", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Auth_attempts_are_rate_limited()
    {
        for (var i = 0; i < 10; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new LoginInput("missing@example.com", "wrong"))).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync("/api/auth/login", new LoginInput("missing@example.com", "wrong"))).StatusCode);
    }

    [Fact]
    public async Task Mvc_home_and_compiled_assets_are_served()
    {
        var response = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("/app/assets/app.js", await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/app/assets/app.js")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/app/assets/app.css")).StatusCode);
    }

}
