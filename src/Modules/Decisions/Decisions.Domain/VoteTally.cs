namespace Acmp.Modules.Decisions.Domain;

// Frozen tally computed at Close (docs/domain/domain-model.md §Vote, docs/domain/entity-lifecycles.md §4 "tally frozen"). Per-option cast counts +
// the abstain count + the total non-recused ballots that counted toward quorum. Immutable once set;
// serialized inline on the vote row (JSON) — a snapshot, never recomputed after Close (AC-025).
// NOTE: as a record over a Dictionary, the synthesized == uses REFERENCE equality for OptionCounts
// (Dictionary has no structural Equals); EF persistence is unaffected (VoteConfiguration uses a JSON
// ValueComparer). Do not rely on VoteTally value-equality in domain/test code.
public sealed record VoteTally(
    IReadOnlyDictionary<string, int> OptionCounts,
    int AbstainCount,
    int CastCount);
