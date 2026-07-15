# ADR-0029: Bespoke read-only "Linked artifacts" card for the wiki reading view (reverses the P15e TraceabilityPanel substitution)

- Status: Proposed
- Date: 2026-07-15
- Deciders: Architecture Committee execution (operator-ratification pending)
- Context: P15 (Research & Knowledge) design-fidelity audit remediation

## Context and Problem Statement

The design's wiki reading view (`ACMP Research & Knowledge.dc.html`, lines 356-363) shows cross-links as a
compact **read-only "Linked artifacts" card**: a labelled card whose rows are `[key-chip] [title] [chevron]`,
each navigating to the linked artifact. During P15e the reading view instead mounted the shared
`TraceabilityPanel` — a documented, intentional deviation (progress-log:156): the panel is richer (grouped
up/down/related edges) **and it also creates edges** (an "Add relationship" affordance), reused across 8
consumers.

The P15 fidelity audit flagged this as an INV-014 divergence (WK10): the reading view does not match the
mockup's card. The remediation must decide whether to keep the blessed panel substitution or force-match the
mockup — the latter reverses a settled decision and drops a function, so per AGENTS.md it cannot be changed
silently and needs an ADR.

## Decision Drivers

- **INV-014** (design fidelity): the reading view has a matching `.dc.html`, so it must match it exactly.
- Settled decisions are FINAL — the blessed `TraceabilityPanel` substitution (progress-log:156) may only be
  reversed via a new ADR, not silently.
- The operator explicitly chose **force-match the mockup** for the shared-component/convention items in this
  remediation, accepting the functional trade-off.
- Data completeness must not regress: the card must show the same edges the panel showed.

## Considered Options

1. **Keep the `TraceabilityPanel`** in the reading view (the P15e status quo). Rejected — leaves the WK10
   INV-014 divergence open; the operator chose to force-match.
2. **Keep the panel but wrap it in the mockup's card chrome.** Rejected — the panel's grouped multi-section
   layout and "Add relationship" control cannot be made to read as the mockup's flat chip-row card without
   effectively rebuilding it.
3. **Replace the panel with a bespoke read-only card (this ADR).** A new `WikiLinkedArtifacts` component that
   **reuses the panel's relationship read-hook** (`useArtifactRelationships('Document', id)`), renders the
   flat chip-row card, links routable targets and shows non-routable ones as plain rows (no dead links), and
   is hidden when there are no links. Chosen.

## Decision

Replace `TraceabilityPanel` in `WikiReadingView` with a bespoke read-only `WikiLinkedArtifacts` card that
matches the mockup. It reads the same edges via `useArtifactRelationships('Document', id)` (so the linked set
is complete — no data gap), unions outgoing + incoming, routes each row through `hrefFor` (routable →
navigable `Link`, non-routable → plain row), and renders nothing when the document has no edges.

This **drops the wiki reading view's "Add relationship" affordance** — a deliberate functional regression.
Wiki edge *creation* now lives only where a routable detail page still exposes the `TraceabilityPanel`; the
wiki reading view becomes read-only for cross-links, consistent with the mockup. The shared `TraceabilityPanel`
and its other 7 consumers are untouched.

## Consequences

- **Positive:** the reading view matches the mockup (INV-014 closed for WK10); the linked set is identical to
  what the panel showed (same read-hook); no dead links.
- **Negative / accepted:** cross-links can no longer be *created* from the wiki reading view. For a reference
  wiki this is acceptable — pages are linked from the artifacts that reference them. If in-place wiki linking
  is later wanted, re-expose a create affordance without reverting the card.
- **Scope:** frontend only — no backend, schema, or API change. The design `.dc.html` already specifies this
  card, so no design-update is owed for the reading-view cross-links (the P15e substitution note is
  superseded by this ADR).
- **Verification:** `WikiLinkedArtifacts.test.tsx` (empty → renders nothing; routable → link; non-routable →
  plain row) + `WikiReadingView.test.tsx` (mounts the card focused on the document).
