---
status: Approved
version: 1.0.0
updated: 2026-07-06
owner: lead-secretary
---

# Follow-up Prompts — ACMP

> Copy-pasteable prompts for continuing the build after the [initial prompt](initial-prompt.md) orientation is approved. Each **assumes the agent has read** [`../../AGENTS.md`](../../AGENTS.md), [`../README.md`](../README.md), and the [invariant register](../requirements/invariant-register.md). They **reference** the authoritative artifacts rather than restating them. **Reminder:** ACMP is built through **P12 (MVP complete)** — PH-1 and most of PH-2 have shipped; the prompts below cover the remaining phase work and recurring situations.

## Standard footer (every phase prompt must satisfy)

Ship with unit + integration tests and satisfy the relevant `AC-###` in [`../validation/acceptance-criteria.md`](../validation/acceptance-criteria.md); enforce authorization ([`../requirements/invariant-register.md`](../requirements/invariant-register.md) INV-004) and emit `AuditEvent`s (INV-005); no hardcoded strings, EN+AR, verify RTL (INV-009); for any screen with a matching `.dc.html` in `/ACMP product context/`, match it exactly — tokens/components/states/iconography/RTL/AA — composing the shared design system and reconciling drift before "done"; where a screen has no reference, compose from the design system + the IA spec and flag it as a no-reference composition (INV-014); no secrets in source (INV-007); update the [progress log](../progress/progress-log.md) and acceptance audit; conventional commits on a `feat/P{n}-<slug>` branch off `main`, never direct to `main` (INV-013) — CI green locally before push, open a PR, monitor remote CI to green, then squash-merge and sync `main`; raise an ADR in [`../adrs/`](../adrs/) for any new architecture decision.

## Phase-gate prompts

### PH-1 — MVP Governance (shipped; use only for gap-fill)

PH-1 is **complete** (delivered incrementally through P12). Use this prompt only to close a verified PH-1 gap: pick the failing `AC-###` from the acceptance audit, confirm it against its Must-priority FR in [`../planning/roadmap.md`](../planning/roadmap.md) (PH-1 scope), reproduce the gap with a failing test, fix to green, and re-run the PH-1 exit checks (core loop E2E: login → topic → triage → agenda → meeting → vote → decision → action; audit row immutable; vote immutable after Ratified; RTL regression clean). Satisfy the standard footer.

### PH-2 — Governance Expansion (in progress; the current track)

Advance the PH-2 remainder in [`../planning/roadmap.md`](../planning/roadmap.md) (Should-priority FRs). ADRs+Invariants (P11), risks/dependencies + impact graph (P10), and reporting (P12) have shipped; the open items are the **Webex adapter** (notifications + recording links, behind `INotificationChannel`), the **Tarseem** diagram sidecar (behind `IDiagramRenderer`, JSON spec = source of truth), and **Knowledge/Research** (Research module works standalone before any Keystone import). Pick one bounded slice, confirm its FRs and `AC-###`, keep it strictly behind its adapter so v1 still runs without it, and satisfy the standard footer. Exit gate: Should FRs signed off, adapter stable, zero Sev-1.

### PH-3 — Research & Knowledge (not started)

Only after PH-2 exit is met. Build Could-priority FRs per [`../planning/roadmap.md`](../planning/roadmap.md): AI-assisted transcript extraction (**candidate-only, human-reviewed — INV-006 / OWASP LLM01**, off by default, Admin-activated), transcript FTS, the email channel via `INotificationChannel` (when SMTP available), the KPI dashboard, and the traceability-matrix export. No AI candidate enters a governance record without human approval. Satisfy the standard footer.

## Situational prompts

### Fresh-session refresher

New session, mid-phase. Re-read [`../../AGENTS.md`](../../AGENTS.md), the [status report](../progress/status-report.md), and the [progress log](../progress/progress-log.md); state the current phase, the branch you are on, and the single next work item before touching anything. Do not re-open settled decisions in [`../adrs/`](../adrs/).

### Invariant audit

Before opening a PR, self-audit the diff against every applicable `INV-###` in the [invariant register](../requirements/invariant-register.md) — see [`review-prompts.md`](review-prompts.md) for the checklist. Any violation stops the PR and requires a new ADR.

### Bug triage

Reproduce the reported symptom with a failing regression test first, then find the **root cause** (grep every caller of the function you are about to touch — fix it once where all callers route through, not per call site). Fix to green, confirm no `AC-###` regressed, satisfy the standard footer.

### Deviation → ADR

If a needed change conflicts with a settled decision or an `INV-###`, **stop**. Write a MADR file in [`../adrs/`](../adrs/) (lifecycle `Draft → Proposed → Approved`) stating the driver, the option chosen, and the consequence; get human approval before implementing. Never work around a guardrail silently or edit a superseded ADR.

### Status report

On request, refresh the [status report](../progress/status-report.md) and acceptance audit from the [progress log](../progress/progress-log.md): current phase, `AC-###` verdicts (Met/Partial/Not-met/Pending), open `OQ-###` in the [open-question register](../decisions/open-question-register.md), and open items in the [deferred-work register](../execution/deferred-work-register.md). Report facts, not claims — verify before asserting Met.
