using Acmp.Modules.Membership.Application.Features.CreateStream;
using Acmp.Modules.Membership.Application.Features.RenameStream;
using Acmp.Shared.Application.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Acmp.Integration.Tests;

// WBS-24.7 / DW-063 — NFR-010's CONFIGURATION-DRIVEN clause, on real SQL Server.
//
// ⚠ THIS TEST EXISTS BECAUSE THE REQUIREMENT ASKS FOR IT BY NAME. NFR-010's own verification note
// reads "Code review: confirm no magic number for stream count; integration test with 10 streams",
// and until now no such test existed — which is why the row could not record more than a partial.
//
// It cannot be a unit test. The InMemory provider does not enforce the unique index on Code or the
// FILTERED unique index that caps the wildcard at one row, so the two database-level guarantees this
// feature leans on would both pass vacuously there (DEF-066's lesson: the provider you test on
// decides what can pass). Only this project runs against a real SQL Server.
[Collection(SqlBackstopCollection.Name)]
public sealed class StreamConfigurationSqlTests
{
    private readonly SqlBackstopFixture _fx;

    public StreamConfigurationSqlTests(SqlBackstopFixture fx) => _fx = fx;

    // The fixture's database is shared across this collection and already carries the six seeded rows,
    // so every assertion below is scoped to codes this run created. A bare COUNT would couple this
    // test to whatever else happens to be in the table.
    private static string Prefix() => "wbs247-" + Guid.NewGuid().ToString("N")[..8];

    [Fact]
    public async Task Ten_streams_are_created_and_all_ten_are_projected_back_with_no_cap()
    {
        var prefix = Prefix();
        await using var db = _fx.NewMembershipSql();
        var handler = new CreateStreamHandler(db, Substitute.For<IAuditSink>());

        // TEN, because that is the number the requirement's verification note names. The seeded five
        // plus the wildcard are already present, so the table holds sixteen rows by the end and the
        // taxonomy is well past the five that go-live assumes.
        for (var i = 0; i < 10; i++)
        {
            await handler.Handle(
                new CreateStreamCommand($"{prefix}-{i}", $"Stream {i}", $"مسار {i}"), CancellationToken.None);
        }

        await using var reader = _fx.NewMembershipSql();
        var created = await reader.Streams.AsNoTracking()
            .Where(s => s.Code.StartsWith(prefix))
            .OrderBy(s => s.Code)
            .ToListAsync();

        created.Should().HaveCount(10, "the requirement's verification note asks for ten and nothing caps the count");
        created.Should().OnlyContain(s => s.Code == s.Code.ToLowerInvariant());
        created.Should().OnlyContain(s => s.Name.En.Length > 0 && s.Name.Ar.Length > 0);

        // None of them is the wildcard. Ten new streams must widen the taxonomy without widening
        // ADR-0043's bypass surface by a single row.
        created.Should().OnlyContain(s => !s.IsWildcard);

        // And the whole taxonomy is readable in one projection — the shape StreamCatalog uses.
        var total = await reader.Streams.AsNoTracking().CountAsync();
        total.Should().BeGreaterThanOrEqualTo(16);
    }

    [Fact]
    public async Task The_unique_index_is_the_real_backstop_when_the_handler_check_is_bypassed()
    {
        // The handler refuses a duplicate with a legible message, and that is proven by a unit test.
        // This proves the SECOND line of defence: that the database itself would refuse, so a future
        // caller reaching Stream.Create directly cannot create a colliding scope key. Asserting only
        // the handler would leave that entirely untested.
        var prefix = Prefix();
        await using var db = _fx.NewMembershipSql();
        await new CreateStreamHandler(db, Substitute.For<IAuditSink>())
            .Handle(new CreateStreamCommand($"{prefix}-dupe", "Dupe", "مكرر"), CancellationToken.None);

        await using var second = _fx.NewMembershipSql();
        second.Streams.Add(Acmp.Modules.Membership.Domain.Stream.Create(
            $"{prefix}-dupe", new Acmp.Shared.Domain.ValueObjects.LocalizedString("Dupe again", "مكرر ثانية")));

        var act = () => second.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>(
            "the unique index on Code must refuse a colliding scope key even when no application check runs");
    }

    [Fact]
    public async Task A_rename_survives_a_round_trip_and_never_moves_the_scope_key()
    {
        var prefix = Prefix();
        await using var db = _fx.NewMembershipSql();
        var publicId = await new CreateStreamHandler(db, Substitute.For<IAuditSink>())
            .Handle(new CreateStreamCommand($"{prefix}-rename", "Before", "قبل"), CancellationToken.None);

        await using var writer = _fx.NewMembershipSql();
        await new RenameStreamHandler(writer, Substitute.For<IAuditSink>())
            .Handle(new RenameStreamCommand(publicId, "After", "بعد"), CancellationToken.None);

        // Read through a THIRD context so the assertion cannot be served by a tracked entity.
        await using var reader = _fx.NewMembershipSql();
        var stored = await reader.Streams.AsNoTracking().SingleAsync(s => s.PublicId == publicId);

        stored.Name.En.Should().Be("After");
        stored.Name.Ar.Should().Be("بعد");
        stored.Code.Should().Be($"{prefix}-rename", "topics carry the code and the ABAC intersect resolves on it");
    }
}
