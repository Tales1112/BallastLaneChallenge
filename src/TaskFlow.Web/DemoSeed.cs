using TaskFlow.Application.Interfaces;
using TaskFlow.Application;
using TaskFlow.Domain.Models;

namespace TaskFlow.Web;

public static class DemoSeed
{
    public static async Task Run(IServiceProvider services)
    {
        var users = services.GetRequiredService<IUserRepository>();
        var passwords = services.GetRequiredService<IPasswordService>();

        var user = new User(Guid.NewGuid(), "Alex Morgan", "demo@taskflow.local", passwords.Hash("DemoPass123!"), DateTimeOffset.UtcNow);
        if (!await users.TryAdd(user, default)) return;
        var tasks = services.GetRequiredService<TaskService>();
        var today = DateOnly.FromDateTime(DateTime.Today);
        await tasks.Create(user.Id, new("Map out the next product release", "Bring the roadmap, open questions and launch milestones into one clear plan.", "inProgress", today.AddDays(2).ToString("yyyy-MM-dd")), default);
        await tasks.Create(user.Id, new("Review the onboarding experience", "Walk through the first five minutes as a new customer. Capture the small things that could be simpler.", "todo", today.ToString("yyyy-MM-dd")), default);
        await tasks.Create(user.Id, new("Prepare notes for the team check-in", "Share progress, blockers and one thing we learned this week.", "todo", today.AddDays(1).ToString("yyyy-MM-dd")), default);
        await tasks.Create(user.Id, new("Refresh the project documentation", "Update setup instructions and add examples for the new endpoints.", "done", today.AddDays(-1).ToString("yyyy-MM-dd")), default);
        await tasks.Create(user.Id, new("Explore ideas for the next sprint", "A little space for the ideas that have not found their way onto the roadmap yet.", "todo", null), default);
        await tasks.Create(user.Id, new("Follow up on design feedback", "Review the latest comments and close out the remaining decisions.", "inProgress", today.AddDays(-2).ToString("yyyy-MM-dd")), default);
    }
}
