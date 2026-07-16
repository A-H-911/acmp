using Acmp.Modules.Actions.Infrastructure.Persistence;
using Acmp.Modules.Decisions.Infrastructure.Persistence;
using Acmp.Modules.Meetings.Infrastructure.Persistence;
using Acmp.Modules.Membership.Infrastructure.Persistence;
using Acmp.Modules.Notifications.Infrastructure.Persistence;
using Acmp.Modules.Risks.Infrastructure.Persistence;
using Acmp.Modules.Topics.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace Acmp.Integration.Tests;

// S5 (ADR-0016 §3). Boots ONE real SQL Server container for the whole assembly and applies every
// module's migrations into its own schema. The point of this suite is to prove the DATABASE-enforced
// backstops (unique indexes, FK behaviour, migrations) that the EF Core InMemory provider silently
// ignores — so each test contrasts a write that SQL Server rejects against the same write that
// InMemory happily accepts. Starting the container is expensive, so it is shared via a collection.
public sealed class SqlBackstopFixture : IAsyncLifetime
{
    // Image passed explicitly (this is Testcontainers' own default, pinned): the parameterless ctor is
    // obsolete, and an explicit tag means a Testcontainers upgrade can't silently move which SQL Server
    // these tests run against.
    private readonly MsSqlContainer _container =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04").Build();

    public string ConnectionString { get; private set; } = string.Empty;

    // Fixed clock + a stand-in actor: ModuleDbContext.SaveChangesAsync stamps audit fields from these.
    public IClock Clock { get; } = new TestClock();
    public ICurrentUser CurrentUser { get; } = new TestCurrentUser();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();

        // Applying each context's migrations on real SQL Server IS the "migrations apply cleanly" proof
        // (ADR-0016 §3 / validation). A broken migration throws here and fails the whole suite.
        await using (var db = NewMembershipSql()) await db.Database.MigrateAsync();
        await using (var db = NewTopicsSql()) await db.Database.MigrateAsync();
        await using (var db = NewMeetingsSql()) await db.Database.MigrateAsync();
        await using (var db = NewDecisionsSql()) await db.Database.MigrateAsync();
        await using (var db = NewActionsSql()) await db.Database.MigrateAsync();
        await using (var db = NewRisksSql()) await db.Database.MigrateAsync();
        await using (var db = NewNotificationsSql()) await db.Database.MigrateAsync();
        await using (var db = NewAuditSql()) await db.Database.MigrateAsync(); // audit schema + Audit_DenyMutation (D-16)
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    // ---- SQL Server contexts (mirror the production wiring: per-module migrations-history schema) ----

    public MembershipDbContext NewMembershipSql() => new(
        SqlOptions<MembershipDbContext>(MembershipDbContext.Schema), Clock, CurrentUser);

    public TopicsDbContext NewTopicsSql() => new(
        SqlOptions<TopicsDbContext>(TopicsDbContext.Schema), Clock, CurrentUser);

    public MeetingsDbContext NewMeetingsSql() => new(
        SqlOptions<MeetingsDbContext>(MeetingsDbContext.Schema), Clock, CurrentUser);

    public DecisionsDbContext NewDecisionsSql() => new(
        SqlOptions<DecisionsDbContext>(DecisionsDbContext.Schema), Clock, CurrentUser);

    public ActionsDbContext NewActionsSql() => new(
        SqlOptions<ActionsDbContext>(ActionsDbContext.Schema), Clock, CurrentUser);

    public RisksDbContext NewRisksSql() => new(
        SqlOptions<RisksDbContext>(RisksDbContext.Schema), Clock, CurrentUser);

    public NotificationsDbContext NewNotificationsSql() => new(
        SqlOptions<NotificationsDbContext>(NotificationsDbContext.Schema), Clock, CurrentUser);

    // AuditDbContext is not a ModuleDbContext (no clock/user) — its own schema "audit" (BL-066).
    public AuditDbContext NewAuditSql() => new(SqlOptions<AuditDbContext>(AuditDbContext.Schema));

    // ---- InMemory twins (the "accepts what SQL rejects" side of each contrast) ----

    public MembershipDbContext NewMembershipInMemory(string name) => new(
        InMemoryOptions<MembershipDbContext>(name), Clock, CurrentUser);

    public MeetingsDbContext NewMeetingsInMemory(string name) => new(
        InMemoryOptions<MeetingsDbContext>(name), Clock, CurrentUser);

    private DbContextOptions<T> SqlOptions<T>(string schema) where T : DbContext =>
        new DbContextOptionsBuilder<T>()
            .UseSqlServer(ConnectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", schema))
            .Options;

    private static DbContextOptions<T> InMemoryOptions<T>(string name) where T : DbContext =>
        new DbContextOptionsBuilder<T>().UseInMemoryDatabase(name).Options;
}

[CollectionDefinition(Name)]
public sealed class SqlBackstopCollection : ICollectionFixture<SqlBackstopFixture>
{
    public const string Name = "sql-backstop";
}

internal sealed class TestClock : IClock
{
    public DateTimeOffset UtcNow => new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
}

// Minimal authenticated actor — only UserId is read (by ModuleDbContext audit stamping).
internal sealed class TestCurrentUser : ICurrentUser
{
    public bool IsAuthenticated => true;
    public string? UserId => "it-actor";
    public string? UserName => "it-actor";
    public string? Email => "it-actor@acmp.gov";
    public string? DisplayName => "Integration Test Actor";
    public IReadOnlyCollection<string> Roles => Array.Empty<string>();
    public bool IsInRole(string role) => false;
}
