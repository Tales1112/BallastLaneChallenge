using TaskFlow.Infrastructure;

namespace TaskFlow.Web.Configuration;

public static class DatabaseConfigurationExtensions
{
    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<SqliteDatabase>().Initialize();

        if (app.Configuration.GetValue<bool>("SeedDemo"))
            await DemoSeed.Run(scope.ServiceProvider);
    }
}
