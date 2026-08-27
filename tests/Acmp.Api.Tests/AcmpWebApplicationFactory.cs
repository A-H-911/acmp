using Acmp.Modules.Actions.Infrastructure.Persistence;
using Acmp.Modules.Decisions.Infrastructure.Persistence;
using Acmp.Modules.Dependencies.Infrastructure.Persistence;
using Acmp.Modules.Governance.Infrastructure.Persistence;
using Acmp.Modules.Knowledge.Infrastructure.Persistence;
using Acmp.Modules.Meetings.Infrastructure.Persistence;
using Acmp.Modules.Membership.Application.Abstractions;
using Acmp.Modules.Membership.Domain;
using Acmp.Modules.Membership.Domain.Enums;
using Acmp.Modules.Membership.Infrastructure.Directory;
using Acmp.Modules.Membership.Infrastructure.Persistence;
using Acmp.Modules.Notifications.Infrastructure.Persistence;
using Acmp.Modules.Research.Infrastructure.Persistence;
using Acmp.Modules.Risks.Infrastructure.Persistence;
using Acmp.Modules.Topics.Domain;
using Acmp.Modules.Topics.Domain.Enums;
using Acmp.Modules.Topics.Infrastructure.Persistence;
using Acmp.Modules.Traceability.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Contracts.Membership;
using Acmp.Shared.Domain.ValueObjects;
using Acmp.Shared.Infrastructure.Audit;
using Acmp.Shared.Infrastructure.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using MembershipStream = Acmp.Modules.Membership.Domain.Stream;

namespace Acmp.Api.Tests;

