namespace Acmp.Modules.Decisions.Domain.Enums;

// Committee decision outcomes (README §E). The full set the committee can record on a topic; the
// outcome is fixed at draft time and immutable once issued (AC-027) — a different outcome means a NEW
// decision that supersedes the prior one (W21), never an edit.
public enum DecisionOutcome
{
    Approved = 0,
    ConditionallyApproved = 1,
    Rejected = 2,
    MoreInfoRequired = 3,
    FeedbackProvided = 4,
    EnhancementsRequired = 5,
    DesignChangesRequired = 6,
    ResearchRequired = 7,
    Deferred = 8,
    Escalated = 9,
    Converted = 10,
}
