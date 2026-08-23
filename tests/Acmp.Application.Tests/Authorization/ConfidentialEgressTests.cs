using Acmp.Application.Tests.Shared;
using Acmp.Modules.Dependencies.Application.Features.GetDependenciesForArtifact;
using Acmp.Modules.Dependencies.Application.Features.GetDependenciesRegister;
using Acmp.Modules.Dependencies.Application.Features.GetDependencyByKey;
using Acmp.Modules.Meetings.Application.Features.GetMeetingDetail;
using Acmp.Modules.Meetings.Domain;
using Acmp.Modules.Meetings.Domain.Enums;
using Acmp.Modules.Meetings.Infrastructure.Persistence;
using Acmp.Modules.Traceability.Application.Features.GetArtifactRelationships;
using Acmp.Modules.Traceability.Domain;
using Acmp.Modules.Traceability.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Contracts.Membership;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using DepDomain = Acmp.Modules.Dependencies.Domain;
using DepEnums = Acmp.Modules.Dependencies.Domain.Enums;
using DepPersistence = Acmp.Modules.Dependencies.Infrastructure.Persistence;
using TraceEnums = Acmp.Modules.Traceability.Domain.Enums;

namespace Acmp.Application.Tests.Authorization;

/// <summary>
/// FR-163 / C-AUTHZ-04 / AC-114 — THE EGRESS HALF: data already COPIED OUT of a topic.
///
/// TopicVisibilityQuery narrows what Topics itself returns, but three other modules froze a topic's
/// key+title into their own schema at create time (ADR-0019), and no predicate over Topics can reach a
/// copy. Those copies are redacted at PROJECTION time — never by mutating the stored snapshot, which
/// INV-005 forbids and which AgendaItem freezes by design.
/// </summary>
/// <remarks>
/// ⚠ THIS SUITE IS THE ONLY PLACE THE EGRESS CONTROL IS VISIBLE. Every other Meetings, Traceability and
/// Dependencies suite wires <c>TopicConfidentialityStub.SeesEverything()</c> so it keeps measuring what it
/// was written to measure — which means they stay green whether the redaction narrows anything or not.
/// Every case below is written to FAIL if its redaction is deleted; the "sees everything" twin next to each
/// one is what proves the case is measuring the CONTROL and not just an empty list.
/// </remarks>
public class ConfidentialEgressTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 19, 9, 0, 0, TimeSpan.Zero);

    // The restricted topic every case hides, and an ordinary one that must never be touched — a control
    // that hid everything would pass a one-topic test just as well as a correct one does.
    private static readonly Guid Secret = Guid.NewGuid();
    private static readonly Guid Ordinary = Guid.NewGuid();
    private static readonly Guid Decision = Guid.NewGuid();
    private static readonly Guid ActionId = Guid.NewGuid();

    private static ICurrentUser User(string sub = "kc-member", string name = "Mo M.")
    {
        var u = Substitute.For<ICurrentUser>();
        u.IsAuthenticated.Returns(true);
        u.UserId.Returns(sub);
        u.DisplayName.Returns(name);
        return u;
    }

    private static IClock Clock()
    {
        var c = Substitute.For<IClock>();
        c.UtcNow.Returns(Now);
        return c;
    }

    // ================= Meetings — the agenda item snapshot is MASKED IN PLACE =========================

    private static async Task<MeetingsDbContext> AgendaWithBothTopicsAsync()
    {
        var db = new MeetingsDbContext(
            new DbContextOptionsBuilder<MeetingsDbContext>()
                .UseInMemoryDatabase("egress-mtg-" + Guid.NewGuid()).Options,
            Clock(), User());

        var meeting = Meeting.Schedule("MTG-2026-001", "August review", Meeting.SingleCommitteeId,
            Guid.NewGuid(), "Chair C.", Now, Now.AddHours(2),
            MeetingType.Regular, MeetingMode.InPerson, "Room 1", null, Now);
        db.Meetings.Add(meeting);

        var agenda = Agenda.Draft("AGD-2026-001", meeting.PublicId);
        agenda.AddItem(Secret, "TOP-2026-042", "Acquire the competitor", urgent: true, 20, null, null);
        agenda.AddItem(Ordinary, "TOP-2026-043", "Upgrade the gateway", urgent: false, 15, null, null);
        db.Agendas.Add(agenda);

        await db.SaveChangesAsync();
        return db;
    }

    private static GetMeetingDetailHandler Detail(MeetingsDbContext db, params Guid[] hidden) =>
        new(db, Substitute.For<ICommitteeDirectory>(), User(), TopicConfidentialityStub.Hiding(hidden));

    [Fact]
    public async Task Agenda_item_for_a_restricted_topic_goes_out_with_no_key_and_no_title()
    {
        await using var db = await AgendaWithBothTopicsAsync();

        var detail = await Detail(db, Secret).Handle(new GetMeetingDetailQuery("MTG-2026-001"), default);

        var masked = detail!.Agenda!.Items.Single(i => i.TopicId == Secret);
        masked.TopicKey.Should().BeEmpty();
        masked.TopicTitle.Should().BeEmpty();
        // Urgent is a topic attribute copied out alongside the title; leaving it set would say "something
        // urgent is hidden here", which is itself a fact about the topic.
        masked.Urgent.Should().BeFalse();
    }

    [Fact]
    public async Task Masking_one_item_leaves_every_other_item_on_the_agenda_untouched()
    {
        await using var db = await AgendaWithBothTopicsAsync();

        var detail = await Detail(db, Secret).Handle(new GetMeetingDetailQuery("MTG-2026-001"), default);

        var open = detail!.Agenda!.Items.Single(i => i.TopicId == Ordinary);
        open.TopicKey.Should().Be("TOP-2026-043");
        open.TopicTitle.Should().Be("Upgrade the gateway");
    }

    [Fact]
    public async Task A_masked_item_keeps_its_slot_its_order_its_timebox_and_a_distinct_TopicId()
    {
        await using var db = await AgendaWithBothTopicsAsync();

        var detail = await Detail(db, Secret, Ordinary)
            .Handle(new GetMeetingDetailQuery("MTG-2026-001"), default);

        var items = detail!.Agenda!.Items;

        // MASKED, NOT REMOVED: dropping the row would give two members different agendas and different
        // totals, and would renumber "item 2 of 2" under one reader and not another.
        items.Should().HaveCount(2);
        items.Select(i => i.Order).Should().BeEquivalentTo(new[] { 1, 2 });
        items.Select(i => i.TimeboxMinutes).Should().BeEquivalentTo(new[] { 20, 15 });
        detail.Agenda.TotalTimeboxMinutes.Should().Be(35);

        // ⚠ TopicId SURVIVES ON PURPOSE. The SPA keys agenda rows by it (MeetingWorkspace.tsx,
        // AgendaBuilder.tsx), so blanking it would collide two masked rows onto one React key and break
        // item selection. It leaks nothing readable: topics are read by KEY, and that path already 404s.
        items.Select(i => i.TopicId).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task A_committee_wide_reader_still_sees_the_restricted_agenda_item_in_full()
    {
        await using var db = await AgendaWithBothTopicsAsync();

        // Confidentiality only ever NARROWS (AC-114). This is also the twin that proves the three cases
        // above measure the redaction rather than an agenda that was empty all along.
        var detail = await Detail(db).Handle(new GetMeetingDetailQuery("MTG-2026-001"), default);

        var item = detail!.Agenda!.Items.Single(i => i.TopicId == Secret);
        item.TopicKey.Should().Be("TOP-2026-042");
        item.TopicTitle.Should().Be("Acquire the competitor");
        item.Urgent.Should().BeTrue();
    }

    // ============ Traceability — the edge is DROPPED, because an edge IS a pointer ====================

    private static async Task<TraceabilityDbContext> TwoEdgesAsync()
    {
        var db = new TraceabilityDbContext(
            new DbContextOptionsBuilder<TraceabilityDbContext>()
                .UseInMemoryDatabase("egress-trc-" + Guid.NewGuid()).Options,
            Clock(), User());

        // Both edges hang off the SAME decision, so that decision's panel is where one must vanish and
        // the other must survive.
        db.Relationships.Add(Relationship.Create(
            TraceEnums.ArtifactType.Topic, Secret, "TOP-2026-042", "Acquire the competitor",
            TraceEnums.ArtifactType.Decision, Decision, "DECN-2026-007", "Approve the plan",
            TraceEnums.RelationshipType.DecidedBy, null));
        db.Relationships.Add(Relationship.Create(
            TraceEnums.ArtifactType.Topic, Ordinary, "TOP-2026-043", "Upgrade the gateway",
            TraceEnums.ArtifactType.Decision, Decision, "DECN-2026-007", "Approve the plan",
            TraceEnums.RelationshipType.DecidedBy, null));

        await db.SaveChangesAsync();
        return db;
    }

    private static GetArtifactRelationshipsHandler Panel(TraceabilityDbContext db, params Guid[] hidden) =>
        new(db, TopicConfidentialityStub.Hiding(hidden));

    [Fact]
    public async Task An_edge_naming_a_restricted_topic_disappears_from_another_artifacts_panel()
    {
        await using var db = await TwoEdgesAsync();

        var panel = await Panel(db, Secret)
            .Handle(new GetArtifactRelationshipsQuery(TraceEnums.ArtifactType.Decision, Decision), default);

        panel.Incoming.Should().ContainSingle().Which.OtherTitle.Should().Be("Upgrade the gateway");
        panel.Incoming.Should().NotContain(e => e.OtherId == Secret);
    }

    [Fact]
    public async Task The_same_panel_shows_both_edges_when_nothing_is_hidden()
    {
        await using var db = await TwoEdgesAsync();

        var panel = await Panel(db)
            .Handle(new GetArtifactRelationshipsQuery(TraceEnums.ArtifactType.Decision, Decision), default);

        panel.Incoming.Should().HaveCount(2);
    }

    [Fact]
    public async Task A_restricted_topics_own_panel_is_identical_to_a_topic_that_does_not_exist()
    {
        await using var db = await TwoEdgesAsync();
        var handler = Panel(db, Secret);

        // ⚠ THE POINT OF FILTERING BOTH ENDPOINTS. Because the predicate also drops an edge whose SOURCE
        // is hidden, asking for the restricted topic's own panel needs no separate focus guard — and the
        // answer is indistinguishable from a topic that was never created, so the panel is not an
        // existence oracle for a key somebody was sent.
        var restricted = await handler.Handle(
            new GetArtifactRelationshipsQuery(TraceEnums.ArtifactType.Topic, Secret), default);
        var nonexistent = await handler.Handle(
            new GetArtifactRelationshipsQuery(TraceEnums.ArtifactType.Topic, Guid.NewGuid()), default);

        restricted.Should().BeEquivalentTo(nonexistent);
        restricted.Outgoing.Should().BeEmpty();
        restricted.Incoming.Should().BeEmpty();
    }

    // ============ Dependencies — the surface AC-114 never enumerated (DEF-090) ========================

    private static async Task<DepPersistence.DependenciesDbContext> TwoDependenciesAsync()
    {
        var db = new DepPersistence.DependenciesDbContext(
            new DbContextOptionsBuilder<DepPersistence.DependenciesDbContext>()
                .UseInMemoryDatabase("egress-dep-" + Guid.NewGuid()).Options,
            Clock(), User());

        db.Dependencies.Add(DepDomain.Dependency.Create("DPN-2026-001",
            DepEnums.DependencyEndpointType.Topic, Secret, "TOP-2026-042", "Acquire the competitor",
            DepEnums.DependencyEndpointType.Action, ActionId, "ACT-2026-009", "Rotate keys",
            DepEnums.DependencyKind.BlockedBy, null));
        db.Dependencies.Add(DepDomain.Dependency.Create("DPN-2026-002",
            DepEnums.DependencyEndpointType.Topic, Ordinary, "TOP-2026-043", "Upgrade the gateway",
            DepEnums.DependencyEndpointType.Action, ActionId, "ACT-2026-009", "Rotate keys",
            DepEnums.DependencyKind.BlockedBy, null));

        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task The_dependency_register_drops_the_row_AND_narrows_the_total()
    {
        await using var db = await TwoDependenciesAsync();

        var hiddenPage = await new GetDependenciesRegisterHandler(db, TopicConfidentialityStub.Hiding(Secret))
            .Handle(new GetDependenciesRegisterQuery(), default);
        var fullPage = await new GetDependenciesRegisterHandler(db, TopicConfidentialityStub.SeesEverything())
            .Handle(new GetDependenciesRegisterQuery(), default);

        fullPage.Total.Should().Be(2);
        hiddenPage.Items.Should().ContainSingle().Which.Key.Should().Be("DPN-2026-002");

        // ⚠ THE TOTAL IS THE HALF THAT IS EASY TO MISS, and AC-114 names it: "absent from their totals".
        // A register that hides the row but keeps counting it reports a number nobody can reconcile — and
        // confirms something is there, which is exactly the fact the classification protects.
        hiddenPage.Total.Should().Be(1);
    }

    [Fact]
    public async Task A_dependency_touching_a_restricted_topic_is_refused_by_key_as_not_found()
    {
        await using var db = await TwoDependenciesAsync();
        var handler = new GetDependencyByKeyHandler(db, TopicConfidentialityStub.Hiding(Secret));

        // Null → 404, never 403: the by-key route is the IDOR path, and a 403 would confirm the edge —
        // and so the restricted topic — exists. Identical to a key that was never allocated.
        (await handler.Handle(new GetDependencyByKeyQuery("DPN-2026-001"), default)).Should().BeNull();
        (await handler.Handle(new GetDependencyByKeyQuery("DPN-2026-404"), default)).Should().BeNull();

        // The twin: the very same key resolves for a reader who may see the topic, so the refusal above
        // is the control working and not a broken fixture.
        (await new GetDependencyByKeyHandler(db, TopicConfidentialityStub.SeesEverything())
                .Handle(new GetDependencyByKeyQuery("DPN-2026-001"), default))!
            .FromTitle.Should().Be("Acquire the competitor");
    }

    [Fact]
    public async Task The_dependency_panel_hides_the_edge_from_the_far_end_and_from_the_topics_own_panel()
    {
        await using var db = await TwoDependenciesAsync();
        var handler = new GetDependenciesForArtifactHandler(db, TopicConfidentialityStub.Hiding(Secret));

        // Seen from the shared Action: one edge survives, the restricted one is gone.
        var fromAction = await handler.Handle(
            new GetDependenciesForArtifactQuery(DepEnums.DependencyEndpointType.Action, ActionId), default);
        fromAction.Inbound.Should().ContainSingle().Which.OtherKey.Should().Be("TOP-2026-043");

        // Seen from the restricted topic itself: nothing, and identical to a topic that does not exist.
        var fromSecret = await handler.Handle(
            new GetDependenciesForArtifactQuery(DepEnums.DependencyEndpointType.Topic, Secret), default);
        var fromNowhere = await handler.Handle(
            new GetDependenciesForArtifactQuery(DepEnums.DependencyEndpointType.Topic, Guid.NewGuid()), default);
        fromSecret.Should().BeEquivalentTo(fromNowhere);
    }

    [Fact]
    public async Task The_impact_graph_inherits_the_redaction_through_the_panel_read_it_composes()
    {
        await using var db = await TwoDependenciesAsync();

        // GetImpactGraph composes IDependencyArtifactReader, whose implementation sends exactly this
        // query — so the graph inherits the redaction instead of repeating it. Asserted through the
        // handler the reader sends to, because that IS the seam.
        var edges = await new GetDependenciesForArtifactHandler(db, TopicConfidentialityStub.Hiding(Secret))
            .Handle(new GetDependenciesForArtifactQuery(DepEnums.DependencyEndpointType.Action, ActionId), default);

        edges.Inbound.Should().NotContain(e => e.OtherId == Secret);
        edges.Inbound.Concat(edges.Outbound).Should().NotContain(e => e.OtherTitle.Contains("competitor"));
    }
}
