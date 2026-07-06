namespace Acmp.Modules.Meetings.Domain.Enums;

// Per-participant presence (docs/domain/domain-model.md §C Attendance, W8). "Apologies" in committee parlance = Excused.
// Present/Late count toward quorum; quorum itself is computed, never stored.
public enum AttendanceStatus
{
    Invited = 0,
    Present = 1,
    Absent = 2,
    Excused = 3,
    Late = 4,
}
