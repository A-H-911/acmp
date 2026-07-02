namespace Acmp.Modules.Decisions.Domain;

// The two quorum thresholds on a Vote (docs/11 §Vote, ADR-0010). MinPresent is checked at Open (eligible
// voters PRESENT in the linked meeting ≥ MinPresent — live attendance-linked, resolved via the Meetings
// seam). MinCast is checked at Close (non-recused ballots cast ≥ MinCast — AC-024). Owned value object,
// stored inline on the vote row (mirrors how LocalizedString is owned).
public sealed record QuorumRule(int MinPresent, int MinCast);
