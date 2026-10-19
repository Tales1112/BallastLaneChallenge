using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TaskFlow.Application.Models;
using TaskFlow.Tests.Integration.Support;
using Xunit;

namespace TaskFlow.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class TaskWorkflowTests : IntegrationTestBase
{
    [Fact]
    public async Task Invalid_update_preserves_the_existing_task()
    {
        await ApiClient.Register(Client);
        var location = await ApiClient.CreateTask(Client);
        var original = await Client.GetStringAsync(location);

        using var response = await Client.PutAsJsonAsync(location, new TaskInput("", "Changed", "done", null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(original, await Client.GetStringAsync(location));
    }

    [Theory]
    [InlineData(120, 2000, HttpStatusCode.Created)]
    [InlineData(121, 2000, HttpStatusCode.BadRequest)]
    [InlineData(120, 2001, HttpStatusCode.BadRequest)]
    public async Task Field_length_boundaries_are_enforced_through_HTTP(int titleLength, int descriptionLength, HttpStatusCode expected)
    {
        await ApiClient.Register(Client);
        using var response = await Client.PostAsJsonAsync("/api/tasks",
            new TaskInput(new string('T', titleLength), new string('D', descriptionLength), "todo", null));

        Assert.Equal(expected, response.StatusCode);
        var items = await Client.GetFromJsonAsync<JsonElement[]>("/api/tasks");
        Assert.Equal(expected == HttpStatusCode.Created ? 1 : 0, items!.Length);
    }

    [Fact]
    public async Task Optional_fields_dates_and_all_statuses_roundtrip_without_changing_identity()
    {
        var user = await ApiClient.Register(Client);
        var location = await ApiClient.CreateTask(Client, new("  O'Brien's plan  ", "'; DROP TABLE Tasks; --", "todo", "2020-01-02"));
        var original = await Client.GetFromJsonAsync<JsonElement>(location);
        Assert.Equal("O'Brien's plan", original.GetProperty("title").GetString());
        Assert.Equal("'; DROP TABLE Tasks; --", original.GetProperty("description").GetString());
        Assert.Equal("2020-01-02", original.GetProperty("dueDate").GetString());
        Assert.Equal(user.GetProperty("id").GetString(), original.GetProperty("userId").GetString());

        foreach (var status in new[] { "inProgress", "done", "todo" })
        {
            using var response = await Client.PutAsJsonAsync(location, new TaskInput("Updated", null, status, null));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var changed = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(status, changed.GetProperty("status").GetString());
            Assert.Equal("", changed.GetProperty("description").GetString());
            Assert.Equal(JsonValueKind.Null, changed.GetProperty("dueDate").ValueKind);
            Assert.Equal(original.GetProperty("id").GetString(), changed.GetProperty("id").GetString());
            Assert.Equal(original.GetProperty("createdAt").GetString(), changed.GetProperty("createdAt").GetString());
            Assert.True(changed.GetProperty("updatedAt").GetDateTimeOffset() >= original.GetProperty("createdAt").GetDateTimeOffset());
        }

        Assert.Single((await Client.GetFromJsonAsync<JsonElement[]>("/api/tasks"))!);
    }

    [Fact]
    public async Task Request_body_cannot_assign_a_task_to_another_user()
    {
        var user = await ApiClient.Register(Client);
        var injectedOwner = Guid.NewGuid();
        using var response = await Client.PostAsJsonAsync("/api/tasks", new
        {
            title = "Owner injection attempt", description = "", status = "todo", dueDate = (string?)null,
            userId = injectedOwner
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var task = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(user.GetProperty("id").GetString(), task.GetProperty("userId").GetString());
        Assert.NotEqual(injectedOwner.ToString(), task.GetProperty("userId").GetString());
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task Missing_task_returns_404(string method)
    {
        await ApiClient.Register(Client);
        using var request = new HttpRequestMessage(new HttpMethod(method), $"/api/tasks/{Guid.NewGuid()}");
        if (method == "PUT") request.Content = JsonContent.Create(new TaskInput("Valid task", "", "todo", null));
        using var response = await Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("POST", "/api/tasks")]
    [InlineData("PUT", "/api/tasks/11111111-1111-1111-1111-111111111111")]
    [InlineData("DELETE", "/api/tasks/11111111-1111-1111-1111-111111111111")]
    public async Task Anonymous_mutations_are_rejected_without_redirecting_to_HTML(string method, string path)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method != "DELETE") request.Content = JsonContent.Create(new TaskInput("Valid task", "", "todo", null));
        using var response = await Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }
}
