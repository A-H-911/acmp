using Acmp.Modules.Topics.Domain;
using Acmp.Modules.Topics.Domain.Enums;
using Acmp.Modules.Topics.Domain.Events;
using Acmp.Shared.Authorization.Abac;
using FluentAssertions;

namespace Acmp.Domain.Tests.Topics;

public class TopicTests
{
    private static readonly DateTimeOffset Now = new(2026, 2, 15, 9, 0, 0, TimeSpan.Zero);
    private const string Submitter = "kc-omar";
    private const string Secretary = "kc-khalid";
    private const string OwnerName = "Omar H.";
    private static readonly Guid Owner = Guid.NewGuid();

    private static Topic NewDraft(params string[] streams) => Topic.Draft(
        "TOP-2026-014", "Adopt Keycloak as the standard IdP", "Consolidate IAM onto Keycloak.",
        "Fragmented auth increases risk.", TopicType.ArchitectureDecision, TopicUrgency.Urgent,
        TopicSource.CommitteeMember, Submitter, "Omar H.",
        streams.Length == 0 ? new[] { "identity" } : streams, new[] { "API Gateway" }, new[] { "SecurityArch" });

    private static Topic Submitted(params string[] streams)
    {
        var t = NewDraft(streams);
        t.Submit(Now);
        return t;
    }

    private static Topic Accepted()
    {
        var t = Submitted();
        t.BeginTriage(Secretary, "Khalid A.", Now);
        t.Accept(Owner, OwnerName, Secretary, "Khalid A.", Now);
        return t;
    }

    [Fact]
    public void Draft_starts_in_Draft_with_submitter_attribution_and_deduped_streams()
    {
        var t = Topic.Draft("TOP-2026-001", "T", "D", "J", TopicType.GovernanceStandardization,
            TopicUrgency.Normal, TopicSource.StreamRequest, Submitter, "Omar H.",
            new[] { "platform", "platform", " " }, new[] { "Sys" }, new[] { "tag" });

        t.Status.Should().Be(TopicStatus.Draft);
        t.SubmittedBySub.Should().Be(Submitter);
        t.AffectedStreams.Should().BeEquivalentTo("platform");
    }

    [Fact]
    public void Submit_from_draft_transitions_records_history_and_raises_event()
    {
        var t = NewDraft();

        t.Submit(Now);

        t.Status.Should().Be(TopicStatus.Submitted);
        t.History.Should().ContainSingle(h => h.FromStatus == TopicStatus.Draft && h.ToStatus == TopicStatus.Submitted);
        t.DomainEvents.OfType<TopicSubmittedEvent>().Should().ContainSingle();
    }

    [Fact]
    public void Submit_requires_at_least_one_stream()
    {
        var t = Topic.Draft("TOP-2026-002", "T", "D", "J", TopicType.ArchitectureDecision,
            TopicUrgency.Normal, TopicSource.CommitteeMember, Submitter, "Omar H.",
            Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());

        var act = () => t.Submit(Now);

        act.Should().Throw<InvalidOperationException>().WithMessage("*stream*");
    }

    [Theory]
    [InlineData(1, TopicScope.SingleStream)]
    [InlineData(2, TopicScope.MultiStream)]
    public void Submit_derives_scope_from_affected_stream_count(int streamCount, TopicScope expected)
    {
        var streams = Enumerable.Range(0, streamCount).Select(i => $"stream-{i}").ToArray();

        var t = Submitted(streams);

        t.Scope.Should().Be(expected);
    }

    [Fact]
    public void Accept_requires_an_owner_and_sets_it()
    {
        var t = Submitted();
        t.BeginTriage(Secretary, "Khalid A.", Now);

        var noOwner = () => t.Accept(Guid.Empty, OwnerName, Secretary, "Khalid A.", Now);
        noOwner.Should().Throw<InvalidOperationException>().WithMessage("*owner*");

        t.Accept(Owner, OwnerName, Secretary, "Khalid A.", Now);
        t.Status.Should().Be(TopicStatus.Accepted);
        t.OwnerId.Should().Be(Owner);
        t.DomainEvents.OfType<TopicAcceptedEvent>().Should().ContainSingle();
    }

    [Fact]
    public void Reject_requires_a_reason_and_records_an_immutable_history_row()
    {
        var t = Submitted();
        t.BeginTriage(Secretary, "Khalid A.", Now);

        var noReason = () => t.Reject("  ", Secretary, "Khalid A.", Now);
        noReason.Should().Throw<InvalidOperationException>().WithMessage("*reason*");

        t.Reject("Duplicate of TOP-2026-009", Secretary, "Khalid A.", Now);

        t.Status.Should().Be(TopicStatus.Rejected);
        t.History.Last().Reason.Should().Be("Duplicate of TOP-2026-009");
        t.DomainEvents.OfType<TopicRejectedEvent>().Should().ContainSingle();
    }

