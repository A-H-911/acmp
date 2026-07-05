namespace Acmp.Modules.Meetings.Domain.Enums;

// Source of a captured discussion note (docs/domain/domain-model.md §C Discussion, W9). v1 is Human-only; AI candidate
// extraction from transcripts (CandidateFromTranscript) is Phase 3 and requires human approval
// before it becomes part of the record (principle 5).
public enum DiscussionOrigin
{
    Human = 0,
    CandidateFromTranscript = 1,
}
