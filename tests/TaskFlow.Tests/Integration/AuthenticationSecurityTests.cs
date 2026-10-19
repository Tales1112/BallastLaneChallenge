using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Application.Interfaces;
using TaskFlow.Application.Models;
using TaskFlow.Tests.Integration.Support;
using Xunit;

namespace TaskFlow.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class AuthenticationSecurityTests : IntegrationTestBase
{
    [Theory]
    [InlineData("A", "valid@example.com", "Password123!")]
    [InlineData("Valid User", "invalid-email", "Password123!")]
    [InlineData("Valid User", "valid@example.com", "short1")]
    [InlineData("Valid User", "valid@example.com", "onlyletterslong")]
    [InlineData("Valid User", "valid@example.com", "12345678901")]
    public async Task Invalid_registration_neither_creates_a_user_nor_signs_in(string name, string email, string password)
    {
        using var response = await Client.PostAsJsonAsync("/api/auth/register", new RegisterInput(name, email, password));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.False(response.Headers.TryGetValues("Set-Cookie", out var cookies) && cookies.Any(x => x.StartsWith("TaskFlow.Session=")));
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.GetAsync("/api/auth/me")).StatusCode);

        using var scope = Application.Services.CreateScope();
        Assert.Null(await scope.ServiceProvider.GetRequiredService<IUserRepository>().FindByEmail(email, default));
    }

    [Theory]
    [InlineData("create", false)]
    [InlineData("update", false)]
    [InlineData("delete", false)]
    [InlineData("logout", false)]
    [InlineData("create", true)]
    [InlineData("update", true)]
    [InlineData("delete", true)]
    [InlineData("logout", true)]
    public async Task Missing_or_tampered_CSRF_tokens_cannot_mutate_state(string operation, bool tampered)
    {
        await ApiClient.Register(Client);
        var location = await ApiClient.CreateTask(Client);
        var original = await Client.GetStringAsync(location);
        Client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        if (tampered) Client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", "tampered-token");

        using var response = operation switch
        {
            "create" => await Client.PostAsJsonAsync("/api/tasks", new TaskInput("Extra task", "", "todo", null)),
            "update" => await Client.PutAsJsonAsync(location, new TaskInput("Changed", "", "done", null)),
            "delete" => await Client.DeleteAsync(location),
            _ => await Client.PostAsync("/api/auth/logout", null)
        };

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(original, await Client.GetStringAsync(location));
        Assert.Single((await Client.GetFromJsonAsync<JsonElement[]>("/api/tasks"))!);
        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task CSRF_token_from_a_different_browser_session_is_rejected()
    {
        await ApiClient.Register(Client);
        using var other = ApiClient.Create(Application);
        var foreignToken = await ApiClient.RefreshToken(other);
        Client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", foreignToken);

        using var response = await Client.PostAsJsonAsync("/api/tasks", new TaskInput("Not created", "", "todo", null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty((await Client.GetFromJsonAsync<JsonElement[]>("/api/tasks"))!);
    }

    [Fact]
    public async Task Identity_changes_require_a_fresh_CSRF_token()
    {
        var anonymousToken = await ApiClient.RefreshToken(Client);
        await ApiClient.Register(Client);
        Client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", anonymousToken);
        using var rejected = await Client.PostAsJsonAsync("/api/tasks", new TaskInput("Stale token", "", "todo", null));
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);

        var authenticatedToken = await ApiClient.RefreshToken(Client);
        await ApiClient.CreateTask(Client);
        Assert.Equal(HttpStatusCode.NoContent, (await Client.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.GetAsync("/api/tasks")).StatusCode);

        Client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", authenticatedToken);
        using var staleLogin = await Client.PostAsJsonAsync("/api/auth/login", new LoginInput("integration@example.com", "Password123!"));
        Assert.Equal(HttpStatusCode.BadRequest, staleLogin.StatusCode);
        await ApiClient.Login(Client, "integration@example.com", "Password123!");
        Assert.Single((await Client.GetFromJsonAsync<JsonElement[]>("/api/tasks"))!);
    }

    [Fact]
    public async Task Concurrent_duplicate_registration_creates_exactly_one_account()
    {
        using var other = ApiClient.Create(Application);
        await ApiClient.RefreshToken(other);
        var responses = await Task.WhenAll(
            Client.PostAsJsonAsync("/api/auth/register", new RegisterInput("First User", "Race@Example.com", "FirstPassword123!")),
            other.PostAsJsonAsync("/api/auth/register", new RegisterInput("Second User", "race@example.com", "SecondPassword123!")));
        try
        {
            Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Created);
            Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
            var winner = await responses.Single(x => x.StatusCode == HttpStatusCode.Created).Content.ReadFromJsonAsync<JsonElement>();

            using var scope = Application.Services.CreateScope();
            var stored = await scope.ServiceProvider.GetRequiredService<IUserRepository>().FindByEmail("race@example.com", default);
            Assert.NotNull(stored);
            Assert.Equal(winner.GetProperty("id").GetString(), stored.Id.ToString());
            Assert.Equal(winner.GetProperty("name").GetString(), stored.Name);
            var verifier = scope.ServiceProvider.GetRequiredService<IPasswordService>();
            Assert.True(verifier.Verify(stored.PasswordHash, stored.Name == "First User" ? "FirstPassword123!" : "SecondPassword123!"));
            Assert.DoesNotContain("password", winner.GetRawText(), StringComparison.OrdinalIgnoreCase);
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [Fact]
    public async Task Security_headers_session_cookie_and_API_cache_policy_are_applied()
    {
        using var home = await Client.GetAsync("/");
        Assert.Equal("nosniff", Assert.Single(home.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("DENY", Assert.Single(home.Headers.GetValues("X-Frame-Options")));
        Assert.Contains("frame-ancestors 'none'", Assert.Single(home.Headers.GetValues("Content-Security-Policy")));

        using var response = await Client.PostAsJsonAsync("/api/auth/register", new RegisterInput("Cookie User", "cookie@example.com", "Password123!"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var session = Assert.Single(response.Headers.GetValues("Set-Cookie"), x => x.StartsWith("TaskFlow.Session="));
        Assert.Contains("httponly", session, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", session, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("expires=", session, StringComparison.OrdinalIgnoreCase);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.True(response.Headers.CacheControl?.NoCache);
    }
}
