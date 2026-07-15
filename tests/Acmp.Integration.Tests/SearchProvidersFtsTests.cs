using Acmp.Modules.Decisions.Infrastructure.Persistence;
using Acmp.Modules.Decisions.Infrastructure.Search;
using Acmp.Modules.Governance.Infrastructure.Persistence;
using Acmp.Modules.Governance.Infrastructure.Search;
using Acmp.Modules.Knowledge.Infrastructure.Persistence;
using Acmp.Modules.Knowledge.Infrastructure.Search;
using Acmp.Modules.Meetings.Infrastructure.Persistence;
using Acmp.Modules.Meetings.Infrastructure.Search;
using Acmp.Modules.Topics.Infrastructure.Persistence;
using Acmp.Modules.Topics.Infrastructure.Search;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Contracts.Search;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Images;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace Acmp.Integration.Tests;

// AC-061 (FR-143/145/118, OQ-034) — the honest real-stack proof of global search on a Full-Text-Search-enabled
// SQL Server (the stock mssql image ships without FTS). The InMemory API suite cannot translate FREETEXT, so
// this is the ONLY place every ISearchProvider's SQL-Server branch runs: it builds the FTS deploy image, boots
// it, migrates all five source schemas (the guarded CREATE FULLTEXT INDEX fires because IsFullTextInstalled=1),
// seeds Arabic Decisions, waits out the async population, and asserts the Arabic word-breaker (LCID 1025) finds
// them. The other four providers run their FREETEXT path against their (freshly indexed) tables to prove the
// query executes end-to-end. Docker-gated, like MinioFileStoreTests.
public sealed class SearchProvidersFtsTests : IAsyncLifetime
{
    private readonly IFutureDockerImage _image = new ImageFromDockerfileBuilder()
        .WithDockerfileDirectory(CommonDirectoryPath.GetSolutionDirectory(), "deploy")
        .WithDockerfile("Dockerfile.sqlserver")
        .WithName("acmp/sqlserver-fts:test")
        .WithCleanUp(false) // keep the built image cached across runs (~160s to build)
        .Build();

    private MsSqlContainer _container = null!;
    private string _appConnectionString = null!;

    private readonly IClock _clock = new TestClock();
    private readonly ICurrentUser _user = new TestCurrentUser();

    private DecisionsDbContext _decisions = null!;
    private TopicsDbContext _topics = null!;
    private GovernanceDbContext _governance = null!;
    private MeetingsDbContext _meetings = null!;
    private KnowledgeDbContext _knowledge = null!;

    public async Task InitializeAsync()
    {
        await _image.CreateAsync();
        _container = new MsSqlBuilder().WithImage(_image).Build();
        await _container.StartAsync();

        // A full-text catalog cannot live in master/tempdb/model (MsSqlBuilder connects to master) — so create
        // a real application database and migrate every module into it.
        await CreateAppDatabaseAsync();

        _decisions = new DecisionsDbContext(Options<DecisionsDbContext>(DecisionsDbContext.Schema), _clock, _user);
        _topics = new TopicsDbContext(Options<TopicsDbContext>(TopicsDbContext.Schema), _clock, _user);
        _governance = new GovernanceDbContext(Options<GovernanceDbContext>(GovernanceDbContext.Schema), _clock, _user);
        _meetings = new MeetingsDbContext(Options<MeetingsDbContext>(MeetingsDbContext.Schema), _clock, _user);
        _knowledge = new KnowledgeDbContext(Options<KnowledgeDbContext>(KnowledgeDbContext.Schema), _clock, _user);

        foreach (var db in AllContexts())
            await db.Database.MigrateAsync(); // builds each schema + its guarded full-text index

        await SeedDecisionsAsync();
        await SeedTopicAsync();
        await SeedAdrAsync();
        await SeedMinutesAsync();
        await SeedDocumentAsync();
        await WaitForPopulationAsync("ft_decisions");
    }

