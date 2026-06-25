using Acmp.Modules.Membership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Api.Infrastructure;

// Applies module migrations on startup, retrying while SQL Server warms up in Compose.
public static class MigrationRunner
{
    public static async Task MigrateAsync(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        var db = scope.ServiceProvider.GetRequiredService<MembershipDbContext>();

        const int maxAttempts = 12;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
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
