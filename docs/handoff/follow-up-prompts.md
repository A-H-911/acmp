---
status: Approved
version: 1.1.0
updated: 2026-07-06
owner: lead-secretary
---

# Follow-up Prompts — ACMP

> Copy-pasteable prompts for continuing the build after the [initial prompt](initial-prompt.md) orientation is approved. Each **assumes the agent has read** [`../../AGENTS.md`](../../AGENTS.md), [`../README.md`](../README.md), and the [invariant register](../requirements/invariant-register.md). They **reference** the authoritative artifacts rather than restating them. **Reminder:** ACMP is built through **P12 (MVP complete)** — PH-1 and most of PH-2 have shipped; the prompts below cover the remaining build slices (`P13…P19` per the [build-slice ladder](../planning/roadmap.md#build-slice-ladder-p-series)) and recurring situations.

## Standard footer (every phase prompt must satisfy)

Ship with unit + integration tests and satisfy the relevant `AC-###` in [`../validation/acceptance-criteria.md`](../validation/acceptance-criteria.md); enforce authorization ([`../requirements/invariant-register.md`](../requirements/invariant-register.md) INV-004) and emit `AuditEvent`s (INV-005); no hardcoded strings, EN+AR, verify RTL (INV-009); for any screen with a matching `.dc.html` in `/ACMP product context/`, **read the `.dc.html` directly with the file tools — NOT via the design MCP** — and match it exactly — tokens/components/states/iconography/RTL/AA — composing the shared design system and reconciling drift before "done"; the [Usage Map](../../ACMP%20product%20context/ACMP%20Usage%20Map.dc.html) is the authoritative per-screen index — consult it before each slice; where a screen has no reference, compose from the design system + the IA spec and flag it as a no-reference composition (INV-014); no secrets in source (INV-007); update the [progress log](../progress/progress-log.md) and acceptance audit; conventional commits on a `feat/P{n}-<slug>` branch off `main`, never direct to `main` (INV-013) — CI green locally before push, open a PR, monitor remote CI to green, then squash-merge and sync `main`; raise an ADR in [`../adrs/`](../adrs/) for any new architecture decision.

## Phase-gate prompts

### PH-1 — MVP Governance (shipped; use only for gap-fill)

PH-1 is **complete** (delivered incrementally through P12). Use this prompt only to close a verified PH-1 gap: pick the failing `AC-###` from the acceptance audit, confirm it against its Must-priority FR in [`../planning/roadmap.md`](../planning/roadmap.md) (PH-1 scope), reproduce the gap with a failing test, fix to green, and re-run the PH-1 exit checks (core loop E2E: login → topic → triage → agenda → meeting → vote → decision → action; audit row immutable; vote immutable after Ratified; RTL regression clean). Satisfy the standard footer.

### PH-2 — Governance Expansion (in progress; the current track)

Advance the PH-2 remainder in [`../planning/roadmap.md`](../planning/roadmap.md) (Should-priority FRs). ADRs+Invariants (P11), risks/dependencies + impact graph (P10), and reporting (P12) have shipped; the open items are the per-slice prompts **P13–P15** below. Pick one bounded slice, confirm its FRs and `AC-###`, keep it strictly behind its adapter so v1 still runs without it, and satisfy the standard footer. Exit gate: Should FRs signed off, adapter stable, zero Sev-1.

#### P13 — Webex integration

**Design (visual source of truth):** none — integration slice; any settings surface composes from the shared design system + the IA spec (no-reference composition, INV-014).

Implement the **Webex adapter** behind `INotificationChannel` + a meeting-metadata client, per [`../domain/webex-feasibility.md`](../domain/webex-feasibility.md), [`../domain/notification-strategy.md`](../domain/notification-strategy.md) + ADR-0005: bot + **Adaptive Cards v1.3** notifications (**≤80KB per card, ≤10 image links per card**), meeting metadata + recording links (advances D-02), webhook for recording-ready, OAuth/bot token (scoped), webhook signature verification, **429 + Retry-After** backoff via Hangfire. **Do not** assume programmatic Webex Assistant transcripts. Keep Webex strictly behind the adapter — v1 must run without it. If the environment is air-gapped, build the adapter but don't deploy a live connection. Satisfy the standard footer.

#### P14 — Tarseem integration

**Design (visual source of truth):** the **Diagrams** surface in `/ACMP product context/ACMP Diagrams.dc.html` (net-new wiring — this reference existed but was never mapped to a slice; consult the Usage Map row for scope).

Integrate **Tarseem** behind `IDiagramRenderer` per [`../domain/tarseem-analysis.md`](../domain/tarseem-analysis.md) + ADR-0006 (closes D-11): run Tarseem as a containerized render sidecar (thin HTTP wrapper around `tarseem generate`) or a Hangfire worker invoking the CLI; **store the JSON spec as the version-controlled source of truth** (`Diagram.Spec`, `SpecHash`); store artifacts (SVG/PNG/PDF/drawio/pptx) in MinIO; surface the capability report; self-repair on the coded error contract; attach diagrams to topics/ADRs/decisions via `Relationship`. **Do not build a diagram engine** (OOS-02). Handle Tarseem-unavailable gracefully. Satisfy the standard footer.

#### P15 — Research & Knowledge

**Design (visual source of truth):** `/ACMP product context/ACMP Research & Knowledge.dc.html`.

Build the **Research module standalone first** (manual entry: ResearchMission/Finding/Recommendation + links) and the **Knowledge wiki + Templates** per the PH-2 scope in [`../planning/roadmap.md`](../planning/roadmap.md). Only then, optionally, implement the Keystone import per [`../domain/keystone-analysis.md`](../domain/keystone-analysis.md) + ADR-0007 (D-05): an `IResearchImporter` that ingests a Keystone package's structured artifacts (manifest, requirements, decisions, risks, acceptance criteria, traceability) and maps them to ResearchMission/Finding/Recommendation + links. Keystone is never embedded or a hard dependency. Satisfy the standard footer.

### PH-3 — Research & Knowledge (not started)

Only after PH-2 exit is met. Build Could-priority FRs per [`../planning/roadmap.md`](../planning/roadmap.md): AI-assisted transcript extraction (**candidate-only, human-reviewed — INV-006 / OWASP LLM01**, off by default, Admin-activated), transcript FTS, the email channel via `INotificationChannel` (when SMTP available — D-12), the KPI dashboard, and the traceability-matrix export. No AI candidate enters a governance record without human approval. Satisfy the standard footer.

## Cross-cutting hardening slices (P16–P19)

Not phase-bound — run before/alongside release. Sequence per the [build-slice ladder](../planning/roadmap.md#build-slice-ladder-p-series). None of these has a design surface; any incidental UI composes from the shared design system + the IA spec (no-reference composition, INV-014).

### P16 — Security hardening

Apply the controls in [`../domain/security-controls.md`](../domain/security-controls.md) to OWASP ASVS 5.0 L2: finalize authz + SoD, session/MFA at Keycloak, input validation/output encoding, file-upload + malware scanning (ClamAV sidecar — open `OQ`), encryption in transit (TLS everywhere) + at rest (SQL TDE, MinIO SSE), secret management, audit immutability + hash-chain verification (**incl. per-ballot crypto chaining, D-13**), insider-risk controls, notification security, strict CSP, container hardening (non-root, read-only FS), and dependency/secret/image scanning + SBOM. Map each control to a threat in [`../domain/security-threat-model.md`](../domain/security-threat-model.md). Run the security test suite. Satisfy the standard footer.

### P17 — Testing

**First task: harvest the "→ P17" deferrals recorded throughout the [progress log](../progress/progress-log.md)** (indexed in the [deferred-work register](../execution/deferred-work-register.md) §Test-hardening deferrals); the S1–S7 coverage/E2E slices (PR #38–#48, ADR-0016) already advanced part of this scope. Bring the full suite to the targets in [`../validation/test-strategy.md`](../validation/test-strategy.md): coverage thresholds, permission-matrix, workflow, audit-trail, voting-integrity, decision-history, localization/RTL, accessibility (axe), migration, backup/restore, and mocked integration contracts. Wire all gates into CI per [`../domain/devsecops-plan.md`](../domain/devsecops-plan.md). Ensure the [acceptance audit](../validation/acceptance-audit.md) maps every `AC-###` to a passing test. Satisfy the standard footer.

### P18 — Deployment

Finalize per [`../domain/deployment.md`](../domain/deployment.md): multi-stage Dockerfiles, production `docker-compose` (+ overrides), externalized/secret config, EF migration-on-deploy strategy, nightly SQL + MinIO backup, **warm standby** restore-and-promote for 99.9%, health/readiness wiring, and the deployment + rollback **runbook**. Validate a clean `docker compose up` on a fresh VM and a tested restore. Satisfy the standard footer.

### P19 — Final audit & release readiness

Run the release gates in [`../execution/checkpoints.md`](../execution/checkpoints.md) and the full [Definition of Done](../execution/definition-of-done.md): all phase acceptance + exit criteria met; security scans clean; backup/restore tested; EN/AR + RTL complete; WCAG 2.2 AA; observability + alerts live; runbook + rollback ready; secretary/chairman UAT + security sign-off. Produce a final acceptance-audit report (every `AC-###` → verdict) and a release-notes summary. **Nothing ships with open `[BLOCK]` items.** Satisfy the standard footer.

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
