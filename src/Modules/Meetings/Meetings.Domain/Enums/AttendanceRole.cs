namespace Acmp.Modules.Meetings.Domain.Enums;

// The capacity a participant attends in (docs/11 §C Attendance). A per-meeting snapshot — Meetings
// never reads Membership tables; the role is supplied when the roster is seeded (ADR-0001).
public enum AttendanceRole
{
    Chair = 0,
    Secretary = 1,
    Member = 2,
    Reviewer = 3,
    Presenter = 4,
    Guest = 5,
}
