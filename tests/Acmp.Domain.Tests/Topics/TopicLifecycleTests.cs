using Acmp.Modules.Topics.Domain;
using Acmp.Modules.Topics.Domain.Enums;
using Acmp.Modules.Topics.Domain.Events;
using FluentAssertions;

namespace Acmp.Domain.Tests.Topics;

/// <summary>
/// Covers the terminal lifecycle transitions Close() and Convert() — both of which were
/// 0%-covered — plus the domain events they raise (TopicClosedEvent / TopicConvertedEvent)
/// and the Closed/Converted branches of EnsureMutable() and Reopen().
/// </summary>
public sealed class TopicLifecycleTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 10, 10, 0, 0, TimeSpan.Zero);
    private const string ActorSub = "kc-khalid";
    private const string ActorName = "Khalid A.";
    private const string OwnerActorSub = "kc-omar";
    private const string OwnerActorName = "Omar H.";
    private static readonly Guid OwnerId = Guid.NewGuid();

    // ---- helpers ----

    private static Topic NewDraft() => Topic.Draft(
        "TOP-2026-099",
        "Lifecycle terminal-state tests",
        "Covers Close() and Convert() paths.",
        "Ensures terminal transitions are correct.",
        TopicType.ArchitectureDecision,
        TopicUrgency.Normal,
        TopicSource.CommitteeMember,
        OwnerActorSub,
        OwnerActorName,
        new[] { "identity" },
        new[] { "API Gateway" },
        new[] { "SecArch" });

    /// <summary>Drives the topic through the full linear happy-path to Accepted status.</summary>
    private static Topic Accepted()
    {
        var t = NewDraft();
        t.Submit(Now);
        t.BeginTriage(ActorSub, ActorName, Now);
        t.Accept(OwnerId, OwnerActorName, ActorSub, ActorName, Now);
        return t;
    }

    /// <summary>Drives the topic through the full linear happy-path to Decided status.</summary>
    private static Topic Decided()
    {
        var t = Accepted();
        t.MarkPrepared(OwnerActorSub, OwnerActorName, Now);
        t.Schedule(Guid.NewGuid(), ActorSub, ActorName, Now);
        t.EnterCommittee(ActorSub, ActorName, Now);
        t.Decide(ActorSub, ActorName, Now);
        return t;
    }

    // ---- Close() happy path ----

    [Fact]
    public void Close_from_Decided_transitions_status_to_Closed()
    {
        var t = Decided();

        t.Close(ActorSub, ActorName, Now);

        t.Status.Should().Be(TopicStatus.Closed);
    }

    [Fact]
    public void Close_records_history_row_from_Decided_to_Closed()
    {
        var t = Decided();

        t.Close(ActorSub, ActorName, Now);

        t.History.Should().Contain(h =>
            h.FromStatus == TopicStatus.Decided && h.ToStatus == TopicStatus.Closed);
    }

    [Fact]
    public void Close_raises_TopicClosedEvent_with_matching_identity()
    {
        var t = Decided();
        var expectedPublicId = t.PublicId;
        var expectedKey = t.Key;

        t.Close(ActorSub, ActorName, Now);

        var evt = t.DomainEvents.OfType<TopicClosedEvent>().Should().ContainSingle().Subject;
        evt.TopicPublicId.Should().Be(expectedPublicId);
        evt.Key.Should().Be(expectedKey);
        evt.OccurredOn.Should().Be(Now);
    }

    // ---- Convert() happy path ----

    [Fact]
    public void Convert_from_Decided_transitions_status_to_Converted()
    {
        var t = Decided();

        t.Convert(ActorSub, ActorName, Now);

        t.Status.Should().Be(TopicStatus.Converted);
    }

    [Fact]
    public void Convert_records_history_row_from_Decided_to_Converted()
    {
        var t = Decided();

        t.Convert(ActorSub, ActorName, Now);

        t.History.Should().Contain(h =>
            h.FromStatus == TopicStatus.Decided && h.ToStatus == TopicStatus.Converted);
    }

    [Fact]
    public void Convert_raises_TopicConvertedEvent_with_matching_identity()
    {
        var t = Decided();
        var expectedPublicId = t.PublicId;
        var expectedKey = t.Key;

        t.Convert(ActorSub, ActorName, Now);

        var evt = t.DomainEvents.OfType<TopicConvertedEvent>().Should().ContainSingle().Subject;
        evt.TopicPublicId.Should().Be(expectedPublicId);
        evt.Key.Should().Be(expectedKey);
        evt.OccurredOn.Should().Be(Now);
    }

    // ---- guard failures (failure-first) ----

    [Fact]
    public void Close_from_non_Decided_status_throws_naming_the_current_status()
    {
        var t = Accepted(); // status = Accepted, not Decided

        var act = () => t.Close(ActorSub, ActorName, Now);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Accepted*");
    }

    [Fact]
    public void Convert_from_non_Decided_status_throws_naming_the_current_status()
    {
        var t = Accepted(); // status = Accepted, not Decided

        var act = () => t.Convert(ActorSub, ActorName, Now);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Accepted*");
    }

    [Fact]
    public void Close_from_Draft_throws_naming_Draft()
    {
        var t = NewDraft();

        var act = () => t.Close(ActorSub, ActorName, Now);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Draft*");
    }

    [Fact]
    public void Convert_from_Draft_throws_naming_Draft()
    {
        var t = NewDraft();

        var act = () => t.Convert(ActorSub, ActorName, Now);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Draft*");
    }

    // ---- Closed/Converted immutability (EnsureMutable Closed+Converted branches) ----

    [Fact]
    public void Closed_topic_is_immutable_to_metadata_edits()
    {
        var t = Decided();
        t.Close(ActorSub, ActorName, Now);

        var act = () => t.SetUrgency(TopicUrgency.Critical);

        act.Should().Throw<InvalidOperationException>().WithMessage("*immutable*");
    }

    [Fact]
    public void Converted_topic_is_immutable_to_metadata_edits()
    {
        var t = Decided();
        t.Convert(ActorSub, ActorName, Now);

        var act = () => t.SetUrgency(TopicUrgency.Critical);

        act.Should().Throw<InvalidOperationException>().WithMessage("*immutable*");
    }

    [Fact]
    public void Closed_topic_blocks_AddAttachment_as_immutable()
    {
        var t = Decided();
        t.Close(ActorSub, ActorName, Now);

        var act = () => t.AddAttachment("x.pdf", "application/pdf", 500, "key/x", OwnerActorSub, OwnerActorName, Now);

        act.Should().Throw<InvalidOperationException>().WithMessage("*immutable*");
    }

    [Fact]
    public void Converted_topic_blocks_AddAttachment_as_immutable()
    {
        var t = Decided();
        t.Convert(ActorSub, ActorName, Now);

        var act = () => t.AddAttachment("x.pdf", "application/pdf", 500, "key/x", OwnerActorSub, OwnerActorName, Now);

        act.Should().Throw<InvalidOperationException>().WithMessage("*immutable*");
    }

    // ---- Reopen from Closed (covers the Closed branch of Reopen's RequireStatus) ----

    [Fact]
    public void Closed_topic_can_be_reopened_with_a_justification()
    {
        var t = Decided();
        t.Close(ActorSub, ActorName, Now);

        t.Reopen("New evidence emerged", ActorSub, ActorName, Now);

        t.Status.Should().Be(TopicStatus.Reopened);
        t.DomainEvents.OfType<TopicReopenedEvent>().Should().ContainSingle();
    }

    [Fact]
    public void Reopen_from_Closed_without_justification_throws()
    {
        var t = Decided();
        t.Close(ActorSub, ActorName, Now);

        var act = () => t.Reopen("  ", ActorSub, ActorName, Now);

        act.Should().Throw<InvalidOperationException>();
    }
}
