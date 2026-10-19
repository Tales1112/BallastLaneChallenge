using TaskFlow.Application;
using TaskFlow.Application.Interfaces;
using TaskFlow.Infrastructure;

namespace TaskFlow.Web.Configuration;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<TaskService>();
        services.AddScoped<AuthService>();

        return services;
    }

    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddSingleton(provider =>
        {
            var configuration = provider.GetRequiredService<IConfiguration>();
            var environment = provider.GetRequiredService<IWebHostEnvironment>();
            var path = configuration["Database:Path"] ?? "App_Data/taskflow.db";

            if (!Path.IsPathRooted(path))
                path = Path.Combine(environment.ContentRootPath, path);

            return new SqliteDatabase(path);
        });

        services.AddScoped<ITaskRepository, SqliteTaskRepository>();
        services.AddScoped<IUserRepository, SqliteUserRepository>();
        services.AddSingleton<IPasswordService, PasswordService>();

        return services;
    }
}
