# ADR-0029: Read-only "Linked artifacts" card on the wiki reading view + make Document a pickable link target

- Status: Accepted
- Date: 2026-07-15
- Deciders: Architecture Committee execution (operator chose option C + ratified 2026-07-15)
- Context: P15 (Research & Knowledge) design-fidelity audit remediation

## Context and Problem Statement

The design's wiki reading view (`ACMP Research & Knowledge.dc.html`, lines 356-363) shows cross-links as a
compact **read-only "Linked artifacts" card** (rows of `[key-chip] [title] [chevron]`, each navigating to the
linked artifact). During P15e the reading view instead mounted the shared `TraceabilityPanel` — a documented,
intentional deviation (progress-log:156): the panel is richer (grouped edges) **and** it exposed an "Add
relationship" affordance.

The P15 fidelity audit flagged this as an INV-014 divergence (WK10). Force-matching the mockup means the reading
view becomes read-only. But there is a coupled constraint: the shared `ArtifactPicker` only lists
**Topic / Action / Risk** as pickable endpoints (the only types with a FE list source), so a `Document` was never
a selectable *target* from any other artifact's panel. The wiki reading-view panel was therefore the **only** UI
path to create a wiki cross-link, and it was narrow + one-directional (a wiki page could link *out* to a
Topic/Action/Risk; nothing could ever link *to* a wiki page). Simply removing the panel would drop that
capability entirely (edges only via API/seed) — which the operator rejected.

## Decision Drivers

- **INV-014** (design fidelity): the reading view has a matching `.dc.html`, so it must match it exactly.
- Settled decisions are FINAL — the blessed `TraceabilityPanel` substitution (progress-log:156) may only be
  reversed via a new ADR.
- The operator values **both** mockup fidelity (they commissioned the audit) **and** the linking capability
  (they rejected losing it) — so the solution must satisfy both.
- A wiki page is reference material that other artifacts *cite*; modelling links as "the citing artifact
  references the page" is the more natural direction than "the page links out".

## Considered Options

1. **Keep the `TraceabilityPanel`** on the wiki reading view (P15e status quo). Rejected — leaves the WK10
   INV-014 divergence open.
2. **Bespoke read-only card, drop wiki linking entirely** (the first draft of this ADR). Rejected by the
   operator — it makes wiki cross-linking impossible in the UI.
3. **Bespoke card + a manager-only "Add link" affordance on the card.** Rejected — puts a control on the exact
   surface just fidelity-fixed, a fresh mockup deviation.
4. **Bespoke read-only card + make `Document` a pickable relationship target (this ADR, operator's choice).**
   The wiki reading view matches the mockup exactly (read-only card), and a wiki page is linked **from the
   artifact that cites it** — `Document` is added to `ArtifactPicker` + the relationship dialog's pickable set,
   so any panel can create an edge to a wiki page. Chosen.

## Decision

Two coupled changes:

1. **Reading view** — replace `TraceabilityPanel` with a bespoke read-only `WikiLinkedArtifacts` card that
   matches the mockup. It reuses `useArtifactRelationships('Document', id)` (same edges the panel read), unions
   outgoing + incoming, routes each row through `hrefFor` (routable → `Link`, non-routable → plain row, never a
   dead link), and renders nothing when the document has no edges.
2. **Linking** — add `Document` to the shared `ArtifactPicker` (a `useWikiDocuments` list source, gated so it is
   fetched only when Document is offered) and to the **relationship** dialog's pickable set (`CreateRelationshipDialog`
   only — `Document` is not a `DependencyEndpointType`, so the dependency dialog must never offer it). A wiki page
   is now linked from any citing artifact's panel; the incoming edge then appears in the page's Linked-artifacts
   card automatically.

This **removes the wiki reading view's own "Add relationship" affordance** but replaces the lost capability with a
richer, bidirectional one: previously a wiki page could only link *out* to Topic/Action/Risk; now any artifact can
link *to* a wiki page (and the page shows it as an incoming link).

## Consequences

- **Positive:** the reading view matches the mockup (INV-014 closed for WK10); wiki linking is preserved and
  strengthened (bidirectional, driven from the citing artifact); no dead links; the shared `TraceabilityPanel`
  and its other consumers are untouched.
- **Negative / accepted:** the linking *direction* changes — you cite a wiki page **from** the referencing
  artifact's panel, not from the wiki page. Every relationship dialog now also fetches the wiki-documents list
  when Document is offered (negligible at ≤20 users; gated off for the dependency dialog).
- **Scope:** frontend only — no backend, schema, or API change. The design `.dc.html` already specifies the
  read-only card, so no design-update is owed for the reading-view cross-links.
- **Verification:** `WikiLinkedArtifacts.test.tsx` (empty → nothing; routable → link; non-routable → plain row);
  `WikiReadingView.test.tsx` (card focused on the document); `ArtifactPicker.test.tsx` (picks a Document target,
  resolving its localized title).
