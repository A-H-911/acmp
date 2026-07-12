using System.Data.Common;
using Acmp.Modules.Actions.Infrastructure;
using Acmp.Modules.Actions.Infrastructure.Persistence;
using Acmp.Modules.Decisions.Infrastructure;
using Acmp.Modules.Decisions.Infrastructure.Persistence;
using Acmp.Modules.Meetings.Infrastructure;
using Acmp.Modules.Meetings.Infrastructure.Persistence;
using Acmp.Modules.Membership.Infrastructure;
using Acmp.Modules.Membership.Infrastructure.Persistence;
using Acmp.Modules.Notifications.Infrastructure;
using Acmp.Modules.Notifications.Infrastructure.Persistence;
using Acmp.Modules.Risks.Infrastructure;
using Acmp.Modules.Risks.Infrastructure.Persistence;
using Acmp.Modules.Topics.Infrastructure;
using Acmp.Modules.Topics.Infrastructure.Persistence;
using Acmp.Shared;
using Acmp.Shared.Infrastructure.Audit;
using Acmp.Shared.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Acmp.Integration.Tests;

// NFR-042 (ADR-0026). The production DI wiring must place every module DbContext and the AuditDbContext on the
// SAME per-scope DbConnection — that shared connection is what lets a command's state change and its audit
// append commit on one local transaction. This resolves each context through the real module registrations
// (executing their AddDbContext option lambdas) and asserts the invariant. No container/DB needed: building a
// context runs its options factory and exposes its DbConnection without opening it.
public sealed class SharedConnectionWiringTests
{
    [Fact]
    public void Every_module_and_audit_context_is_built_on_the_one_shared_connection()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Acmp"] = "Server=unused;Database=Acmp;TrustServerCertificate=True",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSharedKernel(config);
        services.AddMembershipModule(config);
        services.AddTopicsModule(config);
        services.AddMeetingsModule(config);
        services.AddDecisionsModule(config);
        services.AddActionsModule(config);
        services.AddRisksModule(config);
        services.AddNotificationsModule(config);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;

        var shared = sp.GetRequiredService<DbConnection>();
        DbContext[] contexts =
        {
            sp.GetRequiredService<MembershipDbContext>(),
            sp.GetRequiredService<TopicsDbContext>(),
            sp.GetRequiredService<MeetingsDbContext>(),
            sp.GetRequiredService<DecisionsDbContext>(),
            sp.GetRequiredService<ActionsDbContext>(),
            sp.GetRequiredService<RisksDbContext>(),
            sp.GetRequiredService<NotificationsDbContext>(),
            sp.GetRequiredService<AuditDbContext>(),
        };

        foreach (var context in contexts)
            context.Database.GetDbConnection().Should().BeSameAs(shared,
                $"{context.GetType().Name} must be wired onto the shared ambient connection");
    }

    [Fact]
    public void Shared_connection_persists_security_info_so_fresh_db_creation_keeps_its_password()
    {
        // Regression (2026-07-12). A shared SqlConnection *instance* masks its password out of
        // SqlConnection.ConnectionString once it has been opened (Microsoft.Data.SqlClient default,
        // PersistSecurityInfo=false). On a first boot against an EMPTY database, EF's
        // SqlServerDatabaseCreator derives a `master` connection from THIS instance's (then
        // password-less) ConnectionString to CREATE the DB -> "Login failed for user 'sa'" (18456),
        // so the whole stack never starts. PersistSecurityInfo=true keeps the password so the derived
        // connection authenticates. Neither the EF-InMemory API host nor the Testcontainers suite
        // reproduced the exact interceptor-open + fresh-DB timing; only the full-stack `.env.example`
        // boot did. This one-file guard is far cheaper than that boot.
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Acmp"] = "Server=unused;Database=Acmp;User Id=sa;Password=Secret#2026;TrustServerCertificate=True",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSharedKernel(config);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var shared = scope.ServiceProvider.GetRequiredService<DbConnection>();

        new SqlConnectionStringBuilder(shared.ConnectionString).PersistSecurityInfo
            .Should().BeTrue("EF derives a master connection from the shared instance's ConnectionString "
                + "during fresh-DB creation; without PersistSecurityInfo the password is masked post-open (18456 at boot)");
    }
}
