# ADR-0010: Always-Attributed Voting Model

- Status: Accepted
- Date: 2026-06-24
- Deciders: Architecture Committee (secretary-confirmed)

## Context and Problem Statement

The committee makes decisions via voting followed by chairman final approval. The voting model must specify: who may vote (eligible voters), what constitutes a quorum, how abstentions are handled, whether votes are attributed to individuals or anonymous, and how the chairman's authority interacts with the vote outcome. Getting this wrong produces either accountability gaps (anonymous votes in a governance body) or a model too complex to implement correctly.

## Decision Drivers

- The current process uses voting + chairman final approval, where the chairman has stronger authority (can override a majority vote with justification, or give final approval to a conditional result).
- The committee is a named set of individuals (≤20 users); accountability for architecture decisions requires knowing who voted which way — anonymity is antithetical to governance accountability.
- Quorum and eligible-voter tracking are needed to determine whether a vote result is valid.
- Abstentions are distinct from absence: a present member may choose to abstain (no position); a member who did not vote is absent (missed the window).
- The voting model must be immutable once the vote is closed (ADR-0009).
- Conflict-of-interest handling (a voter recusing themselves) is a natural extension of the attribution model.

## Considered Options

1. **Always attributed; eligible voters list; quorum check; abstentions tracked; chairman approval/override by name** — full accountability, no anonymity.
2. **Secret ballot (anonymous voting)** — cannot attribute accountability to governance decisions in a named committee; governance best practice requires attribution; rejected for v1.
3. **Simple majority with no chairman override** — ignores the actual committee process where the chairman has authority beyond the vote count; not representative of reality.
4. **Ranked-choice or weighted voting** — no requirement for this level of complexity; adds implementation risk; rejected as out of scope.

## Decision Outcome

Chosen option: "Always attributed voting — eligible voters, quorum, abstentions, chairman approval/override by name", because accountability is the primary governance requirement: architectural decisions must be traceable to the individuals who made them, and the chairman's authority must be recorded explicitly (not inferred). In v1 there is no anonymity and no ranked-choice — these can be revisited with evidence of a requirement.

### Consequences

- Good: full attribution means the audit log records who voted which way; the chairman's approval or override is a named, timestamped, immutable event; quorum enforcement prevents invalid decisions; abstentions are treated as a deliberate position (not just absence); conflict-of-interest recusals are expressible.
- Bad / trade-off: some committee members may feel attribution inhibits honest voting (the "reluctant dissenter" problem). This is a governance culture decision, not a technical one — the brief mandates attribution, and it is the correct stance for an architecture committee. If anonymous voting is ever required (e.g., for sensitive personnel matters), it must be introduced as a separate vote type with explicit scope, not as a default.

## Validation

- Vote lifecycle test: configure an eligible-voter list; open a vote; cast attributed votes (including one abstention); close the vote; verify the `VoteRecord` is immutable (ADR-0009) and contains per-voter attribution.
- Quorum test: close a vote where fewer than the quorum threshold voted; verify the result is flagged as invalid pending chairman action.
- Chairman override test: cast a vote where the majority is `Rejected`; chairman issues an `ApproveWithOverride` event with a justification string; verify the event is recorded by name, the original vote result is preserved, and the decision outcome records both the vote result and the override.
- Abstention vs. absence distinction: verify that `Abstained` (present, no position) and `Absent` (did not vote before close) are stored as distinct states on the per-voter ballot record.

## Links / Notes

- Vote entity lifecycle: `Configured → Open → Closed → Ratified` (immutable after Close, per ADR-0009).
- Vote options per topic type: typically `{Approve, ConditionallyApprove, Reject, MoreInfoRequired, Defer}` — configured per vote instance, not hardcoded globally.
- Chairman override is not the same as the chairman's vote: the chairman may vote AND override (or refrain from voting and still issue an override). Both are attributed events.
- Conflict-of-interest: a voter may self-recuse; recusal is recorded as a distinct state (`Recused`) on the ballot record; recused voters are excluded from quorum calculation.
- Anonymous voting is explicitly deferred to a future ADR if a requirement emerges. Do not implement anonymity infrastructure in v1.
- Related: ADR-0004 (authenticated voter identity from Keycloak), ADR-0009 (immutable vote record), ADR-0005 (vote-open notification via in-app center).
