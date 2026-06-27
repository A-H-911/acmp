using Acmp.Modules.Meetings.Domain.Enums;
using Acmp.Shared.Domain.Entities;

namespace Acmp.Modules.Meetings.Domain;

// A per-meeting presence record (docs/11 §C, W8) — quorum input. Mutated only via the Meeting root.
// Identity to Membership is by id + a display-name snapshot (no cross-module navigation, ADR-0001).
public sealed class Attendance : BaseEntity
{
    private Attendance() { }

    public Guid UserId { get; private set; }            // CommitteeMember.PublicId
    public string Name { get; private set; } = string.Empty; // display snapshot
    public AttendanceRole Role { get; private set; }
    public AttendanceStatus Status { get; private set; }
    public bool IsVotingEligible { get; private set; }
    public DateTimeOffset? JoinedAt { get; private set; }
    public DateTimeOffset? LeftAt { get; private set; }

    internal Attendance(Guid userId, string name, AttendanceRole role, bool isVotingEligible)
    {
        if (userId == Guid.Empty) throw new InvalidOperationException("An attendee must reference a user.");
        UserId = userId;
        Name = name.Trim();
        Role = role;
        IsVotingEligible = isVotingEligible;
        Status = AttendanceStatus.Invited;
    }

    internal void Mark(AttendanceStatus status, DateTimeOffset now)
    {
        Status = status;
        if (status is AttendanceStatus.Present or AttendanceStatus.Late)
            JoinedAt ??= now;
    }
}
