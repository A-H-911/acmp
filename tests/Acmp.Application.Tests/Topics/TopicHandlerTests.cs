using Acmp.Modules.Topics.Application.Abstractions;
using Acmp.Modules.Topics.Application.Features.DeferTopic;
using Acmp.Modules.Topics.Application.Features.GetBacklog;
using Acmp.Modules.Topics.Application.Features.GetTopicDetail;
using Acmp.Modules.Topics.Application.Features.MoveTopicPriority;
using Acmp.Modules.Topics.Application.Features.PrepareTopic;
using Acmp.Modules.Topics.Application.Features.PrioritizeTopic;
using Acmp.Modules.Topics.Application.Features.RejectTopic;
using Acmp.Modules.Topics.Application.Features.SubmitTopic;
using Acmp.Modules.Topics.Application.Features.SweepTopicSla;
using Acmp.Modules.Topics.Application.Features.UpdateTopic;
using Acmp.Modules.Topics.Domain;
using Acmp.Modules.Topics.Domain.Enums;
using Acmp.Modules.Topics.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Application.Exceptions;
using Acmp.Shared.Authorization;
using Acmp.Shared.Contracts.Membership;
using Acmp.Shared.Contracts.Notifications;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Acmp.Application.Tests.Topics;

// Round-trip through the real TopicsDbContext (InMemory) — validates the EF mapping for the JSON string
// collections, the owned child tables, and the key counter, plus the submit → backlog → detail flow.
public class TopicHandlerTests
{
    // FR-163: these suites assert BACKLOG filtering, sorting and paging — not confidentiality, which
    // has its own suites. A permissive scope keeps them measuring what they claim to; a restrictive one
    // would silently change what every case here is testing.
    private static ITopicVisibility SeesEverything()
    {
        var v = Substitute.For<ITopicVisibility>();
        v.ResolveAsync(Arg.Any<CancellationToken>())
            .Returns(new TopicVisibilityScope(true, Array.Empty<Guid>()));
        return v;
    }

    // The context's clock stamps CreatedAt/UpdatedAt — share it with the handler so aging is deterministic.
    private static TopicsDbContext NewDb(ICurrentUser user, IClock clock) =>
        new(new DbContextOptionsBuilder<TopicsDbContext>().UseInMemoryDatabase("topics-" + Guid.NewGuid()).Options,
            clock, user);

    private static ICurrentUser User(string sub, string name)
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

    private static SubmitTopicCommand Command() => new(
        "Adopt Keycloak", "Consolidate IAM onto Keycloak.", "Fragmented auth is risky.",
        TopicType.ArchitectureDecision, TopicUrgency.Urgent, TopicSource.CommitteeMember,
        new[] { "identity", "platform" }, new[] { "API Gateway" }, new[] { "SecurityArch" });

