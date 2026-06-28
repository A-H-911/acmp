namespace Acmp.Modules.Meetings.Domain.Enums;

// How the meeting is held (docs/12 §5). InPerson = on-site only; Hybrid = on-site + remote attendees;
// Remote = fully online. Additive domain modelling (no ADR required).
public enum MeetingMode
{
    InPerson = 0,
    Hybrid = 1,
    Remote = 2,
}
