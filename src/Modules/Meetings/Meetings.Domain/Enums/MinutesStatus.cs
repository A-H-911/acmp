namespace Acmp.Modules.Meetings.Domain.Enums;

// MinutesOfMeeting state machine (docs/12 §6, W10). Draft → InReview → Approved → Published; a published
// version is superseded by a NEW version, never edited (AC-036, ADR-0009). InReview → Draft on a
// change-request (AC-037). The 5 states are the operator-locked model; the reference design's 3-toggle
// (draft/review/published) is reconciled as a blessed deviation (Approved is a distinct persisted state
// between review and publish so the SoD-2 approval act and the publish/notify act are separable).
public enum MinutesStatus
{
    Draft = 0,
    InReview = 1,
    Approved = 2,
    Published = 3,
    Superseded = 4,
}
