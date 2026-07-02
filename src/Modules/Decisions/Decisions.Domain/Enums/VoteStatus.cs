namespace Acmp.Modules.Decisions.Domain.Enums;

// Vote state machine (docs/12 §4, ADR-0010). Configured → Open → Closed → Ratified. Strictly
// forward-only: after Closed the ballots + tally are frozen and immutable (AC-025); no path back to
// Open/Configured and no re-Ratify (AC-026). A mistaken vote is corrected by running a NEW vote.
public enum VoteStatus
{
    Configured = 0,
    Open = 1,
    Closed = 2,
    Ratified = 3,
}
