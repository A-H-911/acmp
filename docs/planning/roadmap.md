---
status: Approved
version: 1.1.0
updated: 2026-07-06
owner: lead-secretary
---

# Roadmap — ACMP

Four delivery phases (`PH-0…PH-3`). Each phase states its goal, scope, deliverables, validation, top risks, and exit gate. Requirement coverage per phase is in the [traceability matrix](../validation/traceability-matrix.md); acceptance gates are in [validation/acceptance-criteria.md](../validation/acceptance-criteria.md). **Build status (2026-07-06): PH-0 and PH-1 complete; PH-1's expansion slices through P12 (Dashboards & Reports) have shipped. Next: PH-2 remainder / hardening.** Execution-level tracking uses the [build-slice ladder (P-series)](#build-slice-ladder-p-series) below. Per-phase "Recommended team" bullets are the original delivery-team reference from the pre-migration roadmap; current delivery is a solo operator + Claude Code.

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
- **Recommended team (reference).** 1 Tech Lead / Architect (spikes + planning review); 1 Secretary (stakeholder liaison, glossary sessions); part-time Security reviewer (Keycloak/TLS spike review); part-time UX (bilingual glossary, RTL terminology validation).
- **Exit gate.** All PH-1-blocking `OQ-` resolved; branch strategy agreed; environments accessible. **Status: complete.**

## PH-1 — MVP Governance

- **Goal.** Deliver the complete architecture-governance loop: topic intake → backlog → agenda → meeting → voting → decision → action, with Keycloak identity (RBAC + per-topic ABAC), in-app notifications, full EN/AR + RTL, light/dark, append-only hash-chained audit, basic dashboards, FTS, and a typed traceability panel; plus risk creation and basic dependency recording.
- **Scope.** All Must-priority FRs across Platform (FR-001–015), Membership (FR-016–024), Topics (FR-025–044), Meetings (FR-046–061), Decisions (FR-062–069), Voting (FR-070–078), Actions (FR-079–088), Risks (FR-089–093), Dependencies (FR-094–098), Notifications (FR-129–132), Reporting (FR-135–137), Search & Traceability (FR-143–147), Audit & Records (FR-150–153). Excludes ADRs/Invariants/Research/Knowledge/Diagrams, Guest access, Webex retrieval, notification prefs, per-stream reporting, Gantt view.
- **Deliverables.** Running containerized app covering PH-1 scope; EF Core migrations; OpenAPI; unit + integration + E2E smoke suites (login → topic → triage → agenda → meeting → vote → decision → action); EN+AR UI with 100% RTL; staging pilot.
- **Validation.** All Must FRs pass their `AC-###`; E2E loop completed by the Secretary in staging; audit row immutable (direct UPDATE fails); vote immutable after Ratified; RTL visual regression clean; health checks 200; Hangfire jobs fire.
- **Top risks.** RTL effort underestimated (RISK-002); committee adoption (RISK-007); scope creep to generic PM (RISK-006).
- **Recommended team (reference).** 1 Tech Lead / Senior .NET Architect; 2 .NET Backend Engineers; 1–2 React/TypeScript Frontend Engineers (one with RTL/i18n experience); 1 QA Engineer (manual + automated E2E); part-time Security Reviewer (auth flows, audit-immutability review); part-time UX Designer (bilingual wireframe review, RTL validation); Product Owner: Secretary (domain authority).
- **Exit gate.** All Must FRs implemented, tested, signed off; zero Sev-1; one full meeting cycle on the platform; staging stable ≥5 business days. **Status: complete (delivered incrementally through P12; MVP live).**

## PH-2 — Governance Expansion

