using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TaskFlow.Application.Models;
using Xunit;

namespace TaskFlow.Tests.Integration.Support;

internal static class ApiClient
{
    public static HttpClient Create(TestApplication application, bool https = false) =>
        application.CreateClient(new()
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri(https ? "https://localhost" : "http://localhost")
        });

    public static async Task<string> RefreshToken(HttpClient client)
    {
        using var response = await client.GetAsync("/api/auth/csrf");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var token = json.GetProperty("token").GetString()!;
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token);
        return token;
    }

    public static async Task<JsonElement> Register(HttpClient client, string email = "integration@example.com")
    {
        await RefreshToken(client);
        using var response = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterInput("Integration User", email, "Password123!"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        await RefreshToken(client);
        return json;
    }

    public static async Task<JsonElement> Login(HttpClient client, string email, string password)
    {
        await RefreshToken(client);
        using var response = await client.PostAsJsonAsync("/api/auth/login", new LoginInput(email, password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        await RefreshToken(client);
        return json;
    }

    public static async Task<Uri> CreateTask(HttpClient client, TaskInput? input = null)
    {
        using var response = await client.PostAsJsonAsync("/api/tasks",
            input ?? new TaskInput("Integration task", "Original description", "todo", "2026-11-01"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        return response.Headers.Location;
    }
}
