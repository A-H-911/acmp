using Acmp.Modules.Actions.Infrastructure.Persistence;
using Acmp.Modules.Decisions.Infrastructure.Persistence;
using Acmp.Modules.Dependencies.Infrastructure.Persistence;
using Acmp.Modules.Governance.Infrastructure.Persistence;
using Acmp.Modules.Meetings.Infrastructure.Persistence;
using Acmp.Modules.Membership.Domain;
using Acmp.Modules.Membership.Domain.Enums;
using Acmp.Modules.Membership.Infrastructure.Persistence;
using Acmp.Modules.Notifications.Infrastructure.Persistence;
using Acmp.Modules.Research.Infrastructure.Persistence;
using Acmp.Modules.Risks.Infrastructure.Persistence;
using Acmp.Modules.Topics.Infrastructure.Persistence;
using Acmp.Modules.Traceability.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Infrastructure.Audit;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Acmp.Api.Tests;

// Boots the real API host with two test swaps: the Membership DbContext points at a private
// in-memory store, and authentication uses the header-driven TestAuthHandler instead of Keycloak.
// Everything else (the MediatR pipeline, policy authorization, Problem Details) runs unchanged.
public sealed class AcmpWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = "acmp-it-" + Guid.NewGuid();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<MembershipDbContext>>();
            services.RemoveAll<MembershipDbContext>();
            services.AddDbContext<MembershipDbContext>(o => o.UseInMemoryDatabase(_dbName));

            services.RemoveAll<DbContextOptions<TopicsDbContext>>();
            services.RemoveAll<TopicsDbContext>();
            services.AddDbContext<TopicsDbContext>(o => o.UseInMemoryDatabase(_dbName + "-topics"));

            services.RemoveAll<DbContextOptions<MeetingsDbContext>>();
            services.RemoveAll<MeetingsDbContext>();
            services.AddDbContext<MeetingsDbContext>(o => o.UseInMemoryDatabase(_dbName + "-meetings"));

            services.RemoveAll<DbContextOptions<DecisionsDbContext>>();
            services.RemoveAll<DecisionsDbContext>();
            services.AddDbContext<DecisionsDbContext>(o => o.UseInMemoryDatabase(_dbName + "-decisions"));

            services.RemoveAll<DbContextOptions<ActionsDbContext>>();
            services.RemoveAll<ActionsDbContext>();
            services.AddDbContext<ActionsDbContext>(o => o.UseInMemoryDatabase(_dbName + "-actions"));

            services.RemoveAll<DbContextOptions<RisksDbContext>>();
            services.RemoveAll<RisksDbContext>();
            services.AddDbContext<RisksDbContext>(o => o.UseInMemoryDatabase(_dbName + "-risks"));

            services.RemoveAll<DbContextOptions<TraceabilityDbContext>>();
            services.RemoveAll<TraceabilityDbContext>();
            services.AddDbContext<TraceabilityDbContext>(o => o.UseInMemoryDatabase(_dbName + "-traceability"));

            services.RemoveAll<DbContextOptions<DependenciesDbContext>>();
            services.RemoveAll<DependenciesDbContext>();
            services.AddDbContext<DependenciesDbContext>(o => o.UseInMemoryDatabase(_dbName + "-dependencies"));

            services.RemoveAll<DbContextOptions<GovernanceDbContext>>();
            services.RemoveAll<GovernanceDbContext>();
            services.AddDbContext<GovernanceDbContext>(o => o.UseInMemoryDatabase(_dbName + "-governance"));

            services.RemoveAll<DbContextOptions<ResearchDbContext>>();
            services.RemoveAll<ResearchDbContext>();
            services.AddDbContext<ResearchDbContext>(o => o.UseInMemoryDatabase(_dbName + "-research"));

            services.RemoveAll<DbContextOptions<NotificationsDbContext>>();
            services.RemoveAll<NotificationsDbContext>();
            services.AddDbContext<NotificationsDbContext>(o => o.UseInMemoryDatabase(_dbName + "-notifications"));

            services.RemoveAll<DbContextOptions<AuditDbContext>>();
            services.RemoveAll<AuditDbContext>();
            services.AddDbContext<AuditDbContext>(o => o.UseInMemoryDatabase(_dbName + "-audit"));

            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }

    public async Task SeedMembersAsync(params (string Sub, string Name, CommitteeRole Role)[] members)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MembershipDbContext>();
        foreach (var (sub, name, role) in members)
            db.Members.Add(CommitteeMember.Provision(sub, name, $"{sub}@acmp.gov", role, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
    }

    // Seeds one lean v1 row (a system/authZ event — enriched columns null) and one enriched v2 row (a
    // governed state change), chained correctly off Genesis, so the /api/audit read tests exercise BOTH row
    // shapes deterministically (post-PR2 the API only ever produces v2 rows). Returns their hashes for chain
    // assertions.
    public async Task<(string V1Hash, string V2Hash)> SeedAuditAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
        var t = DateTimeOffset.UtcNow;

        var v1 = AuditEvent.CreateNext(AuditEvent.Genesis, t, "Authentication.NoRoleClaim", "kc-legacy", null);
        db.AuditEvents.Add(v1);
        await db.SaveChangesAsync();

        var v2 = AuditEvent.CreateEnriched(v1.Hash, t.AddSeconds(1), "Vote.Closed", "Vote", "VOTE-2026-001",
            "kc-chair", "Chairman", AuditOutcome.Success, null, "{\"status\":\"Closed\"}", "trace-abc");
        db.AuditEvents.Add(v2);
        await db.SaveChangesAsync();

        return (v1.Hash, v2.Hash);
    }
}
