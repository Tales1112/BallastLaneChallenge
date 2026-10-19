using Xunit;

namespace TaskFlow.Tests.Integration.Support;

public abstract class IntegrationTestBase : IAsyncLifetime
{
    private protected TestApplication Application { get; } = new();
    protected HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Client = ApiClient.Create(Application);
        await ApiClient.RefreshToken(Client);
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();
        await Application.DisposeAsync();
        Application.Cleanup();
    }
}
