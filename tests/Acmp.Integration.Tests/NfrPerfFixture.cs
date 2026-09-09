using Acmp.Modules.Actions.Infrastructure.Persistence;
using Acmp.Modules.Decisions.Infrastructure.Persistence;
using Acmp.Modules.Dependencies.Infrastructure.Persistence;
using Acmp.Modules.Governance.Infrastructure.Persistence;
using Acmp.Modules.Knowledge.Infrastructure.Persistence;
using Acmp.Modules.Meetings.Infrastructure.Persistence;
using Acmp.Modules.Membership.Infrastructure.Persistence;
using Acmp.Modules.Notifications.Infrastructure.Persistence;
using Acmp.Modules.Research.Infrastructure.Persistence;
using Acmp.Modules.Risks.Infrastructure.Persistence;
using Acmp.Modules.Topics.Infrastructure.Persistence;
using Acmp.Modules.Traceability.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Infrastructure.Audit;
using Acmp.Shared.Infrastructure.Configuration;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Images;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace Acmp.Integration.Tests;

/*
 * DEC-160 g1. The 10 000-row stack every NFR performance clause asks for, in one fixture.
 *
 * ⚠⚠ WHY THIS EXISTS, AND WHY IT IS NOT THE ROUTE FIRST AUTHORISED. DEC-159 f2 ruled that a
 * requirement's Verification clause is PART OF THE REQUIREMENT, not advice about how to check it.
 * Read that way, NFR-002/003/004/006 all name an INTEGRATION TEST — not a browser, not curl against
 * a deployed box — and three of them name a 10 000-record dataset. DW-103's existing numbers were
 * taken at the DATABASE layer, and its own title says that is insufficient BECAUSE THE REQUIREMENTS
 * NAME ENDPOINTS. So this measures through a real HTTP host, over real SQL Server, at that scale.
 *
 * ⭐ THE SEED IS COMMITTED THIS TIME, AND THAT IS AS MUCH THE POINT AS THE MEASUREMENT. The
 * 10 216-topic dataset DW-103 measured against was produced by a script that lived only in a
 * throwaway .scratch/ folder, so the number was never reproducible by anyone — and the dataset is
 * now gone (uat holds 216 topics; PE-1045). Trap 27 says nothing a later session must read may live
 * in .scratch, and an unreproducible measurement is exactly the cost of breaking that rule.
 *
 * ⚠ THE FTS IMAGE IS REQUIRED, NOT PREFERRED. NFR-004 measures full-text search and the stock mssql
 * image ships WITHOUT it, so this boots deploy/Dockerfile.sqlserver — the image SearchProvidersFtsTests
 * builds. It is deliberately a SEPARATE fixture from that suite: DEC-077 d3 puts a standing
 * STOP-on-red rule on SearchProvidersFtsTests, and folding a perf measurement into a suite nobody may
 * re-run would muddy both verdicts.
 */
public sealed class NfrPerfFixture : IAsyncLifetime
{
    /// <summary>Rows seeded. NFR-002 and NFR-004 both name 10 000 records in their own statements.</summary>
    public const int SeededTopics = 10_000;

    /// <summary>The key prefix every seeded row carries, so a test can prove it measured THESE rows.</summary>
    public const string SeedKeyPrefix = "TOP-PERF-";

    private readonly IFutureDockerImage _image = new ImageFromDockerfileBuilder()
        .WithDockerfileDirectory(CommonDirectoryPath.GetSolutionDirectory(), "deploy")
        .WithDockerfile("Dockerfile.sqlserver")
        .WithName("acmp/sqlserver-fts:test")   // same tag as SearchProvidersFtsTests, so the ~160s build is shared
        .WithCleanUp(false)                    // DEF-140: keep it cached, and keep the build output visible
        .WithLogger(DockerBuildLog.Instance)
        .Build();

    private MsSqlContainer _container = null!;

    private readonly IClock _clock = new TestClock();
    private readonly ICurrentUser _user = new TestCurrentUser();

    /// <summary>Connection string for the migrated, seeded application database.</summary>
    public string ConnectionString { get; private set; } = string.Empty;

    /// <summary>Wall-clock seconds the seed took — reported alongside every measurement.</summary>
    public double SeedSeconds { get; private set; }