- **Entry gate.** PH-1 exit met.
- **Goal.** Full governance maturity: ADRs + Architecture Invariants, Research/Keystone import, Tarseem diagram sidecar + dependency-graph visualization, Templates + Knowledge wiki, the Webex adapter (notifications + recording/transcript), advanced traceability (impact analysis), expanded reporting, notification preferences + digests, Guest/Presenter access, CoI tracking, Gantt view, audit export + retention config.
- **Scope.** Should-priority FRs: Membership FR-023; Topics FR-030/036/041/045; Meetings FR-057–058; Decisions FR-068/076; Governance ADRs FR-099–105 + Invariants FR-106–109; Research FR-111–115; Knowledge FR-116–120; Diagrams FR-121–128; Dependencies FR-096–097; Notifications FR-133–134; Reporting FR-138–142; Search FR-148; Audit FR-154–155; Risks FR-092.
- **Deliverables.** Tarseem sidecar in Compose; ADR repository + Invariant registry; Research import; wiki + templates; dependency-graph visualization; expanded tests; updated OpenAPI.
- **Validation.** Should FRs pass their `AC-###`; Tarseem renders flowchart/C4/dependency and exports SVG/PNG/draw.io; ADR promoted from a decision and linked; Webex Adaptive Card delivered to a test space; impact analysis A→B→C returns B and C.
- **Top risks.** Tarseem early maturity (RISK-005); Webex Assistant not enabled (RISK-008); impact-traversal performance.
- **Invariants still in force.** All `INV-001…014`.
- **Recommended team (reference).** 1 Tech Lead; 2 .NET Backend Engineers (may overlap with PH-1 team); 1–2 React/TypeScript Frontend Engineers; 1 QA Engineer; part-time Security Reviewer (Webex OAuth, transcript data handling, OWASP ASVS 5.0 L2 re-check); part-time UX Designer (diagram authoring UX, wiki editor, Webex card design).
- **Exit gate.** Should FRs implemented + signed off; Tarseem sidecar stable 48h; zero Sev-1. **Status: substantially delivered (P11 ADRs/Invariants, P10 risks/deps/traceability + impact graph, P12 reporting shipped); Webex/Tarseem/Knowledge remainder = Phase-2 backlog.**

## PH-3 — Research & Knowledge

- **Entry gate.** PH-2 exit met.
- **Goal.** AI-assisted transcript extraction (candidate-only, human-reviewed — OWASP LLM01), transcript FTS, email channel (when SMTP available), KPI/health dashboard, traceability-matrix export, invariant exception workflow, bulk topic ops, advanced analytics.
- **Scope.** Could-priority FRs: Meetings FR-059–060; Invariants FR-110; Notifications email items; Reporting FR-141; Search FR-149; Topics bulk ops; advanced audit export.
- **Deliverables.** AI extraction (off by default, Admin-activated, human-gate in code); email adapter via `INotificationChannel`; KPI dashboard; traceability-matrix CSV export; invariant exception workflow.
- **Validation.** No AI candidate enters the record without Secretary approval; email dispatched via SMTP without call-site change; KPI dashboard renders with historical data; traceability CSV correct.
- **Top risks.** AI extraction privacy (RISK-012 — mandatory human gate, on-prem/residency-compliant LLM, off by default); SMTP procurement; KPI baseline undefined.
- **Invariants still in force.** All `INV-001…014` (esp. INV-006 AI candidate-only).
- **Recommended team (reference).** 1 Tech Lead (part-time; platform stabilized); 1 .NET Backend Engineer; 1 React/TypeScript Frontend Engineer; 1 QA Engineer; part-time Security Reviewer (AI/LLM data flow, OWASP LLM01); part-time UX (KPI dashboard design, AI extraction review UX).
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

## Build-slice ladder (P-series)

The `PH-` phases above are planning-level. Execution is tracked in finer **build slices** (`P1…P19`) — the scheme used by the [progress log](../progress/progress-log.md), the [status report](../progress/status-report.md), branch names (`feat/P{n}-<slug>`), and the per-slice prompts in [handoff/follow-up-prompts.md](../handoff/follow-up-prompts.md). This table is the canonical ladder definition, restored after the Keystone migration retired the pre-migration `execution-handoff/phase-prompts.md` that previously defined it. (The pre-migration copy of that file had itself been truncated by an editing accident in commit `16e0577` — P18 cut mid-sentence, P19 lost; both are restored here from the original baseline `c487448`.)

### Shipped (P1–P12 — MVP complete)

| Slice | Theme | Key merges |
|---|---|---|
| P1 | Repository initialization — solution, Docker stack (api/web/keycloak/sqlserver/seq/minio), CI skeleton | pre-PR bootstrap commits (`2498047`, `ea97dcf`) |
| P2 | Backend foundation — modular-monolith host + Membership reference-module pattern | pre-PR bootstrap commits (`555a363`, `da55081`) |
| P3 | Frontend foundation — app shell, design tokens, i18n EN/AR + RTL, role-filtered nav | `5fae8a8`; refreshes PR #9, #25, #51 |
| P4 | Identity & permissions — claim→role mapping, policy + ABAC authz, SoD, Users & Membership UI | `05c3f04`; PR #3, #19, #26, #52 |
| P5 | Topics & backlog — intake → triage → backlog, views, DnD prioritization | PR #10–#18, #21, #27 |
| P6 | Agenda & meetings + in-app notifications | PR #22, #28–#37, #53, #54 |
| P7 | Minutes & decisions | PR #58–#62 |
| P8 | Actions & follow-up (incl. Hangfire reminders, Job Monitor, decision-link gate) | PR #63–#73 |
| P9 | Voting (incl. the P1–P9 audit remediation + hash-chained audit store) | PR #74–#76 |
| P10 | Risks, dependencies & traceability (incl. impact graph, risk/dep reports) | PR #77–#86 |
| P11 | ADRs & invariants (Governance module, decision→ADR promotion) | PR #87–#92 |
| P12 | Dashboards & reports | PR #93–#96 |

