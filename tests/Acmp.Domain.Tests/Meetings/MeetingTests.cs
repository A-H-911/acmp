using Acmp.Modules.Meetings.Domain;
using Acmp.Modules.Meetings.Domain.Enums;
using Acmp.Modules.Meetings.Domain.Events;
using FluentAssertions;

namespace Acmp.Domain.Tests.Meetings;

public class MeetingTests
{
    private static readonly DateTimeOffset Now = new(2026, 2, 18, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid Committee = Guid.NewGuid();
    private static readonly Guid Chair = Guid.NewGuid();

    private static Meeting Scheduled() => Meeting.Schedule(
        "MTG-2026-019", "Weekly Architecture Committee", Committee, Chair, "S. M.",
        Now, Now.AddMinutes(90), location: null, joinUrl: null, Now);

    [Fact]
    public void Schedule_starts_Scheduled_and_raises_event()
    {
        var m = Scheduled();

        m.Status.Should().Be(MeetingStatus.Scheduled);
        m.ChairUserId.Should().Be(Chair);
        m.DomainEvents.OfType<MeetingScheduledEvent>().Should().ContainSingle();
    }

    [Fact]
    public void Schedule_requires_end_after_start_a_title_and_a_chair()
    {
        var badWindow = () => Meeting.Schedule("MTG-2026-020", "T", Committee, Chair, "S. M.", Now, Now, null, null, Now);
        badWindow.Should().Throw<InvalidOperationException>().WithMessage("*end*");

        var noTitle = () => Meeting.Schedule("MTG-2026-020", "  ", Committee, Chair, "S. M.", Now, Now.AddMinutes(60), null, null, Now);
        noTitle.Should().Throw<InvalidOperationException>().WithMessage("*title*");

        var noChair = () => Meeting.Schedule("MTG-2026-020", "T", Committee, Guid.Empty, "S. M.", Now, Now.AddMinutes(60), null, null, Now);
        noChair.Should().Throw<InvalidOperationException>().WithMessage("*chair*");
    }

    [Fact]
    public void Lifecycle_advances_Scheduled_to_InProgress_to_Held()
    {
        var m = Scheduled();

        m.Start(Now);
        m.Status.Should().Be(MeetingStatus.InProgress);
        m.StartedAt.Should().Be(Now);
        m.DomainEvents.OfType<MeetingStartedEvent>().Should().ContainSingle();

        m.Hold(Now.AddMinutes(85));
        m.Status.Should().Be(MeetingStatus.Held);
        m.HeldAt.Should().Be(Now.AddMinutes(85));
        m.DomainEvents.OfType<MeetingHeldEvent>().Should().ContainSingle();
    }

    [Fact]
    public void Cancel_requires_a_reason_and_only_from_Scheduled()
    {
        var m = Scheduled();
        var noReason = () => m.Cancel(" ", Now);
        noReason.Should().Throw<InvalidOperationException>().WithMessage("*reason*");

        m.Cancel("Quorum unlikely", Now);
        m.Status.Should().Be(MeetingStatus.Cancelled);
        m.CancellationReason.Should().Be("Quorum unlikely");
        m.DomainEvents.OfType<MeetingCancelledEvent>().Should().ContainSingle();
    }

    [Fact]
    public void Cannot_start_a_held_meeting()
    {
        var m = Scheduled();
        m.Start(Now);
        m.Hold(Now.AddMinutes(80));

        var act = () => m.Start(Now);
        act.Should().Throw<InvalidOperationException>().WithMessage("*Held*");
    }

    [Fact]
    public void Attendance_roster_is_seeded_idempotently_and_marked()
    {
        var m = Scheduled();
        var user = Guid.NewGuid();

        m.SeedAttendee(user, "Omar H.", AttendanceRole.Member, isVotingEligible: true);
        m.SeedAttendee(user, "Omar H.", AttendanceRole.Member, isVotingEligible: true); // idempotent
        m.Attendees.Should().ContainSingle(a => a.UserId == user);
        m.Attendees.Single().Status.Should().Be(AttendanceStatus.Invited);

        m.Start(Now);
        m.MarkAttendance(user, AttendanceStatus.Present, Now);
        m.Attendees.Single().Status.Should().Be(AttendanceStatus.Present);
        m.Attendees.Single().JoinedAt.Should().Be(Now);
        m.PresentCount.Should().Be(1);
    }

    [Fact]
    public void Marking_attendance_for_someone_off_the_roster_is_rejected()
    {
        var m = Scheduled();
        var act = () => m.MarkAttendance(Guid.NewGuid(), AttendanceStatus.Present, Now);
        act.Should().Throw<InvalidOperationException>().WithMessage("*roster*");
    }

    [Fact]
    public void Excused_is_the_apologies_state_and_does_not_count_toward_quorum()
    {
        var m = Scheduled();
        var user = Guid.NewGuid();
        m.SeedAttendee(user, "Yousef R.", AttendanceRole.Member, true);
        m.Start(Now);

        m.MarkAttendance(user, AttendanceStatus.Excused, Now);

        m.Attendees.Single().Status.Should().Be(AttendanceStatus.Excused);
        m.PresentCount.Should().Be(0);
    }

    [Fact]
    public void Discussion_note_is_upserted_per_topic_while_in_progress()
    {
        var m = Scheduled();
        var topic = Guid.NewGuid();
        m.Start(Now);

        m.SetDiscussionNote(topic, "Initial note.", "kc-khalid", "Khalid A.", Now);
        m.SetDiscussionNote(topic, "Refined note.", "kc-khalid", "Khalid A.", Now.AddMinutes(5));

        m.Discussions.Should().ContainSingle(d => d.TopicId == topic);
        m.Discussions.Single().Body.Should().Be("Refined note.");
        m.Discussions.Single().UpdatedAt.Should().Be(Now.AddMinutes(5));
    }

    [Fact]
    public void Discussion_capture_requires_an_in_progress_meeting()
    {
        var m = Scheduled();
        var act = () => m.SetDiscussionNote(Guid.NewGuid(), "note", "kc", "K", Now);
        act.Should().Throw<InvalidOperationException>().WithMessage("*Scheduled*");
    }
}
