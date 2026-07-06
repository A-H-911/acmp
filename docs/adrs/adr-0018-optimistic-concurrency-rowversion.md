# ADR-0018: Optimistic Concurrency via RowVersion (implements docs/domain/data-architecture.md §1.5)

- Status: Accepted
- Date: 2026-06-30
- Deciders: Architecture Committee execution (operator-confirmed, 2026-06-30)
- Implements: `docs/domain/data-architecture.md` §1.5
- Resolves: OQ-043 (`docs/decisions/open-decision-register.md`)

## Context and Problem Statement

`docs/domain/data-architecture.md` §1.5 has always stated that every **mutable aggregate root** carries a `RowVersion ROWVERSION` column so EF Core optimistic concurrency turns a stale write into a `DbUpdateConcurrencyException`, which the API maps to **HTTP 409** (`docs/domain/architecture-detail.md` §7.4). The forensic rebuild review (rebuild-findings DI-02 / OQ-043) found the backstop **did not exist in code**: no `IsRowVersion()` / `[Timestamp]` / concurrency token on any entity, and no `RowVersion` column in any migration. Documentation and code disagreed; CLAUDE.md and guardrail #11 require we fix one or raise an ADR rather than let them drift. OQ-043 framed the choice: **(a)** implement the token to make the doc true, or **(b)** amend `docs/domain/data-architecture.md` to last-writer-wins.

## Decision Drivers

- The system of record for committee governance should not silently lose an update when two users edit the same aggregate; surfacing a 409 ("reload and retry") is the auditable behavior `docs/domain/data-architecture.md`/`docs/domain/architecture-detail.md` already specify.
- `docs/domain/data-architecture.md` was authored in detail assuming optimistic concurrency (the §1.5 paragraph, the §2 table conventions, and per-table `RowVersion` columns). Option (a) makes a coherent design true; option (b) would require scrubbing the claim from many places and weakening a settled model.
- The cost of (a) is contained and idiomatic: EF Core's `rowversion`/`IsRowVersion()` is the standard SQL Server optimistic-concurrency mechanism — one property + one config line per root + a store-generated column.
- Right-sizing (guardrail #12): at ≤20 users contention is near-zero, so we add the token **only to mutable aggregate roots**, never to append-only tables (`AuditEvent`, `ProgressUpdate`) or child/owned entities.

## Considered Options

1. **Implement RowVersion + 409 mapping (chosen)** — add the token to the built mutable roots, configure EF, migrate, map `DbUpdateConcurrencyException → 409`, and test it.
2. **Last-writer-wins** — drop the rowversion claim from `docs/domain/data-architecture.md` §1.5 + the §2 conventions + every table row + `docs/domain/architecture-detail.md` §7.4, and accept silent overwrites at this scale.

## Decision Outcome

Chosen option: **(1) implement optimistic concurrency via RowVersion**, because it makes the existing data-architecture canon true, keeps governance writes auditable (no silent lost updates), and is a small, idiomatic EF Core change at this scale.

Applied in v1 to the **built** mutable aggregate roots: `Topic` (Topics), `Meeting` and `Agenda` (Meetings), and `CommitteeMember` (Membership). As further mutable roots from `docs/domain/data-architecture.md` are built (e.g. `MinutesOfMeeting`, `Document`, `Decision`, `Action`), each carries `RowVersion` at build time per `docs/domain/data-architecture.md` §1.5 — this ADR is the standing rule.

### Implementation

- **Domain:** a `public byte[] RowVersion` property on each mutable root.
- **EF config:** `b.Property(x => x.RowVersion).IsRowVersion();` in each root's `IEntityTypeConfiguration`.
- **Migrations:** one per affected module (Topics, Meetings, Membership) adding the `rowversion` column.
- **API:** `GlobalExceptionHandler` maps `DbUpdateConcurrencyException → 409` Problem Details ("The record was modified by another user; reload and try again.").

### Consequences

- Good: stale concurrent writes fail loud (409) instead of silently overwriting; `docs/domain/data-architecture.md` §1.5 and `docs/domain/architecture-detail.md` §7.4 are now true in code; the S5 concurrency invariant is testable.
- Good: append-only and child entities are deliberately excluded — no needless columns, no over-application (guardrail #12).
- Trade-off: a 409 must eventually be handled in the front end (refetch + merge or a "reloaded" prompt). v1 returns the correct status and message; the SPA's optimistic-conflict UX is a **follow-up front-end slice** and is not covered here.
- Trade-off: SQL Server `rowversion` is provider-specific; the in-memory/test path does not enforce it, so the concurrency behavior is verified against a real SQL Server (Testcontainers, per ADR-0016 §3).

## Validation

- A Testcontainers integration test loads an aggregate twice, saves the first edit, then saves the second (stale) edit and asserts a `DbUpdateConcurrencyException` is thrown (and, at the API layer, a 409 is returned).
- `dotnet build` + the full test suite pass; CI's ≥95% per-file coverage gate (ADR-0016 §1) covers the new 409 path.

## Links / Notes

- Implements `docs/domain/data-architecture.md` §1.5; status codes per `docs/domain/architecture-detail.md` §7.4.
- Resolves OQ-043 (`docs/decisions/open-decision-register.md`); recorded as R-27 in §A.
- Numbering note: this is ADR-0018. ADR-0017 (the React-19 amendment, renumbered from a colliding ADR-0015) lands in the same doc-integrity slice; the two do not collide.
