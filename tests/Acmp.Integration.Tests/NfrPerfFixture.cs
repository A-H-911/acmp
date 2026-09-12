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
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace Acmp.Integration.Tests;

/*
 * DEC-160 g1 and DEC-162 j1. TWO seeded databases in ONE container — the scales the two governing
 * requirements name.
 *
 * ⚠⚠ WHY THIS EXISTS, AND WHY IT IS NOT THE ROUTE FIRST AUTHORISED. DEC-159 f2 ruled that a
 * requirement's Verification clause is PART OF THE REQUIREMENT, not advice about how to check it.
 * Read that way, NFR-002/003/004/006 all name an INTEGRATION TEST — not a browser, not curl against
 * a deployed box — and three of them name a 10 000-record dataset. DW-103's numbers were taken at the
 * DATABASE layer, and its own title says that is insufficient BECAUSE THE REQUIREMENTS NAME ENDPOINTS.
 * So this measures through a real HTTP host, over real SQL Server, at the scales they name.
 *
 * ⭐⭐ TWO SCALES, BECAUSE TWO APPROVED `Must` REQUIREMENTS NAME DIFFERENT ONES FOR THE SAME QUERY.
 * NFR-002 says "up to 10 000 topic records". NFR-009 says 2 500 total over a five-year operational
 * life and cross-references NFR-002 for the bound at THAT scale, with its own Verification clause
 * reading "Load test with 2 500 seeded topic records; re-run NFR-002 target assertion". Both are
 * measured here; which one governs a disposition is the operator's (DEC-162 j1).
 *
 * ⛔⛔ BOTH DATABASES LIVE IN ONE CONTAINER AND BOTH TEST CLASSES SHARE ONE COLLECTION, AND THAT IS A
 * MEASUREMENT REQUIREMENT RATHER THAN THRIFT. xUnit runs separate COLLECTIONS in parallel by default,
 * and this assembly sets no CollectionBehavior — so a second collection fixture would boot a second
 * SQL Server container CONCURRENTLY with the first. Two containers competing for CPU would corrupt
 * both sets of latency numbers, which is the one thing this fixture exists to produce.
 *
 * ⭐ THE SEED IS COMMITTED, AND THAT IS AS MUCH THE POINT AS THE MEASUREMENT. The 10 216-topic dataset
 * DW-103 measured against was produced by a script that lived only in a throwaway .scratch/ folder, so
 * the number was never reproducible by anyone — and the dataset is now gone (uat holds 216 topics;
 * PE-1045). Trap 27 exists to prevent exactly this.
 *
 * ⚠ THE FTS IMAGE IS REQUIRED, NOT PREFERRED. NFR-004 measures full-text search and the stock mssql
 * image ships WITHOUT it, so this boots deploy/Dockerfile.sqlserver — the image FtsImage builds once per
 * run for this fixture and SearchProvidersFtsTests alike (DEF-161). It is deliberately a SEPARATE fixture
 * from that suite: DEC-077 d3 puts a standing
 * STOP-on-red rule on SearchProvidersFtsTests, and folding a perf measurement into a suite nobody may
 * re-run would muddy both verdicts.
 *
 * ⚠ A LIMITATION OF THE SEED, STATED SO IT IS NOT REDISCOVERED AS A SURPRISE: it writes only
 * topics.topics and no topic_status_events, so `Include(t => t.History)` joins nothing here. uat
 * carries roughly 2.3 events per topic, so these figures probably UNDERSTATE the real cost.
 */
public sealed class NfrPerfFixture : IAsyncLifetime
{
    /// <summary>NFR-002's stated scale: "up to 10 000 topic records".</summary>
    public const int SeededTopics = 10_000;

    /// <summary>NFR-009's stated scale: 2 500 total over a five-year operational life.</summary>
    public const int SeededTopicsAtOperationalCeiling = 2_500;

    /// <summary>The key prefix every seeded row carries, so a test can prove it measured THESE rows.</summary>
    public const string SeedKeyPrefix = "TOP-PERF-";

    private MsSqlContainer _container = null!;

    private readonly IClock _clock = new TestClock();
    private readonly ICurrentUser _user = new TestCurrentUser();

    /// <summary>Connection string for the 10 000-topic database (NFR-002's scale).</summary>
    public string ConnectionString { get; private set; } = string.Empty;

    /// <summary>Connection string for the 2 500-topic database (NFR-009's scale).</summary>
    public string ConnectionStringAtOperationalCeiling { get; private set; } = string.Empty;

