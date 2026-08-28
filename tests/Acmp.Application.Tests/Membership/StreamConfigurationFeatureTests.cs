using Acmp.Modules.Membership.Application.Features.CreateStream;
using Acmp.Modules.Membership.Application.Features.RenameStream;
using Acmp.Modules.Membership.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using DomainStream = Acmp.Modules.Membership.Domain.Stream;

namespace Acmp.Application.Tests.Membership;

// WBS-24.7 / DW-063 / NFR-010 — the CONFIGURATION-DRIVEN clause. Before this feature the five streams
// were seeded by raw SQL inside a migration and Stream.Create had no caller, so adding a sixth was a
// code change and a deployment. Every guard below is proven by FORCING its refusal, not by asserting
// that a happy path happens to work.
public class StreamConfigurationFeatureTests
{
    private static MembershipDbContext MakeDb(string dbName)
    {
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        var user = Substitute.For<ICurrentUser>();
        user.IsAuthenticated.Returns(true);
        user.UserId.Returns("kc-admin");
        user.Roles.Returns(new[] { "Administrator" });
        return new MembershipDbContext(
            new DbContextOptionsBuilder<MembershipDbContext>().UseInMemoryDatabase(dbName).Options,
            clock, user);
    }

    private static CreateStreamCommand NewStream(string code = "platform") =>
        new(code, "Platform", "المنصّة");