### Remaining (P13–P19)

Per-slice prompts live in [handoff/follow-up-prompts.md](../handoff/follow-up-prompts.md).

| Slice | Theme | Phase home |
|---|---|---|
| P13 | Webex adapter — notifications + recording links, strictly behind `INotificationChannel` (D-02) | PH-2 remainder |
| P14 | Tarseem diagram sidecar behind `IDiagramRenderer` + the Diagrams surface (D-11) | PH-2 remainder |
| P15 | Research & Knowledge — standalone Research module + Knowledge wiki/templates; Keystone import optional (D-05) | PH-2 remainder |
| P16 | Security hardening — OWASP ASVS 5.0 L2 sweep, incl. per-ballot crypto chaining (D-13) | cross-cutting |
| P17 | Testing hardening — first task: harvest the "→ P17" deferrals from the progress log; partially advanced by the S1–S7 coverage/E2E slices (PR #38–#48, ADR-0016) | cross-cutting |
| P18 | Deployment — production compose + overrides, migration-on-deploy, nightly backups + warm-standby restore, deployment/rollback runbook | cross-cutting |
| P19 | Final audit & release readiness — [checkpoints](../execution/checkpoints.md) `[BLOCK]` gates + [Definition of Done](../execution/definition-of-done.md); final acceptance-audit report | cross-cutting |

### Legacy token map

Three other "P-number" schemes appear in project history; none of them is this ladder:

| Token (where seen) | Meaning |
|---|---|
| `P1–P4` in the [backlog](../execution/backlog.md) Priority column | Priority codes (blocker / core loop / workflow / enhancement) — not build slices. |
| Phase numbers inside `ACMP product context/ACMP Usage Map.dc.html` (e.g. its "P12" = Research & Knowledge, its "P15" = Administration remainder) | The design package's own internal scheme — design reference only; never renumber the Usage Map (INV-014). Round-2 progress-log entries citing "P14 = health + jobs; P15 = templates + streams + roles + notif" use this scheme. |
| "→ P14" in early progress-log entries (audit-hardening era) | Audit hash-chain work — shipped in the P9-review slice (PR #76); the one open residual, per-ballot crypto chaining, is tracked as D-13 and routed to P16. |

## Critical-review note — reordering vs. the brief

The original brief described phases roughly as: MVP (full governance loop) → Expansion (diagrams, ADRs, research) → Advanced (AI, analytics). This roadmap makes four adjustments:

1. **PH-0 added (Discovery & Validation).** The brief had no explicit discovery phase, but several analysis workstreams were already underway (Tarseem/Keystone repo inspection 2026-06-24, Webex feasibility, open-source landscape). A formal phase let the spikes produce signed-off answers (Keycloak claim mapping, MinIO provisioning, Webex licensing) before coding; its gate was written permission/access confirmation, not calendar time.
2. **Voting domain and full voting UI both in PH-1.** The core committee loop is meaningless without a vote that produces a decision. The brief hinted at separating voting into Phase 2; this roadmap keeps full voting (FR-070 through FR-078) in PH-1 — deferring it would leave PH-1 without a complete governance loop.
3. **Risk and Dependency domain split across PH-1/PH-2.** Risk record creation (FR-089–FR-091, FR-093) and basic dependency recording with cross-stream flagging (FR-094, FR-095, FR-098) sit in PH-1 because actions and decisions reference them; graph visualization, impact analysis, and the Tarseem-rendered dependency diagram moved to PH-2.
4. **Tarseem sidecar deferred to PH-2.** Tarseem (v1.0.0, released 2026-06-17) is early; using it only as a render sidecar from PH-2 limits blast radius. JSON-spec authoring and the render pipeline are PH-2 scope.