    /// <summary>Wall-clock seconds the 10 000-row seed took — reported alongside every measurement.</summary>
    public double SeedSeconds { get; private set; }

    public async Task InitializeAsync()
    {
        // DEF-161: the image is built once per test run and shared with SearchProvidersFtsTests - the BUILD only.
        // The container, the databases and the verdicts stay separate, as the header above requires; what is
        // now shared is that one failed build reds both, which both DEF-158 occurrences already did.
        _container = new MsSqlBuilder(await FtsImage.BuildOnceAsync()).Build();
        await ContainerStartup.StartOrFailFastAsync(_container, "SQL Server (FTS, NFR perf)");

        var started = System.Diagnostics.Stopwatch.StartNew();
        ConnectionString = await BuildSeededDatabaseAsync("Acmp", SeededTopics);
        SeedSeconds = started.Elapsed.TotalSeconds;

        ConnectionStringAtOperationalCeiling =
            await BuildSeededDatabaseAsync("AcmpCeiling", SeededTopicsAtOperationalCeiling);
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    /// <summary>Create one catalog, migrate every module into it, seed it, and wait out full-text population.</summary>
    private async Task<string> BuildSeededDatabaseAsync(string catalog, int topicCount)
    {
        /*
         * A full-text catalog cannot live in master/tempdb/model, and MsSqlBuilder connects to master.
         *
         * ⚠ THIS WAS `$"IF DB_ID('{catalog}') IS NULL CREATE DATABASE [{catalog}];"` AND SEMGREP'S
         * `csharp.lang.security.sqli` BLOCKED THE BUILD ON IT. Both call sites pass a compile-time
         * constant, so it was not exploitable — but the SCANNER IS RIGHT ABOUT THE SHAPE, and the right
         * response to a true finding about unreachable code is to fix the shape rather than suppress the
         * rule: the parameter is what keeps this safe when someone later feeds `catalog` from a config
         * value or a test argument. An identifier cannot be a bind parameter, which is what QUOTENAME is
         * for — it escapes the name for the dynamic statement while the VALUE still travels as a
         * parameter rather than as concatenated C#.
         */
        await using (var conn = new SqlConnection(_container.GetConnectionString()))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            // ⚠ QUOTENAME GOES IN A DECLARE, NOT INSIDE `EXEC(...)`. T-SQL's EXEC(string) accepts only
            // variables and string literals concatenated — a FUNCTION CALL in that position is a syntax
            // error ("Incorrect syntax near 'QUOTENAME'"), which the first attempt here earned.
            cmd.CommandText =
                "DECLARE @sql nvarchar(max) = N'CREATE DATABASE ' + QUOTENAME(@catalog); " +
                "IF DB_ID(@catalog) IS NULL EXEC sp_executesql @sql;";
            cmd.Parameters.AddWithValue("@catalog", catalog);
            await cmd.ExecuteNonQueryAsync();
        }

        var connectionString = new SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = catalog,
        }.ConnectionString;

        await MigrateEveryModuleAsync(connectionString);
        await SeedTopicsAsync(connectionString, topicCount);
        await WaitForFullTextPopulationAsync(connectionString);

