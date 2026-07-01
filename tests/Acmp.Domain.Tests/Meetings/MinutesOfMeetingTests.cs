using Acmp.Modules.Meetings.Domain;
using Acmp.Modules.Meetings.Domain.Enums;
using Acmp.Modules.Meetings.Domain.Events;
using Acmp.Shared.Domain.ValueObjects;
using FluentAssertions;

namespace Acmp.Domain.Tests.Meetings;

// W10 aggregate behaviour: draft guards, the 5-state lifecycle (submit/request-changes/approve/publish),
// version-preserving supersede, and the AC-036 immutability that there is no edit path once past Draft.
public class MinutesOfMeetingTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid MeetingId = Guid.NewGuid();
    private static readonly LocalizedString Summary = LocalizedString.Create("Discussed the roadmap", "نوقشت خارطة الطريق");

    private static MinutesOfMeeting Drafted() =>
        MinutesOfMeeting.Draft("MIN-2026-001", MeetingId, "MTG-2026-001", "Weekly Committee", Summary, Now);

    private static MinutesOfMeeting InReview()
    {
        var m = Drafted();
        m.SubmitForReview(Now);
        return m;
    }

    [Fact]
    public void Draft_starts_Draft_v1_and_raises_event()
    {
        var m = Drafted();

        m.Status.Should().Be(MinutesStatus.Draft);
        m.Version.Should().Be(1);
        m.MeetingId.Should().Be(MeetingId);
        m.MeetingKey.Should().Be("MTG-2026-001");
        m.DomainEvents.OfType<MinutesDraftedEvent>().Should().ContainSingle();
    }

    [Fact]
    public void Draft_requires_a_meeting_a_key_and_a_summary()
    {
        var noMeeting = () => MinutesOfMeeting.Draft("MIN-2026-002", Guid.Empty, "MTG", "T", Summary, Now);
        noMeeting.Should().Throw<InvalidOperationException>().WithMessage("*meeting*");

        var noKey = () => MinutesOfMeeting.Draft("", MeetingId, "MTG", "T", Summary, Now);
        noKey.Should().Throw<InvalidOperationException>().WithMessage("*key*");

        var noSummary = () => MinutesOfMeeting.Draft("MIN-2026-002", MeetingId, "MTG", "T", null!, Now);
        noSummary.Should().Throw<InvalidOperationException>().WithMessage("*summary*");
    }

    [Fact]
    public void Revise_updates_the_body_only_while_Draft()
    {
        var m = Drafted();
        var edited = LocalizedString.Create("Revised", "منقّح");
        m.Revise(edited, Now);
        m.Summary.Should().Be(edited);

        m.SubmitForReview(Now); // now InReview — revise must be rejected (immutable past Draft)
        var act = () => m.Revise(edited, Now);
        act.Should().Throw<InvalidOperationException>().WithMessage("*InReview*");
    }

    [Fact]
    public void SubmitForReview_moves_Draft_to_InReview()
    {
        var m = Drafted();
        m.SubmitForReview(Now);
        m.Status.Should().Be(MinutesStatus.InReview);
        m.DomainEvents.OfType<MinutesInReviewEvent>().Should().ContainSingle();
    }

    [Fact] // AC-037: a change-request bounces InReview back to Draft
    public void RequestChanges_moves_InReview_back_to_Draft()
    {
        var m = InReview();
        m.RequestChanges(Now);
        m.Status.Should().Be(MinutesStatus.Draft);
        m.DomainEvents.OfType<MinutesChangesRequestedEvent>().Should().ContainSingle();

        var fromDraft = () => m.RequestChanges(Now); // only valid from InReview
        fromDraft.Should().Throw<InvalidOperationException>().WithMessage("*Draft*");
    }

    [Fact] // AC-014: approval records the approver and the soft-SoD-2 sole-author flag
    public void Approve_moves_InReview_to_Approved_and_records_attribution()
    {
        var m = InReview();
        m.Approve("kc-chair", "Sara Chair", isSoleAuthor: true, Now);

        m.Status.Should().Be(MinutesStatus.Approved);
        m.ApprovedByUserId.Should().Be("kc-chair");
        m.ApprovedByName.Should().Be("Sara Chair");
        m.ApprovedAt.Should().Be(Now);
        m.ApprovedBySoleAuthor.Should().BeTrue();
        m.DomainEvents.OfType<MinutesApprovedEvent>().Single().SoleAuthor.Should().BeTrue();
    }

    [Fact]
    public void Approve_is_only_valid_from_InReview()
    {
        var m = Drafted();
        var act = () => m.Approve("kc", "Chair", false, Now);
        act.Should().Throw<InvalidOperationException>().WithMessage("*Draft*");
    }

    [Fact] // AC-038 (state half): publish seals the record
    public void Publish_moves_Approved_to_Published()
    {
        var m = InReview();
        m.Approve("kc", "Chair", false, Now);
        m.Publish(Now);

        m.Status.Should().Be(MinutesStatus.Published);
        m.PublishedAt.Should().Be(Now);
        m.DomainEvents.OfType<MinutesPublishedEvent>().Should().ContainSingle();

        var again = () => m.Publish(Now); // Published is terminal — no re-publish
        again.Should().Throw<InvalidOperationException>().WithMessage("*Published*");
    }

    [Fact]
    public void Publish_requires_Approved()
    {
        var m = InReview();
        var act = () => m.Publish(Now); // still InReview, not Approved
        act.Should().Throw<InvalidOperationException>().WithMessage("*InReview*");
    }

    [Fact] // AC-036: supersede from Approved OR Published records the back-link + reason
    public void Supersede_is_valid_from_Approved_or_Published_and_records_the_backlink()
    {
        var reason = LocalizedString.Create("Corrected attendance", "تصحيح الحضور");

        var fromDraft = Drafted();
        var beforeApprove = () => fromDraft.Supersede(Guid.NewGuid(), reason, Now);
        beforeApprove.Should().Throw<InvalidOperationException>().WithMessage("*Draft*");

        var approved = InReview();
        approved.Approve("kc", "Chair", false, Now);
        var successorA = Guid.NewGuid();
        approved.Supersede(successorA, reason, Now);
        approved.Status.Should().Be(MinutesStatus.Superseded);
        approved.SupersededByMinutesId.Should().Be(successorA);
        approved.SupersessionReason.Should().Be(reason);

        var published = InReview();
        published.Approve("kc", "Chair", false, Now);
        published.Publish(Now);
        published.Supersede(Guid.NewGuid(), reason, Now);
        published.Status.Should().Be(MinutesStatus.Superseded);
        published.DomainEvents.OfType<MinutesSupersededEvent>().Should().ContainSingle();
    }

    [Fact]
    public void Supersede_requires_a_successor_and_a_reason()
    {
        var m = InReview();
        m.Approve("kc", "Chair", false, Now);

        var noSuccessor = () => m.Supersede(Guid.Empty, LocalizedString.Create("r", "ر"), Now);
        noSuccessor.Should().Throw<InvalidOperationException>().WithMessage("*superseding*");
    }

    [Fact] // AC-036: the successor is a NEW version (Version+1) under the SAME key, already Published
    public void PublishedCorrection_builds_a_published_next_version()
    {
        var successor = MinutesOfMeeting.PublishedCorrection("MIN-2026-001", MeetingId, "MTG-2026-001",
            "Weekly Committee", version: 2, Summary, "kc-chair", "Sara Chair", Now);

        successor.Key.Should().Be("MIN-2026-001");
        successor.Version.Should().Be(2);
        successor.Status.Should().Be(MinutesStatus.Published);
        successor.PublishedAt.Should().Be(Now);
        successor.ApprovedByUserId.Should().Be("kc-chair");
        successor.ApprovedBySoleAuthor.Should().BeFalse(); // a supersession, not the AC-014 sole-author scenario
        successor.DomainEvents.OfType<MinutesPublishedEvent>().Should().ContainSingle();
    }

    [Fact] // AC-036 immutability: the aggregate exposes no public state setters — change is via transitions only
    public void Minutes_has_no_public_mutable_state()
    {
        typeof(MinutesOfMeeting)
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.DeclaredOnly)
            .Where(p => p.CanWrite && p.SetMethod!.IsPublic)
            .Should().BeEmpty();
    }
}
