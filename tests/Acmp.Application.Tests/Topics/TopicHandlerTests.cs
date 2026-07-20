using Acmp.Modules.Topics.Application.Features.DeferTopic;
using Acmp.Modules.Topics.Application.Features.GetBacklog;
using Acmp.Modules.Topics.Application.Features.GetTopicDetail;
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
        var backlog = await new GetBacklogHandler(db, Clock(later)).Handle(new GetBacklogQuery(), CancellationToken.None);
        backlog.Total.Should().Be(1);
        backlog.Items[0].Key.Should().Be(result.Key);
        backlog.Items[0].SlaBreached.Should().BeTrue();
        backlog.Items[0].AgeDays.Should().Be(9);

        var detail = await new GetTopicDetailHandler(db, Clock(later)).Handle(new GetTopicDetailQuery(result.Key), CancellationToken.None);
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
}