        return connectionString;
    }

    /*
     * ⚠ A HAND-MAINTAINED CONTEXT LIST IS A PLACE THAT CAN DRIFT, and WBS-24.5's lesson is that a
     * context has to be substituted in three places and omitting one fails confusingly. It is written
     * out anyway, exactly as SqlBackstopFixture does, because the alternative — reflecting over the
     * host's registrations — would migrate whatever the host happens to register and quietly stop
     * being a statement about which schemas this measurement needs. A missing context fails LOUDLY on
     * the first request that touches it, not silently.
     */
    private async Task MigrateEveryModuleAsync(string cs)
    {
        await using (var db = new MembershipDbContext(Options<MembershipDbContext>(cs, MembershipDbContext.Schema), _clock, _user))
            await db.Database.MigrateAsync();
        await using (var db = new TopicsDbContext(Options<TopicsDbContext>(cs, TopicsDbContext.Schema), _clock, _user))
            await db.Database.MigrateAsync();
        await using (var db = new MeetingsDbContext(Options<MeetingsDbContext>(cs, MeetingsDbContext.Schema), _clock, _user))
            await db.Database.MigrateAsync();
        await using (var db = new DecisionsDbContext(Options<DecisionsDbContext>(cs, DecisionsDbContext.Schema), _clock, _user))
            await db.Database.MigrateAsync();
        await using (var db = new ActionsDbContext(Options<ActionsDbContext>(cs, ActionsDbContext.Schema), _clock, _user))
            await db.Database.MigrateAsync();
        await using (var db = new RisksDbContext(Options<RisksDbContext>(cs, RisksDbContext.Schema), _clock, _user))
            await db.Database.MigrateAsync();
        await using (var db = new NotificationsDbContext(Options<NotificationsDbContext>(cs, NotificationsDbContext.Schema), _clock, _user))
            await db.Database.MigrateAsync();
        await using (var db = new DependenciesDbContext(Options<DependenciesDbContext>(cs, DependenciesDbContext.Schema), _clock, _user))
            await db.Database.MigrateAsync();
        await using (var db = new TraceabilityDbContext(Options<TraceabilityDbContext>(cs, TraceabilityDbContext.Schema), _clock, _user))
            await db.Database.MigrateAsync();
        await using (var db = new GovernanceDbContext(Options<GovernanceDbContext>(cs, GovernanceDbContext.Schema), _clock, _user))
            await db.Database.MigrateAsync();
        await using (var db = new KnowledgeDbContext(Options<KnowledgeDbContext>(cs, KnowledgeDbContext.Schema), _clock, _user))
            await db.Database.MigrateAsync();
        await using (var db = new ResearchDbContext(Options<ResearchDbContext>(cs, ResearchDbContext.Schema), _clock, _user))
            await db.Database.MigrateAsync();
        await using (var db = new AuditDbContext(Options<AuditDbContext>(cs, AuditDbContext.Schema)))
            await db.Database.MigrateAsync();
        await using (var db = new ConfigurationDbContext(Options<ConfigurationDbContext>(cs, ConfigurationDbContext.Schema)))
            await db.Database.MigrateAsync();
    }

    /*
     * ⛔⛔ SEED THROUGH THIS, NEVER THROUGH THE HOST'S SERVICE SCOPE — AND THE REASON IS INVISIBLE ON
     * THE InMemory PROVIDER EVERY OTHER API TEST USES. The host wires every module DbContext onto ONE
     * shared connection with an AmbientTransaction (ADR-0026 / NFR-042) that opens LAZILY on the first
     * module write and is committed by TransactionBehavior — a MediatR pipeline behaviour. A raw
     * SaveChangesAsync outside a MediatR command therefore opens that transaction and nothing ever
     * commits it: the scope disposes, it rolls back, and SaveChangesAsync REPORTS SUCCESS THROUGHOUT.
     * That is LL-079, measured here on 2026-09-09.
     *
     * A context built here has its OWN connection and no ambient transaction, so its writes autocommit.
     */
    public MeetingsDbContext NewMeetingsContext() =>
        new(Options<MeetingsDbContext>(ConnectionString, MeetingsDbContext.Schema), _clock, _user);

    private static DbContextOptions<T> Options<T>(string cs, string schema) where T : DbContext =>
        new DbContextOptionsBuilder<T>()
            .UseSqlServer(cs, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", schema))
            .Options;

    /*
     * Set-based raw INSERT, not EF entities. 10 000 entities would fire audit stamping and change
     * tracking per row and take minutes; this is one statement. The column list doubles as a schema
     * assertion — a renamed or newly NOT-NULL column fails here, loudly, at seed time.
     *
     * Ported from the generator that produced DW-103's original dataset, which survived only by
     * accident in a previous session's .scratch/ folder. Committing it is the repair.
     */
    private static async Task SeedTopicsAsync(string cs, int topicCount)
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

        await using var conn = new SqlConnection(cs);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.CommandTimeout = 300;
        cmd.Parameters.AddWithValue("@count", topicCount);
        await cmd.ExecuteNonQueryAsync();
    }

    /*
     * ⛔ WITHOUT THIS WAIT, NFR-004's NUMBER IS MEANINGLESS AND FAST. Full-text population is
     * ASYNCHRONOUS: the INSERT returns long before the index covers the rows, and a FREETEXT query
     * against an unpopulated index returns nothing, very quickly. That is the empty-subject failure
     * LL-074 names — a clean, confident measurement over no data.
     */
    private static async Task WaitForFullTextPopulationAsync(string cs)
    {
        await using var conn = new SqlConnection(cs);
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
