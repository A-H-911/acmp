using Acmp.Modules.Actions.Infrastructure.Persistence;
using Acmp.Modules.Decisions.Infrastructure.Persistence;
using Acmp.Modules.Dependencies.Infrastructure.Persistence;
using Acmp.Modules.Governance.Infrastructure.Persistence;
using Acmp.Modules.Integrations.Webex.Oauth;
using Acmp.Modules.Meetings.Infrastructure.Persistence;
using Acmp.Modules.Membership.Infrastructure.Persistence;
using Acmp.Modules.Notifications.Infrastructure.Persistence;
using Acmp.Modules.Research.Infrastructure.Persistence;
using Acmp.Modules.Risks.Infrastructure.Persistence;
using Acmp.Modules.Topics.Infrastructure.Persistence;
using Acmp.Modules.Traceability.Infrastructure.Persistence;
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
        var contexts = new List<DbContext>
        {
            scope.ServiceProvider.GetRequiredService<MembershipDbContext>(),
            scope.ServiceProvider.GetRequiredService<TopicsDbContext>(),
            scope.ServiceProvider.GetRequiredService<MeetingsDbContext>(),
            scope.ServiceProvider.GetRequiredService<DecisionsDbContext>(),
            scope.ServiceProvider.GetRequiredService<ActionsDbContext>(),
            scope.ServiceProvider.GetRequiredService<RisksDbContext>(),
            scope.ServiceProvider.GetRequiredService<GovernanceDbContext>(),
            scope.ServiceProvider.GetRequiredService<ResearchDbContext>(),
            scope.ServiceProvider.GetRequiredService<TraceabilityDbContext>(),
            scope.ServiceProvider.GetRequiredService<DependenciesDbContext>(),
            scope.ServiceProvider.GetRequiredService<NotificationsDbContext>(),
            scope.ServiceProvider.GetRequiredService<AuditDbContext>(),
        };

        AddWebexIfPresent(contexts, scope.ServiceProvider);

        // Non-relational providers (the in-memory store used by integration tests) have no migrations.
        if (contexts.Any(c => !c.Database.IsRelational()))
            return;

        await RunAsync(contexts, logger, db => db.Database.MigrateAsync(), d => Task.Delay(d));
    }

    // The Webex OAuth token store is only registered when the adapter is enabled (P13). Split out so the
    // enabled/disabled branch is unit-testable without a WebApplication.
    public static void AddWebexIfPresent(List<DbContext> contexts, IServiceProvider services)
    {
        if (services.GetService<WebexDbContext>() is { } webexDb)
            contexts.Add(webexDb);
    }

    // The retry loop, split out with injectable migrate/delay seams so it is unit-testable without a real
    // SQL Server or real 5s waits (the DI-resolving wrapper above is covered incidentally by the app boot).
    public static async Task RunAsync(IReadOnlyList<DbContext> contexts, ILogger logger,
        Func<DbContext, Task> migrate, Func<TimeSpan, Task> delay, int maxAttempts = 12)
    {
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                foreach (var db in contexts)
                    await migrate(db);
                logger.LogInformation("Database migrations applied.");
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                logger.LogWarning(ex, "Migration attempt {Attempt}/{Max} failed; retrying in 5s.", attempt, maxAttempts);
                await delay(TimeSpan.FromSeconds(5));
            }
        }
    }
}
