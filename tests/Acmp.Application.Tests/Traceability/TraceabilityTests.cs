using Acmp.Modules.Traceability.Application.Features.CreateRelationship;
using Acmp.Modules.Traceability.Application.Features.DeactivateRelationship;
using Acmp.Modules.Traceability.Application.Features.GetArtifactRelationships;
using Acmp.Modules.Traceability.Domain;
using Acmp.Modules.Traceability.Domain.Enums;
using Acmp.Modules.Traceability.Infrastructure.Directory;
using Acmp.Modules.Traceability.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Acmp.Application.Tests.Traceability;

// Round-trips through the real TraceabilityDbContext (InMemory): the Relationship edge mapping, the
// create/deactivate flow with audit, the outgoing/incoming panel projection (AC-062/063), and the curated
// AC-029 downstream predicate (ITraceabilityLinks). Both the widened-gate arm and the panel roundtrip are
// proven here without a running SQL Server.
public class TraceabilityTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);

    private static TraceabilityDbContext Db(string name, ICurrentUser user, IClock clock) =>
        new(new DbContextOptionsBuilder<TraceabilityDbContext>().UseInMemoryDatabase(name).Options, clock, user);

    private static TraceabilityDbContext NewDb(ICurrentUser user, IClock clock) =>
        Db("trace-" + Guid.NewGuid(), user, clock);

    private static ICurrentUser User(string sub = "kc-sec", string name = "Sam Secretary")
    {
        var u = Substitute.For<ICurrentUser>();
        u.IsAuthenticated.Returns(true);
        u.UserId.Returns(sub);
        u.DisplayName.Returns(name);
        return u;
    }

    private static IClock Clock(DateTimeOffset now)
    {
        var c = Substitute.For<IClock>();
        c.UtcNow.Returns(now);
        return c;
    }

    private static CreateRelationshipCommand CreateCmd(
        ArtifactType st = ArtifactType.Topic, Guid? sid = null, string sk = "TOP-2026-042", string stitle = "API Gateway",
        ArtifactType tt = ArtifactType.Decision, Guid? tid = null, string tk = "DECN-2026-007", string ttitle = "Approve gateway",
        RelationshipType rel = RelationshipType.DecidedBy, string? notes = null) =>
        new(st, sid ?? Guid.NewGuid(), sk, stitle, tt, tid ?? Guid.NewGuid(), tk, ttitle, rel, notes);

    // ---- CreateRelationship ------------------------------------------------------------------------------

    [Fact] // AC-063: an edge is persisted with both endpoint snapshots and the create is audited.
    public async Task Create_persists_the_edge_and_audits()
    {
        var name = "c-" + Guid.NewGuid();
        var audit = Substitute.For<IAuditSink>();
        var cmd = CreateCmd(notes: "linked at the June meeting");

        Guid id;
        await using (var db = Db(name, User(), Clock(Now)))
            id = await new CreateRelationshipHandler(db, User(), audit).Handle(cmd, default);

        await using var read = Db(name, User(), Clock(Now));
        var edge = await read.Relationships.SingleAsync();
        edge.PublicId.Should().Be(id);
        edge.SourceKey.Should().Be("TOP-2026-042");
        edge.TargetKey.Should().Be("DECN-2026-007");
        edge.RelType.Should().Be(RelationshipType.DecidedBy);
        edge.IsActive.Should().BeTrue();
        edge.Notes.Should().Be("linked at the June meeting");
        edge.CreatedBy.Should().Be("kc-sec");
        await audit.Received(1).EmitAsync("Relationship.Created", "kc-sec", Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Fact] // The domain guard rejects a self-loop (defence in depth behind the validator).
    public async Task Create_rejects_a_self_loop()
    {
        var id = Guid.NewGuid();
        await using var db = NewDb(User(), Clock(Now));
        var act = () => new CreateRelationshipHandler(db, User(), Substitute.For<IAuditSink>())
            .Handle(CreateCmd(st: ArtifactType.Topic, sid: id, tt: ArtifactType.Topic, tid: id, rel: RelationshipType.DependsOn), default);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*itself*");
    }

    [Theory] // The validator blocks malformed edges before the handler runs.
    [InlineData(true, false, false)]   // self-loop
    [InlineData(false, true, false)]   // empty source id
    [InlineData(false, false, true)]   // missing source key
    public void Validator_rejects_invalid_commands(bool selfLoop, bool emptySource, bool missingKey)
    {
        var id = Guid.NewGuid();
        var cmd = selfLoop
            ? CreateCmd(st: ArtifactType.Topic, sid: id, tt: ArtifactType.Topic, tid: id)
            : emptySource ? CreateCmd(sid: Guid.Empty)
            : CreateCmd(sk: missingKey ? "" : "TOP-2026-042");

        new CreateRelationshipValidator().Validate(cmd).IsValid.Should().BeFalse();
    }

    [Fact] // A well-formed command passes validation.
    public void Validator_accepts_a_valid_command() =>
        new CreateRelationshipValidator().Validate(CreateCmd()).IsValid.Should().BeTrue();

    // ---- DeactivateRelationship --------------------------------------------------------------------------

    [Fact] // Soft-delete: the row stays with IsActive=0 (audit trail preserved) and the deactivation is audited.
    public async Task Deactivate_soft_deletes_and_audits()
    {
        var name = "d-" + Guid.NewGuid();
        var audit = Substitute.For<IAuditSink>();
        Guid id;
        await using (var db = Db(name, User(), Clock(Now)))
            id = await new CreateRelationshipHandler(db, User(), Substitute.For<IAuditSink>()).Handle(CreateCmd(), default);

        await using (var db = Db(name, User("kc-chair", "Chair"), Clock(Now)))
            await new DeactivateRelationshipHandler(db, User("kc-chair", "Chair"), Clock(Now), audit)
                .Handle(new DeactivateRelationshipCommand(id), default);

        await using var read = Db(name, User(), Clock(Now));
        var edge = await read.Relationships.SingleAsync();
        edge.IsActive.Should().BeFalse();
        edge.DeactivatedByUserId.Should().Be("kc-chair");
        edge.DeactivatedAt.Should().Be(Now);
        await audit.Received(1).EmitAsync("Relationship.Deactivated", "kc-chair", Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Fact] // Deactivating an unknown edge is a 404 (KeyNotFound).
    public async Task Deactivate_unknown_throws()
    {
        await using var db = NewDb(User(), Clock(Now));
        var act = () => new DeactivateRelationshipHandler(db, User(), Clock(Now), Substitute.For<IAuditSink>())
            .Handle(new DeactivateRelationshipCommand(Guid.NewGuid()), default);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact] // Deactivating an already-inactive edge is a no-op on state but still records the attempt.
    public async Task Deactivate_is_idempotent()
    {
        var name = "di-" + Guid.NewGuid();
        Guid id;
        await using (var db = Db(name, User(), Clock(Now)))
            id = await new CreateRelationshipHandler(db, User(), Substitute.For<IAuditSink>()).Handle(CreateCmd(), default);

        var firstAt = new DateTimeOffset(2026, 3, 2, 0, 0, 0, TimeSpan.Zero);
        await using (var db = Db(name, User(), Clock(firstAt)))
            await new DeactivateRelationshipHandler(db, User(), Clock(firstAt), Substitute.For<IAuditSink>())
                .Handle(new DeactivateRelationshipCommand(id), default);

        var laterAt = new DateTimeOffset(2026, 3, 9, 0, 0, 0, TimeSpan.Zero);
        await using (var db = Db(name, User("kc-x", "X"), Clock(laterAt)))
            await new DeactivateRelationshipHandler(db, User("kc-x", "X"), Clock(laterAt), Substitute.For<IAuditSink>())
                .Handle(new DeactivateRelationshipCommand(id), default);

        await using var read = Db(name, User(), Clock(Now));
        var edge = await read.Relationships.SingleAsync();
        edge.DeactivatedByUserId.Should().Be("kc-sec");    // unchanged by the second call
        edge.DeactivatedAt.Should().Be(firstAt);
    }

    // ---- GetArtifactRelationships (panel) ----------------------------------------------------------------

    [Fact] // AC-062/063: the edge shows as OUTGOING on its source and INCOMING on its target, with the far
           // endpoint ("other") resolved to the opposite end each way.
    public async Task Panel_groups_outgoing_and_incoming()
    {
        var name = "p-" + Guid.NewGuid();
        var topicId = Guid.NewGuid();
        var decisionId = Guid.NewGuid();
        await using (var db = Db(name, User(), Clock(Now)))
            await new CreateRelationshipHandler(db, User(), Substitute.For<IAuditSink>())
                .Handle(CreateCmd(st: ArtifactType.Topic, sid: topicId, sk: "TOP-2026-042", stitle: "API Gateway",
                    tt: ArtifactType.Decision, tid: decisionId, tk: "DECN-2026-007", ttitle: "Approve gateway",
                    rel: RelationshipType.DecidedBy), default);

        await using var db2 = Db(name, User(), Clock(Now));

        var topicPanel = await new GetArtifactRelationshipsHandler(db2)
            .Handle(new GetArtifactRelationshipsQuery(ArtifactType.Topic, topicId), default);
        topicPanel.Outgoing.Should().ContainSingle();
        topicPanel.Incoming.Should().BeEmpty();
        var outEdge = topicPanel.Outgoing[0];
        outEdge.Direction.Should().Be("Outgoing");
        outEdge.RelType.Should().Be("DecidedBy");
        outEdge.OtherType.Should().Be("Decision");
        outEdge.OtherId.Should().Be(decisionId);
        outEdge.OtherKey.Should().Be("DECN-2026-007");
        outEdge.OtherTitle.Should().Be("Approve gateway");

        var decisionPanel = await new GetArtifactRelationshipsHandler(db2)
            .Handle(new GetArtifactRelationshipsQuery(ArtifactType.Decision, decisionId), default);
        decisionPanel.Outgoing.Should().BeEmpty();
        decisionPanel.Incoming.Should().ContainSingle();
        decisionPanel.Incoming[0].Direction.Should().Be("Incoming");
        decisionPanel.Incoming[0].OtherType.Should().Be("Topic");
        decisionPanel.Incoming[0].OtherKey.Should().Be("TOP-2026-042");
    }

    [Fact] // Deactivated edges never surface on the panel.
    public async Task Panel_excludes_inactive_edges()
    {
        var name = "pi-" + Guid.NewGuid();
        var topicId = Guid.NewGuid();
        Guid id;
        await using (var db = Db(name, User(), Clock(Now)))
            id = await new CreateRelationshipHandler(db, User(), Substitute.For<IAuditSink>())
                .Handle(CreateCmd(st: ArtifactType.Topic, sid: topicId, rel: RelationshipType.Addresses,
                    tt: ArtifactType.Risk, tk: "RSK-2026-012", ttitle: "Key rotation"), default);
        await using (var db = Db(name, User(), Clock(Now)))
            await new DeactivateRelationshipHandler(db, User(), Clock(Now), Substitute.For<IAuditSink>())
                .Handle(new DeactivateRelationshipCommand(id), default);

        await using var db2 = Db(name, User(), Clock(Now));
        var panel = await new GetArtifactRelationshipsHandler(db2)
            .Handle(new GetArtifactRelationshipsQuery(ArtifactType.Topic, topicId), default);
        panel.Outgoing.Should().BeEmpty();
    }

    // ---- Domain guards -----------------------------------------------------------------------------------

    [Fact]
    public void Domain_create_rejects_empty_endpoints()
    {
        var act = () => Relationship.Create(ArtifactType.Topic, Guid.Empty, "TOP-1", "t",
            ArtifactType.Decision, Guid.NewGuid(), "DECN-1", "d", RelationshipType.DecidedBy, null);
        act.Should().Throw<InvalidOperationException>();
    }

    // ---- ITraceabilityLinks: the curated AC-029 downstream predicate -------------------------------------

    // The decision under test is the "focus"; each row seeds ONE edge and asserts whether it satisfies the gate.
    // Downstream (true): decision is SOURCE of recorded-as/resolves, or TARGET of implements. Everything else —
    // upstream (decided-by), lineage (derived-from/supersedes), or another decision's edge — is false.
    public static IEnumerable<object[]> DownstreamCases() => new[]
    {
        new object[] { ArtifactType.Decision, RelationshipType.RecordedAs, ArtifactType.Adr, true, true },   // D --recorded-as--> ADR
        new object[] { ArtifactType.Decision, RelationshipType.Resolves, ArtifactType.Risk, true, true },    // D --resolves--> Risk
        new object[] { ArtifactType.Action, RelationshipType.Implements, ArtifactType.Decision, false, true },// Action --implements--> D (D is target)
        new object[] { ArtifactType.Topic, RelationshipType.DecidedBy, ArtifactType.Decision, false, false }, // Topic --decided-by--> D (upstream)
        new object[] { ArtifactType.Decision, RelationshipType.DerivedFrom, ArtifactType.Decision, true, false }, // lineage
        new object[] { ArtifactType.Decision, RelationshipType.Supersedes, ArtifactType.Adr, true, false },   // lineage
        new object[] { ArtifactType.Decision, RelationshipType.IllustratedBy, ArtifactType.Diagram, true, false }, // documentation, not follow-through
    };

    [Theory]
    [MemberData(nameof(DownstreamCases))]
    public async Task Downstream_predicate_counts_only_follow_through_edges(
        ArtifactType otherEndIsSourceType, RelationshipType rel, ArtifactType otherType, bool decisionIsSource, bool expected)
    {
        var name = "links-" + Guid.NewGuid();
        var decisionId = Guid.NewGuid();
        await using (var db = Db(name, User(), Clock(Now)))
        {
            var edge = decisionIsSource
                ? Relationship.Create(ArtifactType.Decision, decisionId, "DECN-2026-007", "d", otherType, Guid.NewGuid(), "X-1", "x", rel, null)
                : Relationship.Create(otherEndIsSourceType, Guid.NewGuid(), "X-1", "x", ArtifactType.Decision, decisionId, "DECN-2026-007", "d", rel, null);
            db.Relationships.Add(edge);
            await db.SaveChangesAsync();
        }

        await using var read = Db(name, User(), Clock(Now));
        (await new TraceabilityLinks(read).DecisionHasDownstreamEdgeAsync(decisionId)).Should().Be(expected);
    }

    [Fact] // An inactive downstream edge does not satisfy the gate.
    public async Task Downstream_predicate_ignores_inactive_edges()
    {
        var name = "li-" + Guid.NewGuid();
        var decisionId = Guid.NewGuid();
        await using (var db = Db(name, User(), Clock(Now)))
        {
            var edge = Relationship.Create(ArtifactType.Decision, decisionId, "DECN-2026-007", "d",
                ArtifactType.Adr, Guid.NewGuid(), "ADR-1", "a", RelationshipType.RecordedAs, null);
            edge.Deactivate("kc-sec", Now);
            db.Relationships.Add(edge);
            await db.SaveChangesAsync();
        }
        await using var read = Db(name, User(), Clock(Now));
        (await new TraceabilityLinks(read).DecisionHasDownstreamEdgeAsync(decisionId)).Should().BeFalse();
    }

    [Fact] // No edges at all → gate not satisfied.
    public async Task Downstream_predicate_is_false_when_there_are_no_edges()
    {
        await using var db = NewDb(User(), Clock(Now));
        (await new TraceabilityLinks(db).DecisionHasDownstreamEdgeAsync(Guid.NewGuid())).Should().BeFalse();
    }
}