    public async Task InitializeAsync()
    {
        await ContainerStartup.BuildOrFailFastAsync(_image, "SQL Server FTS (deploy/Dockerfile.sqlserver)");
        _container = new MsSqlBuilder(_image).Build();
        await ContainerStartup.StartOrFailFastAsync(_container, "SQL Server (FTS, NFR perf)");

        // A full-text catalog cannot live in master/tempdb/model, and MsSqlBuilder connects to master.
        await CreateApplicationDatabaseAsync();
        await MigrateEveryModuleAsync();

        var started = System.Diagnostics.Stopwatch.StartNew();
        await SeedTopicsAsync();
        SeedSeconds = started.Elapsed.TotalSeconds;

        await WaitForFullTextPopulationAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    private async Task CreateApplicationDatabaseAsync()
    {
        await using var conn = new SqlConnection(_container.GetConnectionString());
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "IF DB_ID('Acmp') IS NULL CREATE DATABASE Acmp;";
        await cmd.ExecuteNonQueryAsync();

        ConnectionString = new SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = "Acmp",
        }.ConnectionString;
    }

    /*
     * ⚠ A HAND-MAINTAINED CONTEXT LIST IS A PLACE THAT CAN DRIFT, and WBS-24.5's lesson is that a
     * context has to be substituted in three places and omitting one fails confusingly. It is written
     * out anyway, exactly as SqlBackstopFixture does, because the alternative — reflecting over the
     * host's registrations — would migrate whatever the host happens to register and quietly stop
     * being a statement about which schemas this measurement needs. A missing context fails LOUDLY on
     * the first request that touches it, not silently.
     */
    private async Task MigrateEveryModuleAsync()
    {
        await using (var db = new MembershipDbContext(Options<MembershipDbContext>(MembershipDbContext.Schema), _clock, _user))
            await db.Database.MigrateAsync();
        await using (var db = new TopicsDbContext(Options<TopicsDbContext>(TopicsDbContext.Schema), _clock, _user))
            await db.Database.MigrateAsync();
        await using (var db = new MeetingsDbContext(Options<MeetingsDbContext>(MeetingsDbContext.Schema), _clock, _user))
            await db.Database.MigrateAsync();
        await using (var db = new DecisionsDbContext(Options<DecisionsDbContext>(DecisionsDbContext.Schema), _clock, _user))
            await db.Database.MigrateAsync();
        await using (var db = new ActionsDbContext(Options<ActionsDbContext>(ActionsDbContext.Schema), _clock, _user))
            await db.Database.MigrateAsync();
        await using (var db = new RisksDbContext(Options<RisksDbContext>(RisksDbContext.Schema), _clock, _user))
            await db.Database.MigrateAsync();
        await using (var db = new NotificationsDbContext(Options<NotificationsDbContext>(NotificationsDbContext.Schema), _clock, _user))
            await db.Database.MigrateAsync();
        await using (var db = new DependenciesDbContext(Options<DependenciesDbContext>(DependenciesDbContext.Schema), _clock, _user))
            await db.Database.MigrateAsync();
        await using (var db = new TraceabilityDbContext(Options<TraceabilityDbContext>(TraceabilityDbContext.Schema), _clock, _user))
            await db.Database.MigrateAsync();
        await using (var db = new GovernanceDbContext(Options<GovernanceDbContext>(GovernanceDbContext.Schema), _clock, _user))
            await db.Database.MigrateAsync();
        await using (var db = new KnowledgeDbContext(Options<KnowledgeDbContext>(KnowledgeDbContext.Schema), _clock, _user))
            await db.Database.MigrateAsync();
        await using (var db = new ResearchDbContext(Options<ResearchDbContext>(ResearchDbContext.Schema), _clock, _user))
            await db.Database.MigrateAsync();
        await using (var db = new AuditDbContext(Options<AuditDbContext>(AuditDbContext.Schema)))
            await db.Database.MigrateAsync();
        await using (var db = new ConfigurationDbContext(Options<ConfigurationDbContext>(ConfigurationDbContext.Schema)))
            await db.Database.MigrateAsync();
    }

    /*
     * ⛔⛔ SEED THROUGH THIS, NEVER THROUGH THE HOST'S SERVICE SCOPE — AND THE REASON IS INVISIBLE ON
     * THE InMemory PROVIDER EVERY OTHER API TEST USES. The host wires every module DbContext onto ONE
     * shared connection with an AmbientTransaction (ADR-0026 / NFR-042) that opens LAZILY on the first
     * module write and is committed by TransactionBehavior — a MediatR pipeline behaviour. A raw
     * SaveChangesAsync outside a MediatR command therefore opens that transaction and nothing ever
     * commits it: the scope disposes, it rolls back, and SaveChangesAsync REPORTS SUCCESS THROUGHOUT.
     *
     * That is the design working (state change and audit append commit together), not a defect. But it
     * means the seeding idiom used all over Acmp.Api.Tests — CreateScope, add, SaveChanges — silently
     * writes nothing here, because InMemory has no transaction to leave uncommitted. Measured: the
     * first NFR-006 run 404'd, and a read-back through a fresh scope proved the row was never there.
     *
     * A context built here has its OWN connection and no ambient transaction, so its writes autocommit.
     */
    public MeetingsDbContext NewMeetingsContext() =>
        new(Options<MeetingsDbContext>(MeetingsDbContext.Schema), _clock, _user);