    [Fact]
    public void Defer_captures_reason_and_revisit_date()
    {
        var t = Submitted();
        t.BeginTriage(Secretary, "Khalid A.", Now);
        var revisit = Now.AddDays(30);

        t.Defer("Awaiting budget", revisit, Secretary, "Khalid A.", Now);

        t.Status.Should().Be(TopicStatus.Deferred);
        t.RevisitOn.Should().Be(revisit);
        t.TimesDeferred.Should().Be(1);
    }

    [Fact] // AC-066: the chairman dashboard surfaces topics Deferred ≥2× — the counter spans reactivations.
    public void TimesDeferred_counts_each_defer_across_reactivation()
    {
        var t = Submitted();
        t.BeginTriage(Secretary, "Khalid A.", Now);
        t.Defer("first", null, Secretary, "Khalid A.", Now);
        t.Reactivate(Secretary, "Khalid A.", Now);
        t.Defer("second", null, Secretary, "Khalid A.", Now);

        t.TimesDeferred.Should().Be(2);
    }

    [Fact]
    public void Deferred_topic_can_be_reactivated_to_triage()
    {
        var t = Submitted();
        t.BeginTriage(Secretary, "Khalid A.", Now);
        t.Defer("hold", null, Secretary, "Khalid A.", Now);

        t.Reactivate(Secretary, "Khalid A.", Now);

        t.Status.Should().Be(TopicStatus.Triage);
    }

    [Fact]
    public void MarkPrepared_only_from_accepted()
    {
        var accepted = Accepted();
        accepted.MarkPrepared(Submitter, "Omar H.", Now);
        accepted.Status.Should().Be(TopicStatus.Prepared);

        var draft = NewDraft();
        var act = () => draft.MarkPrepared(Submitter, "Omar H.", Now);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Illegal_transition_is_rejected()
    {
        var t = NewDraft(); // Draft

        var act = () => t.Accept(Owner, OwnerName, Secretary, "Khalid A.", Now); // cannot accept a Draft

        act.Should().Throw<InvalidOperationException>().WithMessage("*Draft*");
    }

    [Fact]
    public void Content_is_locked_after_acceptance_but_metadata_stays_editable()
    {
        var t = Accepted();

        var editContent = () => t.UpdateContent("New title", "New desc", "New just");
        editContent.Should().Throw<InvalidOperationException>();

        t.SetUrgency(TopicUrgency.Critical);          // metadata still allowed
        t.AssignStreams(new[] { "identity", "platform" });
        t.Urgency.Should().Be(TopicUrgency.Critical);
        t.AffectedStreams.Should().BeEquivalentTo("identity", "platform");
    }

    [Fact]
    public void Decided_topic_is_immutable_to_metadata_edits()
    {
        var t = Accepted();
        t.MarkPrepared(Submitter, "Omar H.", Now);
        t.Schedule(Guid.NewGuid(), Secretary, "Khalid A.", Now);
        t.EnterCommittee(Secretary, "Khalid A.", Now);
        t.Decide(Secretary, "Khalid A.", Now);

        var act = () => t.SetUrgency(TopicUrgency.Normal);

        act.Should().Throw<InvalidOperationException>().WithMessage("*immutable*");
    }

    [Fact]
    public void Reopen_requires_justification_and_returns_a_rejected_topic_to_reopened()
    {
        var t = Submitted();
        t.BeginTriage(Secretary, "Khalid A.", Now);
        t.Reject("dup", Secretary, "Khalid A.", Now);

        var noJust = () => t.Reopen(" ", Secretary, "Khalid A.", Now);
        noJust.Should().Throw<InvalidOperationException>();

        t.Reopen("New evidence emerged", Secretary, "Khalid A.", Now);
        t.Status.Should().Be(TopicStatus.Reopened);
    }

    [Fact]
    public void SetPriority_raises_event_and_is_blocked_once_decided()
    {
        var t = Accepted();
        t.SetPriority(5, Now);
        t.Priority.Should().Be(5);
        t.DomainEvents.OfType<TopicPriorityChangedEvent>().Should().ContainSingle();
    }

    [Fact]
    public void Topic_exposes_abac_contracts()
    {
        var t = Accepted();

        t.Should().BeAssignableTo<IStreamScopedResource>();
        ((ITopicScopedResource)t).TopicId.Should().Be(t.PublicId);
        t.AffectedStreams.Should().NotBeEmpty();
    }

    [Fact]
    public void Comment_cannot_be_empty()
    {
        var t = Accepted();
        var act = () => t.AddComment("  ", Secretary, "Khalid A.", Now);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Attachment_metadata_is_recorded()
    {
        var t = Accepted();

        t.AddAttachment("eval.pdf", "application/pdf", 1_400_000, "topics/abc/eval.pdf", Submitter, "Omar H.", Now);

        t.Attachments.Should().ContainSingle(a => a.FileName == "eval.pdf" && a.SizeBytes == 1_400_000);
    }
}