// Boots the real API host with two test swaps: the Membership DbContext points at a private
// in-memory store, and authentication uses the header-driven TestAuthHandler instead of Keycloak.
// Everything else (the MediatR pipeline, policy authorization, Problem Details) runs unchanged.
public sealed class AcmpWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = "acmp-it-" + Guid.NewGuid();
    // ⚠ MUST STAY PARAMETERLESS-CONSTRUCTIBLE. Several suites (the Webex ones) take this as an xUnit
    // IClassFixture, and xUnit activates a fixture through its parameterless constructor — an
    // optional parameter is not parameterless to that activator, and adding one failed 22 tests at
    // construction time. Hence an init-only property set by the named factory below.
    private bool UseIdentityProvider { get; init; }

    /// <summary>
    /// ADR-0040 / SC-005 — a factory whose host has a fake Keycloak, so the invite HAPPY PATH is
    /// reachable from this harness at all. Deliberately not the default: an existing test asserts
    /// that an invite FAILS at composition when no identity provider is configured, and that
    /// assertion protects a real property (a member row with no account can never be deleted,
    /// DEF-029).
    /// </summary>
    public static AcmpWebApplicationFactory WithIdentityProvider() => new() { UseIdentityProvider = true };

    /// <summary>The fake, once the host is built — so a test can assert what Keycloak was asked to do.</summary>
    public FakeIdentityProvider Identity => Services.GetRequiredService<FakeIdentityProvider>();

    // ADR-0042 step 1's taxonomy. ⚠ SEEDED HERE BECAUSE THE HARNESS USES THE INMEMORY PROVIDER, WHICH
    // NEVER RUNS MIGRATIONS — and the taxonomy is seeded BY a migration, so it exists in every real
    // environment and in none of these tests. Without it the submit/update validators would refuse
    // every topic, which is a property of the FIXTURE rather than of the code under test.
    //
    // ⚠ The WILDCARD is deliberately absent: StreamCatalog excludes it from the assignable set, so
    // seeding it here could only mask a bug in that filter. Nothing in this harness needs it.
    private static readonly (string Code, string En, string Ar)[] SeededStreams =
    {
        ("core", "Core", "الأساسي"),
        ("communications", "Communications", "الاتصالات"),
        ("smart-cities", "Smart Cities", "المدن الذكية"),
        ("government", "Government", "الحكومي"),
        ("shared-services", "Shared Services", "الخدمات المشتركة"),
    };

    // Seeded once per host, before any test runs: the taxonomy is reference data that is simply
    // always there, not something an individual test opts into and can forget.
    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MembershipDbContext>();
        foreach (var (code, en, ar) in SeededStreams)
            db.Streams.Add(MembershipStream.Create(code, LocalizedString.Create(en, ar)));
        db.SaveChanges();

        return host;
    }

    /*
     * Swap one module's DbContext onto the InMemory provider (DW-080 phase A).
     *
     * ⚠⚠ THE `IDbContextOptionsConfiguration<TContext>` LINE IS THE WHOLE POINT, AND ITS ABSENCE IS WHAT
     * THE .NET 10 MIGRATION EXPOSED. EF Core 9+ registers that descriptor alongside DbContextOptions<T>,
     * and IT is what carries the `UseSqlServer` call. Removing only the options descriptor left the SQL
     * Server configuration applied, so both providers ended up in one service provider — which EF 8
     * TOLERATED and EF 10 refuses outright: "Services for database providers 'SqlServer', 'InMemory' have
     * been registered ... Only a single database provider can be registered."
     *
     * ⭐ THE SHAPE WORTH KEEPING: it compiled, `dotnet format` passed, and the solution built clean in
     * Release — then 355 of 392 API tests failed at RUNTIME. DW-080's row predicted exactly this class
     * ("the failure mode compiles and unit-tests perfectly"); it was the suite, not the compiler, that
     * caught it. A migration's real verdict comes from executing, never from building.
     *
     * Extracted from fourteen copy-pasted triples rather than fixing fourteen of them, so the next
     * context added here cannot silently omit the line that matters (WBS-24.5's three-places lesson).
     */
    internal static void UseInMemory<TContext>(IServiceCollection services, string dbName)
        where TContext : DbContext
    {
        services.RemoveAll<DbContextOptions<TContext>>();
        services.RemoveAll<IDbContextOptionsConfiguration<TContext>>();
        services.RemoveAll<TContext>();
        services.AddDbContext<TContext>(o => o.UseInMemoryDatabase(dbName));
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            UseInMemory<MembershipDbContext>(services, _dbName);

            UseInMemory<TopicsDbContext>(services, _dbName + "-topics");

            UseInMemory<MeetingsDbContext>(services, _dbName + "-meetings");

            UseInMemory<DecisionsDbContext>(services, _dbName + "-decisions");

            UseInMemory<ActionsDbContext>(services, _dbName + "-actions");

            UseInMemory<RisksDbContext>(services, _dbName + "-risks");

            UseInMemory<TraceabilityDbContext>(services, _dbName + "-traceability");

            UseInMemory<DependenciesDbContext>(services, _dbName + "-dependencies");

            UseInMemory<GovernanceDbContext>(services, _dbName + "-governance");

            UseInMemory<ResearchDbContext>(services, _dbName + "-research");

            UseInMemory<KnowledgeDbContext>(services, _dbName + "-knowledge");

            UseInMemory<NotificationsDbContext>(services, _dbName + "-notifications");

            UseInMemory<AuditDbContext>(services, _dbName + "-audit");

            // WBS-24.5: the externalized configuration store. A DbContext has to be substituted in
            // THREE places, not two — DI, MigrationRunner and here — and omitting this one fails
            // by trying to reach a real SQL Server, which reads like an environment problem
            // rather than a missing registration.
            UseInMemory<ConfigurationDbContext>(services, _dbName + "-config");

            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

            if (!UseIdentityProvider) return;

            // Singleton so the recorded calls survive the request scope the test wants to inspect
            // afterwards. GuestProvisioner is registered here too because the host registers it only
            // alongside the real Keycloak client (ADR-0040) — a fake identity without the port would
            // leave the guest-invite path uncomposable and prove nothing.
            services.AddSingleton<FakeIdentityProvider>();
            services.AddScoped<IIdentityProvider>(sp => sp.GetRequiredService<FakeIdentityProvider>());
            services.AddScoped<IGuestProvisioner, GuestProvisioner>();
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

    /// <summary>
    /// Seed a topic already walked to <c>Decided</c>, and return its public id (FR-160 / AC-109).
    /// </summary>
    /// <remarks>
    /// ⚠ THIS EXISTS BECAUSE <c>Decided</c> IS NOT REACHABLE OVER HTTP IN A TEST. A decision only
    /// advances a topic through <c>TopicDecisionRecorder.MarkDecidedAsync</c>, which SILENTLY RETURNS
    /// unless the topic is already <c>InCommittee</c> — and reaching that needs the whole meetings
    /// flow (schedule → publish agenda → conduct). Building that fixture to exercise one endpoint
    /// would be a large, fragile setup whose failures would look like close bugs.
    ///
    /// The walk uses the REAL aggregate transitions in their real order rather than writing a status
    /// column, so a topic seeded here is one the domain agrees is Decided — if any guard in that chain
    /// changes, this throws instead of quietly producing an impossible row.
    /// </remarks>
    public async Task<Guid> SeedDecidedTopicAsync(string ownerSub = "kc-omar", string ownerName = "Omar H.")
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TopicsDbContext>();
        var now = DateTimeOffset.UtcNow;

        var topic = Topic.Draft(
            $"TOP-2026-{Random.Shared.Next(100, 999)}", "Adopt Keycloak", "Consolidate IAM.",
            "Fragmented auth is risky.", TopicType.ArchitectureDecision, TopicUrgency.Normal,
            TopicSource.CommitteeMember, ownerSub, ownerName,
            new[] { "core" }, Array.Empty<string>(), Array.Empty<string>());

        topic.Submit(now);
        topic.BeginTriage(ownerSub, ownerName, now);
        topic.Accept(Guid.NewGuid(), ownerName, ownerSub, ownerName, now);
        topic.MarkPrepared(ownerSub, ownerName, now);
        topic.Schedule(Guid.NewGuid(), ownerSub, ownerName, now);
        topic.EnterCommittee(ownerSub, ownerName, now);
        topic.Decide(ownerSub, ownerName, now);

        db.Topics.Add(topic);
        await db.SaveChangesAsync();
        return topic.PublicId;
    }

    /// <summary>
    /// Assign a seeded member to streams by CODE (ADR-0043 step 7). Codes rather than ids because the
    /// authorization control keys on Stream.Code, so a test that passed ids could drift from the value
    /// the handler actually intersects.
    /// </summary>
    public async Task AssignStreamsAsync(string sub, params string[] codes)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MembershipDbContext>();
        var member = await db.Members.FirstAsync(m => m.KeycloakUserId == sub);
        var ids = await db.Streams.Where(s => codes.Contains(s.Code)).Select(s => s.Id).ToListAsync();
        if (ids.Count != codes.Length)
            throw new InvalidOperationException($"[test] unknown stream code in [{string.Join(", ", codes)}]");
        member.AssignStreams(ids);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// ADR-0039: mutate a seeded member's revalidation state directly, so a test can put the member
    /// into the condition it wants to FORCE a refusal from — a role change at a known instant, or a
    /// closed access window — without driving the whole assignment flow to get there.
    /// </summary>
    public async Task SetRevalidationStateAsync(
        string sub, DateTimeOffset? rolesChangedAt = null, DateTimeOffset? accessExpiresAt = null, bool disable = false)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MembershipDbContext>();
        var member = await db.Members.FirstAsync(m => m.KeycloakUserId == sub);
        if (rolesChangedAt is { } changed) member.ApplyAssignedRole(member.Role, changed);
        if (accessExpiresAt is not null) member.SetAccessWindow(accessExpiresAt);
        if (disable) member.Deactivate();
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
