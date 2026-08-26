using Acmp.Shared.Infrastructure.Configuration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Integration.Tests;

/*
 * WBS-24.5 (DW-036 / FR-155, NFR-059, NFR-060; DEC-080 / SC-035) — the Configuration store on REAL SQL
 * Server.
 *
 * WHY THIS FILE HAS TO EXIST. SEC-103 specifies `Key` as UNIQUE, and that constraint is the whole reason
 * "the value of this setting" is a well-formed question: without it a second row for the same key is
 * invisible and a reader gets whichever the query happens to return first. EF InMemory — which
 * Acmp.Api.Tests runs on — DOES NOT ENFORCE UNIQUE INDEXES AT ALL, so the endpoint tests next door pass
 * whether the index exists or not. DEF-066 is this project's record of what that costs: stream assignment
 * had never worked on a real database under four green suites.
 *
 * These tests therefore assert two different things, and the split is deliberate:
 *   - the DATABASE refuses a duplicate key (only SQL Server can show this), and
 *   - the migration actually creates the schema and table SEC-103 names (only a real migration can).
 */
[Collection(SqlBackstopCollection.Name)]
public sealed class ConfigurationStoreSqlTests
{
    private readonly SqlBackstopFixture _fx;

    public ConfigurationStoreSqlTests(SqlBackstopFixture fx) => _fx = fx;

    private static string UniqueKey() => $"retention.test.{Guid.NewGuid():N}";

    [Fact] // SEC-103: Key is UNIQUE. The database refuses the second row — InMemory would accept it.
    public async Task Duplicate_key_is_refused_by_the_database()
    {
        var key = UniqueKey();

        await using (var seed = _fx.NewConfigurationSql())
        {
            seed.Settings.Add(ConfigurationSetting.Create(key, "{\"years\":7}", "retention"));
            await seed.SaveChangesAsync();
        }

        await using var second = _fx.NewConfigurationSql();
        // A DIFFERENT row (its own Guid) carrying the SAME key. This is precisely what the unique index
        // exists to stop, and precisely what an upsert that forgot to look first would produce.
        second.Settings.Add(ConfigurationSetting.Create(key, "{\"years\":10}", "retention"));

        var act = async () => await second.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact] // The value round-trips through a real nvarchar(max) column, JSON and all.
    public async Task A_setting_round_trips_through_real_sql()
    {
        var key = UniqueKey();
        // Non-ASCII on purpose: the column is NVARCHAR, and a value legal writes may well be Arabic.
        const string value = "{\"years\":7,\"note\":\"الاحتفاظ بالسجلات\"}";

        await using (var write = _fx.NewConfigurationSql())
        {
            write.Settings.Add(ConfigurationSetting.Create(key, value, "retention"));
            await write.SaveChangesAsync();
        }

        await using var read = _fx.NewConfigurationSql();
        var stored = await read.Settings.SingleAsync(s => s.Key == key);
        stored.ValueJson.Should().Be(value);
        stored.Scope.Should().Be("retention");
    }

    [Fact] // Updating in place keeps ONE row: the key is identity, so an upsert must not accumulate rows.
    public async Task Updating_a_setting_keeps_exactly_one_row()
    {
        var key = UniqueKey();

        await using (var write = _fx.NewConfigurationSql())
        {
            write.Settings.Add(ConfigurationSetting.Create(key, "{\"years\":7}", "retention"));
            await write.SaveChangesAsync();
        }

        await using (var update = _fx.NewConfigurationSql())
        {
            var row = await update.Settings.SingleAsync(s => s.Key == key);
            row.SetValue("{\"years\":10}");
            await update.SaveChangesAsync();
        }

        await using var read = _fx.NewConfigurationSql();
        (await read.Settings.CountAsync(s => s.Key == key)).Should().Be(1);
        (await read.Settings.SingleAsync(s => s.Key == key)).ValueJson.Should().Be("{\"years\":10}");
    }

    [Fact] // The migration creates what SEC-103 names, not something merely similar.
    public async Task The_migration_creates_the_specified_schema_and_table()
    {
        await using var db = _fx.NewConfigurationSql();

        var count = await db.Database
            .SqlQuery<int>($@"SELECT COUNT(*) AS Value FROM sys.tables t
                              JOIN sys.schemas s ON s.schema_id = t.schema_id
                              WHERE s.name = 'config' AND t.name = 'Configuration'")
            .SingleAsync();

        count.Should().Be(1, "SEC-103 names the table `Configuration` in schema `config`");
    }
}
