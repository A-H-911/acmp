using Acmp.Modules.Decisions.Infrastructure;
using Acmp.Modules.Decisions.Infrastructure.Persistence;
using Acmp.Modules.Membership.Infrastructure;
using Acmp.Modules.Membership.Infrastructure.Persistence;
using Acmp.Modules.Topics.Infrastructure;
using Acmp.Modules.Topics.Infrastructure.Persistence;
using Acmp.Shared;
using Acmp.Shared.Infrastructure.Audit;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Acmp.Integration.Tests;

// NFR-042 (ADR-0026). Startup boot risk with zero coverage elsewhere: MigrationRunner resolves every context
// from ONE scope and, now that they all share a single DbConnection, migrates them sequentially on that one
// connection. That path only runs at full-stack boot (never under EF-InMemory), so a regression here means the
// app does not start. This exercises the exact shape — several real modules + AuditDbContext migrating in turn
// on the shared connection (with the audit/atomicity interceptors attached) — against a fresh empty database.
[Collection(SqlBackstopCollection.Name)]
public sealed class MigrationBootOnSharedConnectionTests
{
    private readonly SqlBackstopFixture _fixture;

    public MigrationBootOnSharedConnectionTests(SqlBackstopFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Every_context_migrates_sequentially_on_the_one_shared_connection()
    {
        // A fresh, empty DB so this drives real DDL — the fixture's DB is already migrated (on separate
        // connections), where a re-migrate would be a no-op and prove nothing.
        var dbName = "BootTest_" + Guid.NewGuid().ToString("N")[..8];
        await using (var master = new SqlConnection(_fixture.ConnectionString))
        {
            await master.OpenAsync();
            await using var cmd = master.CreateCommand();
            cmd.CommandText = $"CREATE DATABASE [{dbName}]";
            await cmd.ExecuteNonQueryAsync();
        }

        var connString = new SqlConnectionStringBuilder(_fixture.ConnectionString) { InitialCatalog = dbName }
            .ConnectionString;
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Acmp"] = connString })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSharedKernel(config);
        services.AddMembershipModule(config);
        services.AddTopicsModule(config);
        services.AddDecisionsModule(config);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        // Mirrors MigrationRunner: contexts resolved from one scope share the connection, migrated in turn.
        DbContext[] contexts =
        {
            sp.GetRequiredService<MembershipDbContext>(),
            sp.GetRequiredService<TopicsDbContext>(),
            sp.GetRequiredService<DecisionsDbContext>(),
            sp.GetRequiredService<AuditDbContext>(),
        };

        var migrate = async () =>
        {
            foreach (var db in contexts)
                await db.Database.MigrateAsync();
        };
        await migrate.Should().NotThrowAsync("all contexts must migrate on the one shared connection at boot");

        // A round-trip against the freshly migrated schema proves the connection is usable afterwards.
        (await sp.GetRequiredService<DecisionsDbContext>().Votes.CountAsync())
            .Should().Be(0, "the decisions schema is live on the shared connection");
    }
}
