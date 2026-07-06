---
status: Approved
version: 1.0.0
updated: 2026-07-06
owner: lead-secretary
---

# Roadmap — ACMP

Four delivery phases (`PH-0…PH-3`). Each phase states its goal, scope, deliverables, validation, top risks, and exit gate. Requirement coverage per phase is in the [traceability matrix](../validation/traceability-matrix.md); acceptance gates are in [validation/acceptance-criteria.md](../validation/acceptance-criteria.md). **Build status (2026-07-06): PH-0 and PH-1 complete; PH-1's expansion slices through P12 (Dashboards & Reports) have shipped. Next: PH-2 remainder / hardening.**

| Phase | Theme | Size | Primary value |
|---|---|---|---|
| PH-0 | Discovery & Validation | S | Eliminate blockers before code |
| PH-1 | MVP Governance | XL | Complete governance loop live |
| PH-2 | Governance Expansion | XL | Full governance maturity |
| PH-3 | Research & Knowledge | L | Intelligence & advanced analytics |

## PH-0 — Discovery & Validation

- **Goal.** Turn the analysis workstreams (Keycloak claim mapping, MinIO/SQL provisioning, Webex licensing, Tarseem `doctor`, Arabic-FTS quality) into signed-off answers before coding; complete the planning package.
- **Scope.** Spikes: Keycloak claim mapping, MinIO S3 + `IFileStore` stub, Tarseem container build, SQL Server Arabic FTS (20 sample queries); bilingual glossary review; repository skeleton + CI stub. No feature code.
- **Deliverables.** Pass/Fail/Conditional spike reports; signed bilingual glossary; complete planning package; repo skeleton with `CLAUDE.md`.
- **Validation.** Every PH-1-blocking `OQ-` resolved or defaulted; the planning package passes the Keystone validator gates (G-IDS, G-DEC-STATUS, G-REQ-SRC, G-COMPLETE).
- **Top risks.** Keycloak claim names unknown (RISK-001 — resolved by ADR-0015 self-host); Arabic FTS inadequate (RISK-010 — Meilisearch fallback in PH-2).
- **Exit gate.** All PH-1-blocking `OQ-` resolved; branch strategy agreed; environments accessible. **Status: complete.**

## PH-1 — MVP Governance

- **Goal.** Deliver the complete architecture-governance loop: topic intake → backlog → agenda → meeting → voting → decision → action, with Keycloak identity (RBAC + per-topic ABAC), in-app notifications, full EN/AR + RTL, light/dark, append-only hash-chained audit, basic dashboards, FTS, and a typed traceability panel; plus risk creation and basic dependency recording.
- **Scope.** All Must-priority FRs across Platform (FR-001–015), Membership (FR-016–024), Topics (FR-025–044), Meetings (FR-046–061), Decisions (FR-062–069), Voting (FR-070–078), Actions (FR-079–088), Risks (FR-089–093), Dependencies (FR-094–098), Notifications (FR-129–132), Reporting (FR-135–137), Search & Traceability (FR-143–147), Audit & Records (FR-150–153). Excludes ADRs/Invariants/Research/Knowledge/Diagrams, Guest access, Webex retrieval, notification prefs, per-stream reporting, Gantt view.
- **Deliverables.** Running containerized app covering PH-1 scope; EF Core migrations; OpenAPI; unit + integration + E2E smoke suites (login → topic → triage → agenda → meeting → vote → decision → action); EN+AR UI with 100% RTL; staging pilot.
- **Validation.** All Must FRs pass their `AC-###`; E2E loop completed by the Secretary in staging; audit row immutable (direct UPDATE fails); vote immutable after Ratified; RTL visual regression clean; health checks 200; Hangfire jobs fire.
- **Top risks.** RTL effort underestimated (RISK-002); committee adoption (RISK-007); scope creep to generic PM (RISK-006).
- **Exit gate.** All Must FRs implemented, tested, signed off; zero Sev-1; one full meeting cycle on the platform; staging stable ≥5 business days. **Status: complete (delivered incrementally through P12; MVP live).**

