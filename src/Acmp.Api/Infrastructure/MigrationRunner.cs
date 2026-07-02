using Acmp.Modules.Actions.Infrastructure.Persistence;
using Acmp.Modules.Decisions.Infrastructure.Persistence;
using Acmp.Modules.Meetings.Infrastructure.Persistence;
using Acmp.Modules.Membership.Infrastructure.Persistence;
using Acmp.Modules.Notifications.Infrastructure.Persistence;
using Acmp.Modules.Topics.Infrastructure.Persistence;
using Acmp.Shared.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Api.Infrastructure;

// Applies every module's migrations on startup, retrying while SQL Server warms up in Compose.
public static class MigrationRunner
{
    public static async Task MigrateAsync(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        var contexts = new DbContext[]
        {
            scope.ServiceProvider.GetRequiredService<MembershipDbContext>(),
            scope.ServiceProvider.GetRequiredService<TopicsDbContext>(),
            scope.ServiceProvider.GetRequiredService<MeetingsDbContext>(),
            scope.ServiceProvider.GetRequiredService<DecisionsDbContext>(),
            scope.ServiceProvider.GetRequiredService<ActionsDbContext>(),
            scope.ServiceProvider.GetRequiredService<NotificationsDbContext>(),
            scope.ServiceProvider.GetRequiredService<AuditDbContext>(),
        };

        // Non-relational providers (the in-memory store used by integration tests) have no migrations.
        if (contexts.Any(c => !c.Database.IsRelational()))
            return;

        const int maxAttempts = 12;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                foreach (var db in contexts)
                    await db.Database.MigrateAsync();
                logger.LogInformation("Database migrations applied.");
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                logger.LogWarning(ex, "Migration attempt {Attempt}/{Max} failed; retrying in 5s.", attempt, maxAttempts);
                await Task.Delay(TimeSpan.FromSeconds(5));
            }
        }
    }
}
