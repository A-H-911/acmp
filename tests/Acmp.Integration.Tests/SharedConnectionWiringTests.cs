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
}