## PH-2 — Governance Expansion

- **Entry gate.** PH-1 exit met.
- **Goal.** Full governance maturity: ADRs + Architecture Invariants, Research/Keystone import, Tarseem diagram sidecar + dependency-graph visualization, Templates + Knowledge wiki, the Webex adapter (notifications + recording/transcript), advanced traceability (impact analysis), expanded reporting, notification preferences + digests, Guest/Presenter access, CoI tracking, Gantt view, audit export + retention config.
- **Scope.** Should-priority FRs: Membership FR-023; Topics FR-030/036/041/045; Meetings FR-057–058; Decisions FR-068/076; Governance ADRs FR-099–105 + Invariants FR-106–109; Research FR-111–115; Knowledge FR-116–120; Diagrams FR-121–128; Dependencies FR-096–097; Notifications FR-133–134; Reporting FR-138–142; Search FR-148; Audit FR-154–155; Risks FR-092.
- **Deliverables.** Tarseem sidecar in Compose; ADR repository + Invariant registry; Research import; wiki + templates; dependency-graph visualization; expanded tests; updated OpenAPI.
- **Validation.** Should FRs pass their `AC-###`; Tarseem renders flowchart/C4/dependency and exports SVG/PNG/draw.io; ADR promoted from a decision and linked; Webex Adaptive Card delivered to a test space; impact analysis A→B→C returns B and C.
- **Top risks.** Tarseem early maturity (RISK-005); Webex Assistant not enabled (RISK-008); impact-traversal performance.
- **Invariants still in force.** All `INV-001…014`.
- **Exit gate.** Should FRs implemented + signed off; Tarseem sidecar stable 48h; zero Sev-1. **Status: substantially delivered (P11 ADRs/Invariants, P10 risks/deps/traceability + impact graph, P12 reporting shipped); Webex/Tarseem/Knowledge remainder = Phase-2 backlog.**

## PH-3 — Research & Knowledge

- **Entry gate.** PH-2 exit met.
- **Goal.** AI-assisted transcript extraction (candidate-only, human-reviewed — OWASP LLM01), transcript FTS, email channel (when SMTP available), KPI/health dashboard, traceability-matrix export, invariant exception workflow, bulk topic ops, advanced analytics.
- **Scope.** Could-priority FRs: Meetings FR-059–060; Invariants FR-110; Notifications email items; Reporting FR-141; Search FR-149; Topics bulk ops; advanced audit export.
- **Deliverables.** AI extraction (off by default, Admin-activated, human-gate in code); email adapter via `INotificationChannel`; KPI dashboard; traceability-matrix CSV export; invariant exception workflow.
- **Validation.** No AI candidate enters the record without Secretary approval; email dispatched via SMTP without call-site change; KPI dashboard renders with historical data; traceability CSV correct.
- **Top risks.** AI extraction privacy (RISK-012 — mandatory human gate, on-prem/residency-compliant LLM, off by default); SMTP procurement; KPI baseline undefined.
- **Invariants still in force.** All `INV-001…014` (esp. INV-006 AI candidate-only).
- **Exit gate.** Committed Could FRs implemented + tested; AI off by default; privacy review signed off. **Status: not started (Phase 3).**

## Milestones (MS-)

| ID | Milestone | Phase | Gate |
|---|---|---|---|
| MS-001 | Discovery spikes signed off; planning package validator-green | PH-0 | PH-0 exit |
| MS-002 | Governance loop live end-to-end in staging (topic→decision→action) | PH-1 | PH-1 exit |
| MS-003 | Committee completes one full meeting cycle on the platform | PH-1 | PH-1 exit |
| MS-004 | ADRs + Invariants + traceability graph + reporting shipped | PH-2 | PH-2 partial |
| MS-005 | Tarseem sidecar + Webex adapter + Knowledge/Research operational | PH-2 | PH-2 exit |
| MS-006 | AI extraction (human-gated) + email channel + KPI dashboard | PH-3 | PH-3 exit |
