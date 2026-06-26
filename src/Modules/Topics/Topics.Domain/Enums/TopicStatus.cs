namespace Acmp.Modules.Topics.Domain.Enums;

// Canonical Topic status model (README §E, docs/12 §1). The single state machine; "TopicRequest" is the
// pre-Accepted projection (Draft/Submitted/Triage), not a separate entity (docs/11 §A.2). Backlog views
// (kanban buckets, status chips) group these into presentation buckets — that grouping lives in the read
// model, never here. Field edits are blocked once Decided; Converted/Closed/Rejected are terminal exits.
public enum TopicStatus
{
    Draft = 0,
    Submitted = 1,
    Triage = 2,
    Accepted = 3,
    Prepared = 4,
    Scheduled = 5,
    InCommittee = 6,
    Decided = 7,
    Closed = 8,
    Rejected = 9,
    Deferred = 10,
    Reopened = 11,
    Converted = 12,
}