    public async Task DisposeAsync()
    {
        foreach (var db in AllContexts())
            await db.DisposeAsync();
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task Arabic_query_matches_via_word_breaker_and_returns_english_too()
    {
        var provider = new DecisionSearchProvider(_decisions);

        var arabic = await provider.SearchAsync("قرار", 10);
        arabic.Should().Contain(h => h.Key == "DECN-2026-901", "the Arabic architecture decision must be found");
        arabic.Should().NotContain(h => h.Key == "DECN-2026-902", "the unrelated risk decision must not match");

        // Arabic round-trips with no encoding corruption (AC-061 "no character encoding errors").
        arabic.First(h => h.Key == "DECN-2026-901").Title.Ar.Should().Be("قرار معماري");

        // English query hits the same corpus via the English word-breaker (AC-061 "relevant English results").
        var english = await provider.SearchAsync("architecture", 10);
        english.Should().Contain(h => h.Key == "DECN-2026-901");
    }

    [Fact]
    public async Task Every_provider_executes_its_full_text_query_on_real_sql_server()
    {
        // Each provider's SQL-Server FREETEXT branch runs against its freshly-built full-text index. Empty tables
        // are fine — the point is that the query translates and executes with no SQL error (index present,
        // columns/languages valid), which the InMemory suite can never prove.
        ISearchProvider[] providers =
        {
            new TopicSearchProvider(_topics),
            new AdrSearchProvider(_governance),
            new MinutesSearchProvider(_meetings),
            new DocumentSearchProvider(_knowledge),
            new DecisionSearchProvider(_decisions),
        };

        foreach (var provider in providers)
        {
            var hits = await provider.SearchAsync("architecture", 5);
            hits.Should().NotBeEmpty(
                $"provider {provider.ArtifactType} should find its seeded 'architecture' row via its FREETEXT/LIKE query");
            hits[0].DeepLink.Should().NotBeNullOrWhiteSpace();
        }
    }

    private IEnumerable<DbContext> AllContexts()
    {
        yield return _decisions;
        yield return _topics;
        yield return _governance;
        yield return _meetings;
        yield return _knowledge;
    }

    private DbContextOptions<T> Options<T>(string schema) where T : DbContext =>
        new DbContextOptionsBuilder<T>()
            .UseSqlServer(_appConnectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", schema))
            .Options;

    private async Task CreateAppDatabaseAsync()
    {
        await using var conn = new SqlConnection(_container.GetConnectionString());
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "IF DB_ID('Acmp') IS NULL CREATE DATABASE Acmp;";
        await cmd.ExecuteNonQueryAsync();

        _appConnectionString = new SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = "Acmp",
        }.ConnectionString;
    }

    // Raw insert bypasses EF's audit stamping, so every NOT-NULL-without-default column is supplied here:
    // ChairOverride (bit) and CreatedAt (datetimeoffset) alongside the searchable text. Arabic body carries the
    // definite-article inflected form (القرار) so the word-breaker — not a substring — is what must match.
    private async Task SeedDecisionsAsync()
    {
        const string sql =
            "INSERT INTO decisions.decisions (PublicId,[Key],TopicId,Outcome,Status,ChairOverride,CreatedAt,CreatedBy," +
            "title_en,title_ar,statement_en,statement_ar,rationale_en,rationale_ar) VALUES " +
            "(NEWID(),'DECN-2026-901',NEWID(),0,0,0,SYSDATETIMEOFFSET(),'seed'," +
            "N'Architecture decision',N'قرار معماري'," +
            "N'The architectural decision was issued after voting',N'تم إصدار القرار المعماري بعد التصويت'," +
            "N'Rationale',N'الأساس المنطقي للقرار')," +
            "(NEWID(),'DECN-2026-902',NEWID(),0,0,0,SYSDATETIMEOFFSET(),'seed'," +
            "N'Budget risk',N'مخاطر الميزانية'," +
            "N'An unrelated risk record',N'سجل مخاطر غير ذي صلة'," +
            "N'Rationale',N'الأساس المنطقي')";
        await _decisions.Database.ExecuteSqlRawAsync(sql);
    }

    // One matching row per remaining source, each carrying "Architecture" so the providers return a hit and
    // their SearchHit projection is exercised. Only NOT-NULL columns without a default are supplied.
    private Task SeedTopicAsync() => _topics.Database.ExecuteSqlRawAsync(
        "INSERT INTO topics.topics (PublicId,[Key],Title,Description,Type,Urgency,Scope,Source,Status,Priority," +
        "SubmittedBySub,SubmittedByName,streams,systems,tags,CreatedAt,CreatedBy) VALUES " +
        "(NEWID(),'TOP-2026-901',N'Architecture roadmap',N'خطة معمارية للجنة',0,0,0,0,0,0," +
        "'seed','Seed','[]','[]','[]',SYSDATETIMEOFFSET(),'seed')");

    private Task SeedAdrAsync() => _governance.Database.ExecuteSqlRawAsync(
        "INSERT INTO governance.adrs (PublicId,[Key],Status,title_en,title_ar,context_en,context_ar," +
        "decision_en,decision_ar,AuthorUserId,AuthorName,CreatedAt,CreatedBy) VALUES " +
        "(NEWID(),'ADR-2026-901',0,N'Architecture record',N'سجل معماري',N'Context',N'سياق'," +
        "N'Decision',N'قرار',' seed','Seed',SYSDATETIMEOFFSET(),'seed')");

    private Task SeedMinutesAsync() => _meetings.Database.ExecuteSqlRawAsync(
        "INSERT INTO meetings.minutes_of_meeting (PublicId,[Key],Version,MeetingId,MeetingKey,MeetingTitle,Status," +
        "summary_en,summary_ar,ApprovedBySoleAuthor,CreatedAt,CreatedBy) VALUES " +
        "(NEWID(),'MIN-2026-901',1,NEWID(),'MTG-2026-901',N'Meeting',0," +
        "N'Architecture minutes summary',N'ملخص معماري',0,SYSDATETIMEOFFSET(),'seed')");

    private Task SeedDocumentAsync() => _knowledge.Database.ExecuteSqlRawAsync(
        "INSERT INTO knowledge.documents (PublicId,[Key],Status,title_en,title_ar,body_en,body_ar,Category," +
        "OwnerUserId,Version,tags,CreatedAt,CreatedBy) VALUES " +
        "(NEWID(),'DOC-2026-901',0,N'Architecture guide',N'دليل معماري',N'Body',N'محتوى',N'General'," +
        "'seed',1,'[]',SYSDATETIMEOFFSET(),'seed')");

    // FTS population is asynchronous — querying before it settles yields false misses (the spike lesson).
    // PopulateStatus is NULL until the catalog is registered/ready, so poll as int? and treat null as
    // "not settled yet, keep waiting" (a bare int cast throws "Nullable object must have a value" under load).
    private async Task WaitForPopulationAsync(string catalog)
    {
        for (var i = 0; i < 60; i++)
        {
            var status = await _decisions.Database
                .SqlQuery<int?>($"SELECT CAST(FULLTEXTCATALOGPROPERTY({catalog},'PopulateStatus') AS int) AS Value")
                .FirstAsync();
            if (status == 0)
                return;
            await Task.Delay(1000);
        }
    }
}
