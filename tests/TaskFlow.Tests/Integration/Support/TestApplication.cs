using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace TaskFlow.Tests.Integration.Support;

internal sealed class TestApplication : WebApplicationFactory<Program>
{
    private readonly bool ownsDatabase;
    private readonly bool seedDemo;
    private readonly string environment;

    public TestApplication(string? databasePath = null, bool seedDemo = false, string environment = "Testing")
    {
        ownsDatabase = databasePath is null;
        DatabasePath = databasePath ?? Path.Combine(Path.GetTempPath(), $"taskflow-api-{Guid.NewGuid()}.db");
        this.seedDemo = seedDemo;
        this.environment = environment;
    }

    public string DatabasePath { get; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Path"] = DatabasePath,
                ["SeedDemo"] = seedDemo.ToString(),
                ["HTTPS_PORT"] = environment == "Production" ? "443" : null,
                ["Logging:LogLevel:Default"] = "Warning"
            }));
    }

    public void Cleanup()
    {
        if (ownsDatabase) DeleteDatabase(DatabasePath);
    }

    public static void DeleteDatabase(string path)
    {
        SqliteConnection.ClearAllPools();
        foreach (var suffix in new[] { "", "-wal", "-shm" })
            if (File.Exists(path + suffix)) File.Delete(path + suffix);
    }
}
