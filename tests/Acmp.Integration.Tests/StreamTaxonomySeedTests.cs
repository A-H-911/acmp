using System.Data.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Integration.Tests;

// S5 (ADR-0016 §3) — ADR-0042 step 1. Two things are proven here and neither can be proven anywhere
// else in the suite: that the migration's seed actually LANDED (a unit test would only re-assert the
// literals it was written from), and that "at most one wildcard" is enforced by the DATABASE rather
// than by application code that a future caller could bypass.
//
// ⚠ This is deliberately a seed/backstop test and NOT a test that stream scope works. It cannot be:
// no policy carries StreamScopeRequirement yet (DEF-057), so nothing reads IsWildcard in production.
// That wiring — and the test that proves a refusal — is ADR-0042 step 7.
[Collection(SqlBackstopCollection.Name)]
public sealed class StreamTaxonomySeedTests
{
    private const string SeededBy = "seed:ADR-0042";

    private readonly SqlBackstopFixture _fx;

    public StreamTaxonomySeedTests(SqlBackstopFixture fx) => _fx = fx;

    // The committee's streams, per ADR-0042 (operator-confirmed 2026-08-13). Written out here rather
    // than imported from the migration ON PURPOSE: an independently-stated expectation is what makes
    // this a check. Sharing one constant with the code under test would assert only that a literal
    // equals itself, which is the shape this project keeps paying for.
    private static readonly string[] ExpectedCodes =
        { "all-streams", "communications", "core", "government", "shared-services", "smart-cities" };

    [Fact] // the migration's seed reached a real database — the streams table is no longer empty
    public async Task StreamTaxonomy_IsSeededByMigration()
    {
        await using var db = _fx.NewMembershipSql();

        var seeded = await db.Streams.AsNoTracking()
            .Where(s => s.CreatedBy == SeededBy)
            .OrderBy(s => s.Code)
            .ToListAsync();

        seeded.Select(s => s.Code).Should().Equal(ExpectedCodes);
        // Codes are the stable ABAC scope key — a capitalised one would never match Stream.Code's
        // lowercasing contract and the mismatch would surface as a silent refusal, not an error.
        seeded.Should().OnlyContain(s => s.Code == s.Code.ToLowerInvariant());
        // Guardrail 9: no single-language user-facing string. name_ar is NOT NULL, so a missing
        // translation cannot be null here — it would arrive as an empty string.
        seeded.Should().OnlyContain(s => s.Name.En.Length > 0 && s.Name.Ar.Length > 0);
    }

    // Exactly one row may claim "unrestricted", and it is the one the design names.
    // ⚠ Materialised first and filtered IN MEMORY on purpose. `Where(s => s.IsWildcard)` against the
    // DbSet is translated to SQL and never executes the property, so it proves the COLUMN filters
    // while saying nothing about whether the flag survives materialisation — and the step-7 ABAC
    // reader will read the property, not the column. Both halves have to hold, so both are asserted.
    [Fact]
    public async Task ExactlyOneSeededStream_IsTheWildcard()
    {
        await using var db = _fx.NewMembershipSql();

        var seeded = await db.Streams.AsNoTracking().Where(s => s.CreatedBy == SeededBy).ToListAsync();

        seeded.Where(s => s.IsWildcard).Should().ContainSingle().Which.Code.Should().Be("all-streams");
        // The other five are delivery streams. Stated as a count rather than "not the wildcard" so a
        // seed that quietly marked two rows fails here as well as at the database index.
        seeded.Where(s => !s.IsWildcard).Should().HaveCount(5);
    }

    // ⚠ The constraint is proven against a RAW INSERT, and that is the realistic threat rather than a
    // convenience: there is no domain factory that sets IsWildcard (the wildcard is a seeded
    // singleton), so a second one can only ever arrive from a migration, a repair script or a bulk
    // insert — exactly the paths that bypass application code and exactly what a DB index backstops.
    [Fact]
    public async Task SecondWildcardStream_IsRejectedBySql()
    {
        await using var db = _fx.NewMembershipSql();

        var insertSecondWildcard = () => db.Database.ExecuteSqlRawAsync(
            "INSERT INTO membership.streams (Code, name_en, name_ar, PublicId, CreatedAt, CreatedBy, IsWildcard) " +
            "VALUES ({0}, N'Second wildcard', N'بديل', NEWID(), SYSDATETIMEOFFSET(), 'it-actor', 1)",
            $"wildcard-{Guid.NewGuid():N}"[..32]);

        await insertSecondWildcard.Should().ThrowAsync<DbException>();
    }

    // The same filtered index must NOT constrain ordinary streams — a filter written as a plain
    // unique index would cap the committee at one stream and pass every test above.
    [Fact]
    public async Task TwoAdditionalNonWildcardStreams_AreAllowedBySql()
    {
        await using var db = _fx.NewMembershipSql();

        var insertTwo = () => db.Database.ExecuteSqlRawAsync(
            "INSERT INTO membership.streams (Code, name_en, name_ar, PublicId, CreatedAt, CreatedBy, IsWildcard) " +
            "VALUES ({0}, N'Extra A', N'إضافي أ', NEWID(), SYSDATETIMEOFFSET(), 'it-actor', 0), " +
            "       ({1}, N'Extra B', N'إضافي ب', NEWID(), SYSDATETIMEOFFSET(), 'it-actor', 0)",
            $"extra-a-{Guid.NewGuid():N}"[..32], $"extra-b-{Guid.NewGuid():N}"[..32]);

        await insertTwo.Should().NotThrowAsync();
    }

    // Why the migration's seed is written as INSERT ... WHERE NOT EXISTS rather than InsertData: this
    // is the collision it avoids. A database that already carried a hand-inserted 'core' row would
    // fail a plain insert and take the whole deployment down with the migration.
    [Fact]
    public async Task DuplicateStreamCode_IsRejectedBySql()
    {
        await using var db = _fx.NewMembershipSql();

        var insertDuplicateCode = () => db.Database.ExecuteSqlRawAsync(
            "INSERT INTO membership.streams (Code, name_en, name_ar, PublicId, CreatedAt, CreatedBy, IsWildcard) " +
            "VALUES ('core', N'Duplicate core', N'مكرر', NEWID(), SYSDATETIMEOFFSET(), 'it-actor', 0)");

        await insertDuplicateCode.Should().ThrowAsync<DbException>();
    }
}
