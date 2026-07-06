namespace Acmp.Modules.Meetings.Domain.Enums;

// Meeting type (docs/domain/entity-lifecycles.md §5). Regular = a session on the committee's normal cadence; Extraordinary = a
// session called outside the cadence for urgent matters. Additive domain modelling (no ADR required).
public enum MeetingType
{
    Regular = 0,
    Extraordinary = 1,
}