    [Fact]
    public async Task Creates_a_stream_and_audits_it_under_the_entity_name()
    {
        await using var db = MakeDb("stream-create-" + Guid.NewGuid());
        var audit = Substitute.For<IAuditSink>();

        var publicId = await new CreateStreamHandler(db, audit).Handle(NewStream(), CancellationToken.None);

        var stored = await db.Streams.SingleAsync();
        stored.PublicId.Should().Be(publicId);
        stored.Code.Should().Be("platform");
        stored.Name.En.Should().Be("Platform");
        stored.Name.Ar.Should().Be("المنصّة");

        // INV-005: a real create is audited. The subject type must be "Stream" and not the local
        // alias — nameof(DomainStream) would write "DomainStream", which matches no other audit row.
        await audit.Received(1).EmitEnrichedAsync("Membership.StreamCreated", "Stream",
            publicId.ToString(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task The_code_is_normalised_so_case_and_padding_cannot_smuggle_a_duplicate_past_the_check()
    {
        await using var db = MakeDb("stream-normalise-" + Guid.NewGuid());

        await new CreateStreamHandler(db, Substitute.For<IAuditSink>())
            .Handle(NewStream("  MoBiLe  "), CancellationToken.None);

        (await db.Streams.SingleAsync()).Code.Should().Be("mobile");
    }

    [Fact]
    public async Task Refuses_a_duplicate_code_across_case_and_padding_and_audits_nothing()
    {
        await using var db = MakeDb("stream-dupe-" + Guid.NewGuid());
        var audit = Substitute.For<IAuditSink>();
        await new CreateStreamHandler(db, audit).Handle(NewStream("platform"), CancellationToken.None);
        audit.ClearReceivedCalls();

        // FORCED REFUSAL. The duplicate check runs on the NORMALISED code, so this must be refused —
        // if it compared the raw input, " PLATFORM " would pass here and then fail on the unique index
        // as an opaque 500 instead of a legible message.
        var act = () => new CreateStreamHandler(db, audit).Handle(NewStream(" PLATFORM "), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already exists*");
        (await db.Streams.CountAsync()).Should().Be(1);
        await audit.DidNotReceiveWithAnyArgs().EmitEnrichedAsync(default!, default!, default, default!, default);
    }

    [Fact]
    public async Task A_stream_created_at_runtime_is_never_the_wildcard()
    {
        // ADR-0043's bypass surface cannot be widened through this path. Stream.Create does not set
        // IsWildcard and there is deliberately no runtime factory that does, so this holds by
        // construction rather than by a check a later edit could drop.
        await using var db = MakeDb("stream-wildcard-" + Guid.NewGuid());

        await new CreateStreamHandler(db, Substitute.For<IAuditSink>())
            .Handle(NewStream("data"), CancellationToken.None);

        (await db.Streams.SingleAsync()).IsWildcard.Should().BeFalse();
    }

    [Fact]
    public async Task Rename_changes_the_display_text_and_leaves_the_scope_key_untouched()
    {
        await using var db = MakeDb("stream-rename-" + Guid.NewGuid());
        var stream = DomainStream.Create("platform", new LocalizedString("Platform", "المنصّة"));
        db.Streams.Add(stream);
        await db.SaveChangesAsync();
        var audit = Substitute.For<IAuditSink>();

        await new RenameStreamHandler(db, audit)
            .Handle(new RenameStreamCommand(stream.PublicId, "Platform & Infrastructure", "المنصّة والبنية"),
                CancellationToken.None);

        var stored = await db.Streams.SingleAsync();
        stored.Name.En.Should().Be("Platform & Infrastructure");
        stored.Name.Ar.Should().Be("المنصّة والبنية");

        // The CODE is what topics carry and what the ABAC intersect resolves on. A rename that moved
        // it would silently re-scope every topic naming the old value.
        stored.Code.Should().Be("platform");

        await audit.Received(1).EmitEnrichedAsync("Membership.StreamRenamed", "Stream",
            stream.PublicId.ToString(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Renaming_a_stream_that_does_not_exist_is_refused_and_audits_nothing()
    {
        await using var db = MakeDb("stream-rename-404-" + Guid.NewGuid());
        var audit = Substitute.For<IAuditSink>();

        var act = () => new RenameStreamHandler(db, audit)
            .Handle(new RenameStreamCommand(Guid.NewGuid(), "X", "س"), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
        await audit.DidNotReceiveWithAnyArgs().EmitEnrichedAsync(default!, default!, default, default!, default);
    }

    [Theory]
    [InlineData("", "empty code")]
    [InlineData("-leading", "a leading hyphen would produce an unusable scope key")]
    [InlineData("has space", "a space breaks the code as a claim/URL-safe key")]
    [InlineData("bad_underscore", "underscores are outside the agreed character set")]
    public void Invalid_codes_are_rejected_by_the_validator(string code, string because)
    {
        new CreateStreamValidator().Validate(NewStream() with { Code = code })
            .IsValid.Should().BeFalse(because);
    }

    [Fact]
    public void A_code_at_the_column_limit_passes_and_one_character_over_does_not()
    {
        var validator = new CreateStreamValidator();
        // 64 is the column width in StreamConfiguration; the boundary is asserted in BOTH directions so
        // a silently widened rule cannot pass this test.
        validator.Validate(NewStream() with { Code = new string('a', 64) }).IsValid.Should().BeTrue();
        validator.Validate(NewStream() with { Code = new string('a', 65) }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Both_halves_of_the_bilingual_name_are_required()
    {
        var validator = new CreateStreamValidator();
        validator.Validate(NewStream() with { NameEn = "" }).IsValid.Should().BeFalse();
        validator.Validate(NewStream() with { NameAr = "" }).IsValid.Should().BeFalse();
        validator.Validate(NewStream()).IsValid.Should().BeTrue();
    }

    [Fact]
    public void The_rename_validator_requires_an_id_and_both_names()
    {
        var validator = new RenameStreamValidator();
        var valid = new RenameStreamCommand(Guid.NewGuid(), "Platform", "المنصّة");

        validator.Validate(valid).IsValid.Should().BeTrue();
        validator.Validate(valid with { PublicId = Guid.Empty }).IsValid.Should().BeFalse();
        validator.Validate(valid with { NameEn = "" }).IsValid.Should().BeFalse();
        validator.Validate(valid with { NameAr = "" }).IsValid.Should().BeFalse();
    }
}
