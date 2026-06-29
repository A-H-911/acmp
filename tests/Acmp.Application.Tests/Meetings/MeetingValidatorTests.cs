using Acmp.Modules.Meetings.Application.Features.AgendaBuilder;
using Acmp.Modules.Meetings.Application.Features.CancelMeeting;
using Acmp.Modules.Meetings.Application.Features.ConductMeeting;
using Acmp.Modules.Meetings.Domain.Enums;
using FluentAssertions;

namespace Acmp.Application.Tests.Meetings;

// Pure boundary validation for the conduct/cancel/agenda-edit commands (no DbContext) — the failure-first
// half of S1 (ADR-0016). Each rule is asserted on its own so a regression points at the exact field.
public class MeetingValidatorTests
{
    // ---- AssignPresenterValidator ----

    [Fact]
    public void AssignPresenter_requires_every_identifier_and_a_name()
    {
        var v = new AssignPresenterValidator();
        var ok = new AssignPresenterCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Noura P.");

        v.Validate(ok).IsValid.Should().BeTrue();
        v.Validate(ok with { MeetingId = Guid.Empty }).IsValid.Should().BeFalse();
        v.Validate(ok with { TopicId = Guid.Empty }).IsValid.Should().BeFalse();
        v.Validate(ok with { PresenterUserId = Guid.Empty }).IsValid.Should().BeFalse();
        v.Validate(ok with { PresenterName = "" }).IsValid.Should().BeFalse();
    }

    // ---- CaptureDiscussionValidator ----

    [Fact]
    public void CaptureDiscussion_requires_ids_and_a_non_empty_body()
    {
        var v = new CaptureDiscussionValidator();
        var ok = new CaptureDiscussionCommand(Guid.NewGuid(), Guid.NewGuid(), "Consensus on direction.");

        v.Validate(ok).IsValid.Should().BeTrue();
        v.Validate(ok with { MeetingId = Guid.Empty }).IsValid.Should().BeFalse();
        v.Validate(ok with { TopicId = Guid.Empty }).IsValid.Should().BeFalse();
        v.Validate(ok with { Body = "" }).IsValid.Should().BeFalse();
    }

    // ---- MarkAttendanceValidator ----

    [Fact]
    public void MarkAttendance_requires_meeting_user_and_name()
    {
        var v = new MarkAttendanceValidator();
        var ok = new MarkAttendanceCommand(Guid.NewGuid(), Guid.NewGuid(), "Omar H.",
            AttendanceRole.Member, AttendanceStatus.Present, true);

        v.Validate(ok).IsValid.Should().BeTrue();
        v.Validate(ok with { MeetingId = Guid.Empty }).IsValid.Should().BeFalse();
        v.Validate(ok with { UserId = Guid.Empty }).IsValid.Should().BeFalse();
        v.Validate(ok with { Name = "" }).IsValid.Should().BeFalse();
    }

    // ---- CancelMeetingValidator ----

    [Fact]
    public void CancelMeeting_requires_a_meeting_and_a_reason()
    {
        var v = new CancelMeetingValidator();
        var ok = new CancelMeetingCommand(Guid.NewGuid(), "Quorum will not be met");

        v.Validate(ok).IsValid.Should().BeTrue();
        v.Validate(ok with { MeetingId = Guid.Empty }).IsValid.Should().BeFalse();
        v.Validate(ok with { Reason = "" }).IsValid.Should().BeFalse();
    }
}