    [Fact]
    public async Task Submit_persists_topic_with_generated_key_streams_and_history()
    {
        var now = new DateTimeOffset(2026, 2, 15, 9, 0, 0, TimeSpan.Zero);
        var user = User("kc-omar", "Omar H.");
        var clock = Clock(now);
        await using var db = NewDb(user, clock);
        var audit = Substitute.For<IAuditSink>();

        var result = await new SubmitTopicHandler(db, new TopicKeyGenerator(db), user, clock, audit)
            .Handle(Command(), CancellationToken.None);

        result.Key.Should().Be("TOP-2026-001");

        var stored = await db.Topics.Include(t => t.History).SingleAsync();
        stored.Status.Should().Be(TopicStatus.Submitted);
        stored.Scope.Should().Be(TopicScope.MultiStream);            // derived from 2 streams
        stored.AffectedStreams.Should().BeEquivalentTo("identity", "platform");  // JSON collection round-trips
        stored.SubmittedByName.Should().Be("Omar H.");
        stored.History.Should().ContainSingle(h => h.ToStatus == TopicStatus.Submitted);  // owned child table
        await audit.Received(1).EmitEnrichedAsync("Topics.TopicSubmitted", "Topic", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Backlog_and_detail_read_back_the_submitted_topic()
    {
        var now = new DateTimeOffset(2026, 2, 15, 9, 0, 0, TimeSpan.Zero);
        var user = User("kc-omar", "Omar H.");
        var clock = Clock(now);
        await using var db = NewDb(user, clock);

        var result = await new SubmitTopicHandler(db, new TopicKeyGenerator(db), user, clock, Substitute.For<IAuditSink>())
            .Handle(Command(), CancellationToken.None);

        // SLA aging: Urgent (7-day threshold) submitted 9 days ago → breaching (AC-057).
        var later = now.AddDays(9);
        var backlog = await new GetBacklogHandler(db, Clock(later), SeesEverything()).Handle(new GetBacklogQuery(), CancellationToken.None);
        backlog.Total.Should().Be(1);
        backlog.Items[0].Key.Should().Be(result.Key);
        backlog.Items[0].SlaBreached.Should().BeTrue();
        backlog.Items[0].AgeDays.Should().Be(9);

        var detail = await new GetTopicDetailHandler(db, Clock(later), SeesEverything(), Substitute.For<IAnomalyDetector>()).Handle(new GetTopicDetailQuery(result.Key), CancellationToken.None);
        detail.Should().NotBeNull();
        detail!.Streams.Should().BeEquivalentTo("identity", "platform");
        detail.Tags.Should().BeEquivalentTo("SecurityArch");
        detail.History.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Key_generator_increments_per_year()
    {
        var user = User("kc-x", "X");
        await using var db = NewDb(user, Clock(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)));
        var gen = new TopicKeyGenerator(db);

        (await gen.NextAsync(2026)).Should().Be("TOP-2026-001");
        (await gen.NextAsync(2026)).Should().Be("TOP-2026-002");
        (await gen.NextAsync(2027)).Should().Be("TOP-2027-001");
    }

    // ───────────────────────── S1 adversarial: triage/edit handlers (ADR-0016) ─────────────────────────
    // These handlers authorize per-resource via IResourceAuthorizer (Topic loads, then EnsureAsync) — so
    // authz-denial IS assertable here (unlike the Meetings handlers, whose role-gate is the MediatR
    // pipeline). Failure-first: 404 · authz-deny · domain status/immutability guard · audit-on-change.

    // Denies every EnsureAsync (→ ForbiddenAccessException) when deny:true; otherwise authorizes (default).
    private static IResourceAuthorizer Authz(bool deny = false)
    {
        var a = Substitute.For<IResourceAuthorizer>();
        if (deny)
            a.EnsureAsync(Arg.Any<object>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromException(new ForbiddenAccessException("Forbidden.")));
        return a;
    }

    // The Secretary roster for the prepare fan-out. Empty by default (no recipients); pass subs to
    // exercise skip-self. Name mirrors the sub — display name is irrelevant to the notification target.
    private static ICommitteeDirectory Directory(params string[] secretarySubs)
    {
        var d = Substitute.For<ICommitteeDirectory>();
        d.GetActiveMembersInRoleAsync(AcmpRoles.Secretary, Arg.Any<CancellationToken>())
            .Returns(secretarySubs.Select(s => new CommitteeRecipient(s, s)).ToArray());
        return d;
    }

    // Builds a topic walked to the requested status via the real domain transitions, then persists it.
    private static async Task<Topic> SeedTopicAsync(TopicsDbContext db, TopicStatus target, string submitterSub = "kc-omar")
    {
        var t0 = new DateTimeOffset(2026, 2, 1, 9, 0, 0, TimeSpan.Zero);
        var t = Topic.Draft("TOP-2026-100", "Title", "Desc", "Justification", TopicType.ArchitectureDecision,
            TopicUrgency.Normal, TopicSource.CommitteeMember, submitterSub, "Omar H.",
            new[] { "platform" }, Array.Empty<string>(), Array.Empty<string>());

        if (target != TopicStatus.Draft)
        {
            t.Submit(t0);
            if (target != TopicStatus.Submitted)
            {
                t.BeginTriage("kc-sec", "Sec", t0);
                if (target != TopicStatus.Triage)
                {
                    t.Accept(Guid.NewGuid(), "Owner", "kc-sec", "Sec", t0);
                    if (target == TopicStatus.Prepared || target == TopicStatus.Decided)
                        t.MarkPrepared("kc-sec", "Sec", t0);
                    if (target == TopicStatus.Decided)
                    {
                        t.Schedule(Guid.NewGuid(), "kc-sec", "Sec", t0);
                        t.EnterCommittee("kc-sec", "Sec", t0);
                        t.Decide("kc-sec", "Sec", t0);
                    }
                }
            }
        }

        t.Status.Should().Be(target);   // guard the helper itself
        db.Topics.Add(t);
        await db.SaveChangesAsync();
        return t;
    }

    // ---- UpdateTopicHandler (AC-034) ----

    [Fact]
    public async Task Update_throws_not_found_for_an_unknown_topic()
    {
        var user = User("kc-omar", "Omar H.");
        await using var db = NewDb(user, Clock(default));

        var act = () => new UpdateTopicHandler(db, Authz(), user, Substitute.For<IAuditSink>())
            .Handle(new UpdateTopicCommand(Guid.NewGuid(), "T", "D", "J", TopicUrgency.Urgent,
                new[] { "platform" }, Array.Empty<string>(), Array.Empty<string>()), default);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Update_preAccept_by_the_submitter_edits_content_without_extra_authz_and_audits()
    {
        var user = User("kc-omar", "Omar H.");          // the submitter
        await using var db = NewDb(user, Clock(default));
        var topic = await SeedTopicAsync(db, TopicStatus.Submitted, submitterSub: "kc-omar");
        var authz = Authz();
        var audit = Substitute.For<IAuditSink>();

        await new UpdateTopicHandler(db, authz, user, audit).Handle(
            new UpdateTopicCommand(topic.PublicId, "New Title", "New Desc", "New Just", TopicUrgency.Critical,
                new[] { "identity" }, Array.Empty<string>(), new[] { "tag" }), default);

        var stored = await db.Topics.SingleAsync();
        stored.Title.Should().Be("New Title");                       // content edited pre-Accept
        stored.Urgency.Should().Be(TopicUrgency.Critical);
        await authz.DidNotReceive().EnsureAsync(Arg.Any<object>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await audit.Received(1).EmitEnrichedAsync("Topics.TopicUpdated", "Topic", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_preAccept_by_a_non_submitter_requires_TopicEdit_and_is_denied()
    {
        var user = User("kc-other", "Someone Else");     // not the submitter
        await using var db = NewDb(user, Clock(default));
        var topic = await SeedTopicAsync(db, TopicStatus.Submitted, submitterSub: "kc-omar");
        var authz = Authz(deny: true);
        var audit = Substitute.For<IAuditSink>();

        var act = () => new UpdateTopicHandler(db, authz, user, audit).Handle(
            new UpdateTopicCommand(topic.PublicId, "Hijack", "D", "J", TopicUrgency.Normal,
                new[] { "platform" }, Array.Empty<string>(), Array.Empty<string>()), default);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
        await authz.Received(1).EnsureAsync(Arg.Any<object>(), Policies.TopicEdit, Arg.Any<CancellationToken>());
        (await db.Topics.SingleAsync()).Title.Should().Be("Title");  // unchanged
        await audit.DidNotReceive().EmitEnrichedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_postAccept_edits_metadata_only_under_TopicTriage_and_keeps_content_locked()
    {
        var user = User("kc-sec", "Sec");
        await using var db = NewDb(user, Clock(default));
        var topic = await SeedTopicAsync(db, TopicStatus.Accepted);
        var authz = Authz();

        await new UpdateTopicHandler(db, authz, user, Substitute.For<IAuditSink>()).Handle(
            new UpdateTopicCommand(topic.PublicId, "Tampered", "Tampered", "Tampered", TopicUrgency.Critical,
                new[] { "identity" }, new[] { "sys" }, Array.Empty<string>()), default);

        var stored = await db.Topics.SingleAsync();
        stored.Title.Should().Be("Title");                           // content locked post-Accept (AC-034)
        stored.Urgency.Should().Be(TopicUrgency.Critical);           // metadata still editable
        stored.AffectedStreams.Should().BeEquivalentTo("identity");
        await authz.Received(1).EnsureAsync(Arg.Any<object>(), Policies.TopicTriage, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_postAccept_is_denied_without_TopicTriage()
    {
        var user = User("kc-member", "Member");
        await using var db = NewDb(user, Clock(default));
        var topic = await SeedTopicAsync(db, TopicStatus.Accepted);

        var act = () => new UpdateTopicHandler(db, Authz(deny: true), user, Substitute.For<IAuditSink>()).Handle(
            new UpdateTopicCommand(topic.PublicId, "T", "D", "J", TopicUrgency.Normal,
                new[] { "platform" }, Array.Empty<string>(), Array.Empty<string>()), default);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    // DEF-058: the caller that makes Platform/OrgWide reachable at all. Before this, DeriveScope was
    // the only writer and could produce nothing but SingleStream/MultiStream.
    [Fact]
    public async Task Update_elevates_the_scope_under_TopicTriage_and_audits_it_separately()
    {
        var user = User("kc-sec", "Sec");
        await using var db = NewDb(user, Clock(default));
        var topic = await SeedTopicAsync(db, TopicStatus.Triage, submitterSub: "kc-omar");
        var authz = Authz();
        var audit = Substitute.For<IAuditSink>();

        await new UpdateTopicHandler(db, authz, user, audit).Handle(
            new UpdateTopicCommand(topic.PublicId, "T", "D", "J", TopicUrgency.Normal,
                new[] { "identity" }, Array.Empty<string>(), Array.Empty<string>(), TopicScope.OrgWide), default);

        var stored = await db.Topics.SingleAsync();
        stored.Scope.Should().Be(TopicScope.OrgWide);
        stored.AffectsAllStreams.Should().BeTrue("clause (5) makes an OrgWide topic actionable by any stream-bounded member");
        await authz.Received(1).EnsureAsync(Arg.Any<object>(), Policies.TopicTriage, Arg.Any<CancellationToken>());
        // A widening of write access is findable on its own verb, not buried inside "topic updated".
        await audit.Received(1).EmitEnrichedAsync("Topics.TopicScopeChanged", "Topic", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ⚠ THE ESCALATION THIS GATE EXISTS FOR. Pre-Accept, a submitter editing their OWN topic passes
    // no ABAC check at all — so without a separate gate on scope, the submitter of any topic could
    // elevate it to OrgWide and hand every stream-bounded member write access to it.
    [Fact]
    public async Task A_submitter_editing_their_own_topic_still_cannot_elevate_its_scope()
    {
        var user = User("kc-omar", "Omar H.");           // IS the submitter
        await using var db = NewDb(user, Clock(default));
        var topic = await SeedTopicAsync(db, TopicStatus.Submitted, submitterSub: "kc-omar");
        var authz = Authz(deny: true);

        var act = () => new UpdateTopicHandler(db, authz, user, Substitute.For<IAuditSink>()).Handle(
            new UpdateTopicCommand(topic.PublicId, "T", "D", "J", TopicUrgency.Normal,
                new[] { "identity" }, Array.Empty<string>(), Array.Empty<string>(), TopicScope.Platform), default);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
        await authz.Received(1).EnsureAsync(Arg.Any<object>(), Policies.TopicTriage, Arg.Any<CancellationToken>());
        (await db.Topics.SingleAsync()).Scope.Should().NotBe(TopicScope.Platform);
    }

    // Omitting Scope means "leave it alone", which is what lets an existing caller keep working
    // without silently resetting an elevated topic — and it must not cost an authorization check.
    [Fact]
    public async Task Update_without_a_scope_leaves_it_untouched_and_never_asks_for_triage()
    {
        var user = User("kc-omar", "Omar H.");
        await using var db = NewDb(user, Clock(default));
        var topic = await SeedTopicAsync(db, TopicStatus.Submitted, submitterSub: "kc-omar");
        topic.SetScope(TopicScope.Platform);
        await db.SaveChangesAsync();
        var authz = Authz();

        await new UpdateTopicHandler(db, authz, user, Substitute.For<IAuditSink>()).Handle(
            new UpdateTopicCommand(topic.PublicId, "T", "D", "J", TopicUrgency.Normal,
                new[] { "identity" }, Array.Empty<string>(), Array.Empty<string>()), default);

        (await db.Topics.SingleAsync()).Scope.Should().Be(TopicScope.Platform);
        await authz.DidNotReceive().EnsureAsync(Arg.Any<object>(), Policies.TopicTriage, Arg.Any<CancellationToken>());
    }

    // ---- DeferTopicHandler ----

    [Fact]
    public async Task Defer_throws_not_found_for_an_unknown_topic()
    {
        var user = User("kc-sec", "Sec");
        await using var db = NewDb(user, Clock(default));

        var act = () => new DeferTopicHandler(db, Authz(), user, Clock(default), Substitute.For<IAuditSink>())
            .Handle(new DeferTopicCommand(Guid.NewGuid(), "later", null), default);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Defer_is_denied_without_TopicTriage()
    {
        var user = User("kc-member", "Member");
        await using var db = NewDb(user, Clock(default));
        var topic = await SeedTopicAsync(db, TopicStatus.Triage);

        var act = () => new DeferTopicHandler(db, Authz(deny: true), user, Clock(default), Substitute.For<IAuditSink>())
            .Handle(new DeferTopicCommand(topic.PublicId, "Awaiting budget", null), default);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
        (await db.Topics.SingleAsync()).Status.Should().Be(TopicStatus.Triage);  // not advanced
    }

    [Fact]
    public async Task Defer_from_a_disallowed_status_trips_the_domain_guard()
    {
        var user = User("kc-sec", "Sec");
        await using var db = NewDb(user, Clock(default));
        var topic = await SeedTopicAsync(db, TopicStatus.Submitted);   // Defer allows Triage/Accepted/Scheduled/InCommittee

        var act = () => new DeferTopicHandler(db, Authz(), user, Clock(default), Substitute.For<IAuditSink>())
            .Handle(new DeferTopicCommand(topic.PublicId, "reason", null), default);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Defer_succeeds_records_revisit_date_and_audits()
    {
        var user = User("kc-sec", "Sec");
        var revisit = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
        await using var db = NewDb(user, Clock(default));
        var topic = await SeedTopicAsync(db, TopicStatus.Triage);
        var audit = Substitute.For<IAuditSink>();

        await new DeferTopicHandler(db, Authz(), user, Clock(default), audit)
            .Handle(new DeferTopicCommand(topic.PublicId, "Awaiting budget", revisit), default);

        var stored = await db.Topics.SingleAsync();
        stored.Status.Should().Be(TopicStatus.Deferred);
        stored.RevisitOn.Should().Be(revisit);
        await audit.Received(1).EmitEnrichedAsync("Topics.TopicDeferred", "Topic", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ---- PrepareTopicHandler (AC-035) ----

    [Fact]
    public async Task Prepare_throws_not_found_for_an_unknown_topic()
    {
        var user = User("kc-sec", "Sec");
        await using var db = NewDb(user, Clock(default));

        var act = () => new PrepareTopicHandler(db, Authz(), user, Clock(default), Substitute.For<IAuditSink>(), Directory(), Substitute.For<INotificationChannel>())
            .Handle(new PrepareTopicCommand(Guid.NewGuid()), default);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Prepare_is_denied_without_TopicEdit()
    {
        var user = User("kc-guest", "Guest");
        await using var db = NewDb(user, Clock(default));
        var topic = await SeedTopicAsync(db, TopicStatus.Accepted);

        var act = () => new PrepareTopicHandler(db, Authz(deny: true), user, Clock(default), Substitute.For<IAuditSink>(), Directory(), Substitute.For<INotificationChannel>())
            .Handle(new PrepareTopicCommand(topic.PublicId), default);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task Prepare_from_a_non_accepted_status_trips_the_domain_guard()
    {
        var user = User("kc-sec", "Sec");
        await using var db = NewDb(user, Clock(default));
        var topic = await SeedTopicAsync(db, TopicStatus.Submitted);   // MarkPrepared requires Accepted

        var act = () => new PrepareTopicHandler(db, Authz(), user, Clock(default), Substitute.For<IAuditSink>(), Directory(), Substitute.For<INotificationChannel>())
            .Handle(new PrepareTopicCommand(topic.PublicId), default);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Prepare_marks_the_topic_prepared_and_audits()
    {
        var user = User("kc-sec", "Sec");
        await using var db = NewDb(user, Clock(default));
        var topic = await SeedTopicAsync(db, TopicStatus.Accepted);
        var audit = Substitute.For<IAuditSink>();

        await new PrepareTopicHandler(db, Authz(), user, Clock(default), audit, Directory(), Substitute.For<INotificationChannel>())
            .Handle(new PrepareTopicCommand(topic.PublicId), default);

        (await db.Topics.SingleAsync()).Status.Should().Be(TopicStatus.Prepared);
        await audit.Received(1).EmitEnrichedAsync("Topics.TopicPrepared", "Topic", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact] // W4: the Secretary roster is notified on prepare, except the actor if they are a Secretary
    public async Task Prepare_notifies_each_secretary_except_the_actor()
    {
        var user = User("kc-sec", "Sec");                       // the actor is themselves a Secretary
        await using var db = NewDb(user, Clock(default));
        var topic = await SeedTopicAsync(db, TopicStatus.Accepted);
        var directory = Directory("kc-sec", "kc-sec2");         // two Secretaries, incl. the actor
        var notifications = Substitute.For<INotificationChannel>();

        await new PrepareTopicHandler(db, Authz(), user, Clock(default), Substitute.For<IAuditSink>(), directory, notifications)
            .Handle(new PrepareTopicCommand(topic.PublicId), default);

        await notifications.Received(1).PublishAsync(
            Arg.Is<NotificationMessage>(m => m.RecipientUserId == "kc-sec2" && m.Category == "TopicPrepared" && m.DeepLink == "/topics/" + topic.Key),
            Arg.Any<CancellationToken>());
        await notifications.DidNotReceive().PublishAsync(
            Arg.Is<NotificationMessage>(m => m.RecipientUserId == "kc-sec"), Arg.Any<CancellationToken>());
    }

    // ---- PrioritizeTopicHandler (AC-043) ----

    [Fact]
    public async Task Prioritize_throws_not_found_for_an_unknown_topic()
    {
        var user = User("kc-sec", "Sec");
        await using var db = NewDb(user, Clock(default));

        var act = () => new PrioritizeTopicHandler(db, Authz(), user, Clock(default), Substitute.For<IAuditSink>())
            .Handle(new PrioritizeTopicCommand(Guid.NewGuid(), 3), default);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Prioritize_is_denied_without_BacklogPrioritize()
    {
        var user = User("kc-member", "Member");
        await using var db = NewDb(user, Clock(default));
        var topic = await SeedTopicAsync(db, TopicStatus.Accepted);

        var act = () => new PrioritizeTopicHandler(db, Authz(deny: true), user, Clock(default), Substitute.For<IAuditSink>())
            .Handle(new PrioritizeTopicCommand(topic.PublicId, 5), default);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
        (await db.Topics.SingleAsync()).Priority.Should().Be(0);   // not reprioritized
    }

    [Fact]
    public async Task Prioritize_an_immutable_decided_topic_trips_the_immutability_guard()
    {
        var user = User("kc-sec", "Sec");
        await using var db = NewDb(user, Clock(default));
        var topic = await SeedTopicAsync(db, TopicStatus.Decided);     // EnsureMutable throws on Decided

        var act = () => new PrioritizeTopicHandler(db, Authz(), user, Clock(default), Substitute.For<IAuditSink>())
            .Handle(new PrioritizeTopicCommand(topic.PublicId, 2), default);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*immutable*");
    }

    [Fact]
    public async Task Prioritize_sets_the_ordinal_and_audits()
    {
        var user = User("kc-sec", "Sec");
        await using var db = NewDb(user, Clock(default));
        var topic = await SeedTopicAsync(db, TopicStatus.Accepted);
        var audit = Substitute.For<IAuditSink>();

        await new PrioritizeTopicHandler(db, Authz(), user, Clock(default), audit)
            .Handle(new PrioritizeTopicCommand(topic.PublicId, 7), default);

        (await db.Topics.SingleAsync()).Priority.Should().Be(7);
        await audit.Received(1).EmitEnrichedAsync("Topics.TopicPrioritized", "Topic", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ---- RejectTopicHandler (AC-031/032/033) ----

    [Fact]
    public async Task Reject_throws_not_found_for_an_unknown_topic()
    {
        var user = User("kc-sec", "Sec");
        await using var db = NewDb(user, Clock(default));

        var act = () => new RejectTopicHandler(db, Authz(), user, Clock(default), Substitute.For<IAuditSink>(), Substitute.For<INotificationChannel>())
            .Handle(new RejectTopicCommand(Guid.NewGuid(), "Duplicate"), default);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Reject_is_denied_without_TopicTriage()
    {
        var user = User("kc-member", "Member");
        await using var db = NewDb(user, Clock(default));
        var topic = await SeedTopicAsync(db, TopicStatus.Submitted);

        var act = () => new RejectTopicHandler(db, Authz(deny: true), user, Clock(default), Substitute.For<IAuditSink>(), Substitute.For<INotificationChannel>())
            .Handle(new RejectTopicCommand(topic.PublicId, "Duplicate"), default);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
        (await db.Topics.SingleAsync()).Status.Should().Be(TopicStatus.Submitted);  // not rejected
    }

    [Fact]
    public async Task Reject_from_a_disallowed_status_trips_the_domain_guard()
    {
        var user = User("kc-sec", "Sec");
        await using var db = NewDb(user, Clock(default));
        var topic = await SeedTopicAsync(db, TopicStatus.Accepted);    // Reject allows Submitted/Triage only

        var act = () => new RejectTopicHandler(db, Authz(), user, Clock(default), Substitute.For<IAuditSink>(), Substitute.For<INotificationChannel>())
            .Handle(new RejectTopicCommand(topic.PublicId, "Too late"), default);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Reject_records_the_rationale_as_immutable_history_and_audits()
    {
        var user = User("kc-sec", "Sec");
        await using var db = NewDb(user, Clock(default));
        var topic = await SeedTopicAsync(db, TopicStatus.Submitted);   // submitter = kc-omar, actor = kc-sec
        var audit = Substitute.For<IAuditSink>();
        var notifications = Substitute.For<INotificationChannel>();

        await new RejectTopicHandler(db, Authz(), user, Clock(default), audit, notifications)
            .Handle(new RejectTopicCommand(topic.PublicId, "Duplicate of TOP-2026-001"), default);

        var stored = await db.Topics.Include(t => t.History).SingleAsync();
        stored.Status.Should().Be(TopicStatus.Rejected);
        stored.History.Should().Contain(h => h.ToStatus == TopicStatus.Rejected && h.Reason == "Duplicate of TOP-2026-001");
        await audit.Received(1).EmitEnrichedAsync("Topics.TopicRejected", "Topic", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        // AC-032: the submitter (not the actor) is notified of the rejection.
        await notifications.Received(1).PublishAsync(
            Arg.Is<NotificationMessage>(m => m.RecipientUserId == "kc-omar" && m.Category == "TopicRejected"
                && m.DeepLink == "/topics/" + topic.Key),
            Arg.Any<CancellationToken>());
    }

    // ---- SweepTopicSlaHandler (AC-057) ----
    // Seed = Normal urgency (21-day SLA), submitted 2026-02-01; a clock 22 days later breaches.

    private static SweepTopicSlaHandler SlaSweep(TopicsDbContext db, DateTimeOffset now, INotificationChannel notifications,
        ICommitteeDirectory directory, IAuditSink audit) =>
        new(db, Clock(now), notifications, directory, audit);

    [Fact]
    public async Task Sla_sweep_notifies_the_secretary_and_marks_a_breaching_topic()
    {
        var now = new DateTimeOffset(2026, 2, 23, 9, 0, 0, TimeSpan.Zero);  // 22d after the seed > 21d Normal SLA
        await using var db = NewDb(User("kc-sec", "Sec"), Clock(now));
        var topic = await SeedTopicAsync(db, TopicStatus.Submitted);
        var notifications = Substitute.For<INotificationChannel>();
        var audit = Substitute.For<IAuditSink>();

        var count = await SlaSweep(db, now, notifications, Directory("kc-sec2"), audit)
            .Handle(new SweepTopicSlaCommand(), default);

        count.Should().Be(1);
        (await db.Topics.SingleAsync()).SlaNotifiedAt.Should().Be(now);
        await notifications.Received(1).PublishAsync(
            Arg.Is<NotificationMessage>(m => m.RecipientUserId == "kc-sec2" && m.Category == "TopicSlaBreach"
                && m.DeepLink == "/topics/" + topic.Key),
            Arg.Any<CancellationToken>());
        await audit.Received(1).EmitAsync("Topics.SlaBreachNotified", "system:topic-sla", Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sla_sweep_ignores_a_topic_within_its_sla()
    {
        var now = new DateTimeOffset(2026, 2, 5, 9, 0, 0, TimeSpan.Zero);   // 4d after the seed < 21d Normal SLA
        await using var db = NewDb(User("kc-sec", "Sec"), Clock(now));
        await SeedTopicAsync(db, TopicStatus.Submitted);
        var notifications = Substitute.For<INotificationChannel>();

        var count = await SlaSweep(db, now, notifications, Directory("kc-sec2"), Substitute.For<IAuditSink>())
            .Handle(new SweepTopicSlaCommand(), default);

        count.Should().Be(0);
        (await db.Topics.SingleAsync()).SlaNotifiedAt.Should().BeNull();
        await notifications.DidNotReceive().PublishAsync(Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sla_sweep_is_a_one_shot_per_breach_window()
    {
        var now = new DateTimeOffset(2026, 2, 23, 9, 0, 0, TimeSpan.Zero);
        await using var db = NewDb(User("kc-sec", "Sec"), Clock(now));
        await SeedTopicAsync(db, TopicStatus.Submitted);
        var notifications = Substitute.For<INotificationChannel>();
        var handler = SlaSweep(db, now, notifications, Directory("kc-sec2"), Substitute.For<IAuditSink>());

        (await handler.Handle(new SweepTopicSlaCommand(), default)).Should().Be(1);
        (await handler.Handle(new SweepTopicSlaCommand(), default)).Should().Be(0);   // marker set → not re-notified

        await notifications.Received(1).PublishAsync(Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sla_sweep_marks_the_topic_even_when_the_secretary_roster_is_empty()
    {
        var now = new DateTimeOffset(2026, 2, 23, 9, 0, 0, TimeSpan.Zero);
        await using var db = NewDb(User("kc-sec", "Sec"), Clock(now));
        await SeedTopicAsync(db, TopicStatus.Submitted);
        var notifications = Substitute.For<INotificationChannel>();

        var count = await SlaSweep(db, now, notifications, Directory(), Substitute.For<IAuditSink>())  // empty roster
            .Handle(new SweepTopicSlaCommand(), default);

        count.Should().Be(1);
        (await db.Topics.SingleAsync()).SlaNotifiedAt.Should().Be(now);   // marker flips so we don't re-scan it
        await notifications.DidNotReceive().PublishAsync(Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact] // AC-057: a status transition re-arms SLA notification for the new time-in-status window.
    public void A_status_transition_clears_the_sla_notified_marker()
    {
        var t0 = new DateTimeOffset(2026, 2, 1, 9, 0, 0, TimeSpan.Zero);
        var topic = Topic.Draft("TOP-2026-101", "T", "D", "J", TopicType.ArchitectureDecision,
            TopicUrgency.Normal, TopicSource.CommitteeMember, "kc-omar", "Omar",
            new[] { "platform" }, Array.Empty<string>(), Array.Empty<string>());
        topic.Submit(t0);
        topic.MarkSlaNotified(t0);
        topic.SlaNotifiedAt.Should().Be(t0);

        topic.BeginTriage("kc-sec", "Sec", t0.AddDays(1));

        topic.SlaNotifiedAt.Should().BeNull();
    }

    // ---- TopicBuckets (AC-043 — server-side mirror of the kanban grouping) ----

    [Theory]
    [InlineData(TopicStatus.Draft, "triage")]
    [InlineData(TopicStatus.Submitted, "triage")]
    [InlineData(TopicStatus.Triage, "triage")]
    [InlineData(TopicStatus.Reopened, "triage")]
    [InlineData(TopicStatus.Accepted, "accepted")]
    [InlineData(TopicStatus.Prepared, "accepted")]
    [InlineData(TopicStatus.Scheduled, "scheduled")]
    [InlineData(TopicStatus.InCommittee, "scheduled")]
    [InlineData(TopicStatus.Deferred, "returned")]
    [InlineData(TopicStatus.Rejected, "returned")]
    [InlineData(TopicStatus.Decided, "done")]
    [InlineData(TopicStatus.Closed, "done")]
    [InlineData(TopicStatus.Converted, "done")]
    [InlineData((TopicStatus)999, "triage")]   // defensive default
    public void TopicBuckets_maps_every_status_to_its_kanban_bucket(TopicStatus status, string bucket) =>
        Acmp.Modules.Topics.Application.Internal.TopicBuckets.BucketOf(status).Should().Be(bucket);

    // ---- MoveTopicPriorityHandler (AC-043) ----

    private static MoveTopicPriorityHandler MovePriority(TopicsDbContext db, IAuditSink? audit = null, bool deny = false) =>
        new(db, Authz(deny), Clock(new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero)), audit ?? Substitute.For<IAuditSink>());

    // A Submitted topic in the 'triage' bucket with a given key; all share Priority 0 and CreatedAt, so their
    // deterministic order is by Key (the third tiebreak).
    private static async Task<Topic> SeedSubmittedAsync(TopicsDbContext db, string key)
    {
        var t = Topic.Draft(key, "T", "D", "J", TopicType.ArchitectureDecision, TopicUrgency.Normal,
            TopicSource.CommitteeMember, "kc-omar", "Omar", new[] { "platform" }, Array.Empty<string>(), Array.Empty<string>());
        t.Submit(new DateTimeOffset(2026, 2, 1, 9, 0, 0, TimeSpan.Zero));
        db.Topics.Add(t);
        await db.SaveChangesAsync();
        return t;
    }

    [Fact]
    public async Task Move_throws_not_found_for_an_unknown_topic()
    {
        await using var db = NewDb(User("kc-sec", "Sec"), Clock(default));
        var act = () => MovePriority(db).Handle(new MoveTopicPriorityCommand(Guid.NewGuid(), -1), default);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Move_is_denied_without_BacklogPrioritize()
    {
        await using var db = NewDb(User("kc-member", "M"), Clock(default));
        var t = await SeedSubmittedAsync(db, "TOP-2026-201");
        var act = () => MovePriority(db, deny: true).Handle(new MoveTopicPriorityCommand(t.PublicId, -1), default);
        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task Move_rejects_a_decided_topic()
    {
        await using var db = NewDb(User("kc-sec", "Sec"), Clock(default));
        var t = await SeedTopicAsync(db, TopicStatus.Decided);
        var act = () => MovePriority(db).Handle(new MoveTopicPriorityCommand(t.PublicId, -1), default);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Move_up_at_the_top_of_the_column_is_a_no_op()
    {
        await using var db = NewDb(User("kc-sec", "Sec"), Clock(default));
        var a = await SeedSubmittedAsync(db, "TOP-2026-201");   // order by Key → a is first
        await SeedSubmittedAsync(db, "TOP-2026-202");

        await MovePriority(db).Handle(new MoveTopicPriorityCommand(a.PublicId, -1), default);

        (await db.Topics.ToListAsync()).Should().OnlyContain(t => t.Priority == 0);   // no swap → no renumber
    }

    [Fact]
    public async Task Move_down_swaps_the_neighbour_and_renumbers_the_column_contiguously()
    {
        await using var db = NewDb(User("kc-sec", "Sec"), Clock(default));
        var a = await SeedSubmittedAsync(db, "TOP-2026-201");
        await SeedSubmittedAsync(db, "TOP-2026-202");
        await SeedSubmittedAsync(db, "TOP-2026-203");
        var audit = Substitute.For<IAuditSink>();

        // initial order by (Priority 0, CreatedAt, Key) = [201, 202, 203]. Move 201 DOWN → [202, 201, 203], 1..3.
        await MovePriority(db, audit).Handle(new MoveTopicPriorityCommand(a.PublicId, 1), default);

        var byKey = await db.Topics.ToDictionaryAsync(t => t.Key, t => t.Priority);
        byKey["TOP-2026-202"].Should().Be(1);
        byKey["TOP-2026-201"].Should().Be(2);
        byKey["TOP-2026-203"].Should().Be(3);
        await audit.Received(1).EmitEnrichedAsync("Topics.TopicReordered", "Topic", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ---- MOVE semantics, widened for drag-and-drop (AC-141 / FR-037, DW-040) ----
    //
    // The delta was ±1-only and the operation was a SWAP. Drag needs neither: dragging a card from index 4
    // to index 0 must shift 0..3 down by one, which a swap does not do. These four tests exist to pin both
    // halves of that change — that ±1 behaviour is UNCHANGED, and that larger magnitudes are a move rather
    // than a swap. Without the first, widening the delta would be a silent regression of AC-043's keyboard
    // path; without the second, the widening would ship swap semantics under a drag gesture.

    [Fact]
    public async Task Move_by_more_than_one_MOVES_rather_than_swapping()
    {
        await using var db = NewDb(User("kc-sec", "Sec"), Clock(default));
        await SeedSubmittedAsync(db, "TOP-2026-201");
        await SeedSubmittedAsync(db, "TOP-2026-202");
        await SeedSubmittedAsync(db, "TOP-2026-203");
        var d = await SeedSubmittedAsync(db, "TOP-2026-204");

        // Order by (Priority 0, CreatedAt, Key) = [201, 202, 203, 204]. Drag 204 to the top: delta -3.
        await MovePriority(db).Handle(new MoveTopicPriorityCommand(d.PublicId, -3), default);

        var byKey = await db.Topics.ToDictionaryAsync(t => t.Key, t => t.Priority);
        // MOVE → [204, 201, 202, 203]: everything it passed shifts down one.
        byKey["TOP-2026-204"].Should().Be(1);
        byKey["TOP-2026-201"].Should().Be(2);
        byKey["TOP-2026-202"].Should().Be(3);
        byKey["TOP-2026-203"].Should().Be(4);
        // A SWAP would have produced [204, 202, 203, 201], leaving 202 and 203 untouched at 2 and 3.
        // Asserting 201 lands at 2 is precisely what discriminates the two; it is the whole point of this test.
    }

    [Fact]
    public async Task Move_by_one_is_still_indistinguishable_from_the_swap_it_replaced()
    {
        // AC-043's keyboard path must be untouched by the widening. Same fixture and same assertion as
        // Move_down_swaps_the_neighbour_and_renumbers_the_column_contiguously, kept separate so a future
        // change to move semantics fails HERE with a name that says what broke.
        await using var db = NewDb(User("kc-sec", "Sec"), Clock(default));
        var a = await SeedSubmittedAsync(db, "TOP-2026-201");
        await SeedSubmittedAsync(db, "TOP-2026-202");
        await SeedSubmittedAsync(db, "TOP-2026-203");

        await MovePriority(db).Handle(new MoveTopicPriorityCommand(a.PublicId, 1), default);

        var byKey = await db.Topics.ToDictionaryAsync(t => t.Key, t => t.Priority);
        byKey["TOP-2026-202"].Should().Be(1);
        byKey["TOP-2026-201"].Should().Be(2);
        byKey["TOP-2026-203"].Should().Be(3);
    }

    [Fact]
    public async Task Move_past_the_end_of_the_column_is_a_no_op()
    {
        await using var db = NewDb(User("kc-sec", "Sec"), Clock(default));
        var a = await SeedSubmittedAsync(db, "TOP-2026-201");
        await SeedSubmittedAsync(db, "TOP-2026-202");

        // Delta overshoots the column; the handler returns before touching anything rather than clamping,
        // so priorities stay at their un-renumbered default of 0.
        await MovePriority(db).Handle(new MoveTopicPriorityCommand(a.PublicId, 99), default);

        (await db.Topics.ToListAsync()).Should().OnlyContain(t => t.Priority == 0);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(-1, true)]
    [InlineData(5, true)]
    [InlineData(-5, true)]
    public void Move_validator_rejects_only_a_zero_delta(int delta, bool expectedValid)
    {
        // Zero is refused rather than silently no-oping, so a bugged caller is told. Everything else is a
        // legal signed offset — the validator is what used to make drag impossible to express.
        var result = new MoveTopicPriorityValidator().Validate(new MoveTopicPriorityCommand(Guid.NewGuid(), delta));
        result.IsValid.Should().Be(expectedValid);
    }

    // ---- Target-topic addressing: the DRAG path (AC-141 / FR-037) ----
    //
    // ⚠ WHY THIS MODE EXISTS AT ALL, so nobody "simplifies" it back to a delta. The kanban renders the
    // FILTERED, SORTED and PAGE-TRUNCATED backlog result, so the index a user sees is not the index of the
    // canonical column this handler orders by. A client-computed positional delta would therefore move the
    // topic somewhere else entirely whenever a filter is active, a sort persists from the table view, or the
    // column runs past one page. Sending the target's IDENTITY makes the client's ordering irrelevant.

    [Fact]
    public async Task Move_to_a_target_topic_places_it_at_that_target_position()
    {
        await using var db = NewDb(User("kc-sec", "Sec"), Clock(default));
        var a = await SeedSubmittedAsync(db, "TOP-2026-201");
        await SeedSubmittedAsync(db, "TOP-2026-202");
        await SeedSubmittedAsync(db, "TOP-2026-203");
        var d = await SeedSubmittedAsync(db, "TOP-2026-204");

        // [201, 202, 203, 204] — drag 204 onto 201 (the top card).
        await MovePriority(db).Handle(new MoveTopicPriorityCommand(d.PublicId, 0, a.PublicId), default);

        var byKey = await db.Topics.ToDictionaryAsync(t => t.Key, t => t.Priority);
        byKey["TOP-2026-204"].Should().Be(1);
        byKey["TOP-2026-201"].Should().Be(2);
        byKey["TOP-2026-202"].Should().Be(3);
        byKey["TOP-2026-203"].Should().Be(4);
    }

    [Fact]
    public async Task Move_to_a_target_ignores_the_callers_own_idea_of_position()
    {
        // THE POINT OF THE WHOLE ADDRESSING MODE, expressed as a test. The command carries Delta = 0 — a
        // value the validator refuses on its own and which would be meaningless arithmetic — and the move
        // still lands correctly, because the destination comes from the target's identity and nothing else.
        // If anyone reintroduces "target = index + Delta" as a shortcut, this test is what fails.
        await using var db = NewDb(User("kc-sec", "Sec"), Clock(default));
        await SeedSubmittedAsync(db, "TOP-2026-201");
        var b = await SeedSubmittedAsync(db, "TOP-2026-202");
        var c = await SeedSubmittedAsync(db, "TOP-2026-203");

        await MovePriority(db).Handle(new MoveTopicPriorityCommand(b.PublicId, 0, c.PublicId), default);

        var byKey = await db.Topics.ToDictionaryAsync(t => t.Key, t => t.Priority);
        byKey["TOP-2026-201"].Should().Be(1);
        byKey["TOP-2026-203"].Should().Be(2);
        byKey["TOP-2026-202"].Should().Be(3);
    }

    [Fact]
    public async Task Move_to_a_target_in_another_column_is_refused()
    {
        // Cross-column drag is a STATUS change (FR-033), a different requirement handled by a different
        // endpoint. Reordering must not quietly become a transition, so a target outside the topic's own
        // bucket is a no-op rather than a partial move.
        await using var db = NewDb(User("kc-sec", "Sec"), Clock(default));
        var a = await SeedSubmittedAsync(db, "TOP-2026-201");   // triage bucket
        var other = await SeedTopicAsync(db, TopicStatus.Accepted);   // accepted bucket

        await MovePriority(db).Handle(new MoveTopicPriorityCommand(a.PublicId, 0, other.PublicId), default);

        (await db.Topics.ToListAsync()).Should().OnlyContain(t => t.Priority == 0);   // nothing renumbered
    }

    [Theory]
    [InlineData(1, false, false)]    // delta only → valid
    [InlineData(0, true, false)]     // target only → valid
    [InlineData(1, true, true)]      // BOTH → refused: the handler must never pick a winner silently
    [InlineData(0, false, true)]     // NEITHER → refused: a request that asks for nothing
    public void Move_validator_requires_exactly_one_addressing_mode(int delta, bool withTarget, bool expectInvalid)
    {
        var cmd = new MoveTopicPriorityCommand(Guid.NewGuid(), delta, withTarget ? Guid.NewGuid() : null);
        new MoveTopicPriorityValidator().Validate(cmd).IsValid.Should().Be(!expectInvalid);
    }

    [Fact]
    public void Move_validator_refuses_a_topic_targeting_itself()
    {
        var id = Guid.NewGuid();
        new MoveTopicPriorityValidator().Validate(new MoveTopicPriorityCommand(id, 0, id))
            .IsValid.Should().BeFalse();
    }
}