    private DbContextOptions<T> Options<T>(string schema) where T : DbContext =>
        new DbContextOptionsBuilder<T>()
            .UseSqlServer(ConnectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", schema))
            .Options;

    /*
     * Set-based raw INSERT, not EF entities. 10 000 entities would fire audit stamping and change
     * tracking per row and take minutes; this is one statement. The column list doubles as a schema
     * assertion — a renamed or newly NOT-NULL column fails here, loudly, at seed time.
     *
     * Ported from the generator that produced DW-103's original dataset, which survived only by
     * accident in a previous session's .scratch/ folder. Committing it is the repair.
     */
    private async Task SeedTopicsAsync()
    {
        const string sql =
            "SET NOCOUNT ON; " +
            ";WITH n AS (SELECT TOP (@count) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS i " +
            "            FROM sys.all_objects a CROSS JOIN sys.all_objects b) " +
            "INSERT INTO topics.topics ( " +
            "  [Key],Title,Description,Justification,Type,Urgency,Scope,Source,Status,Priority, " +
            "  SubmittedBySub,SubmittedByName,streams,systems,tags,PublicId,CreatedAt,CreatedBy,TimesDeferred,IsRestricted) " +
            "SELECT " +
            "  'TOP-PERF-' + RIGHT('0000000' + CAST(i AS varchar(8)), 7), " +
            "  'Perf seed ' + CAST(i AS varchar(8)) + ' ' " +
            "    + CHOOSE(1+(i%8),'migration','resilience','observability','governance','integration','identity','throughput','deprecation') " +
            "    + ' review for the ' " +
            "    + CHOOSE(1+(i%5),'payments','registry','portal','ledger','gateway') + ' platform', " +
            "  'Synthetic performance-seed description ' + CAST(i AS varchar(8)) + '. ' " +
            "    + CHOOSE(1+(i%6), " +
            "        'The committee must assess latency budgets across the estate.', " +
            "        'A dependency upgrade changes the supported runtime baseline.', " +
            "        'Capacity headroom is insufficient for the projected load.', " +
            "        'Audit retention conflicts with the storage tier policy.', " +
            "        'Cross-stream coupling makes rollback expensive.', " +
            "        'The vendor contract expires within the planning horizon.'), " +
            "  'Synthetic justification ' + CAST(i AS varchar(8)), " +
            "  1+(i%4), 1+(i%3), 1+(i%3), 1+(i%9), 1+(i%6), i%4, " +
            "  'perf-seed-sub','Perf Seed','[\"Platform\"]','[]','[]', " +
            "  NEWID(), " +
            "  DATEADD(hour, -(i%43800), SYSDATETIMEOFFSET()), " +
            "  'perf-seed', 0, 0 " +
            "FROM n;";

        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.CommandTimeout = 300;
        cmd.Parameters.AddWithValue("@count", SeededTopics);
        await cmd.ExecuteNonQueryAsync();
    }

    /*
     * ⛔ WITHOUT THIS WAIT, NFR-004's NUMBER IS MEANINGLESS AND FAST. Full-text population is
     * ASYNCHRONOUS: the INSERT returns long before the index covers the rows, and a FREETEXT query
     * against an unpopulated index returns nothing, very quickly. That is the empty-subject failure
     * LL-074 names — a clean, confident measurement over no data.
     */
    private async Task WaitForFullTextPopulationAsync()
    {
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();

        for (var attempt = 0; attempt < 180; attempt++)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT ISNULL(MAX(CAST(FULLTEXTCATALOGPROPERTY(name,'PopulateStatus') AS int)), 0) FROM sys.fulltext_catalogs;";
            var status = (int)(await cmd.ExecuteScalarAsync() ?? 0);
            if (status == 0)
                return;
            await Task.Delay(1000);
        }

        throw new InvalidOperationException(
            "Full-text population did not settle within 180s; NFR-004 cannot be measured against an unpopulated index.");
    }
}

[CollectionDefinition(Name)]
public sealed class NfrPerfCollection : ICollectionFixture<NfrPerfFixture>
{
    public const string Name = "nfr-perf";
}
