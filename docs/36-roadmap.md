# 36 — Phased Roadmap

**Purpose:** Defines the four delivery phases for ACMP, their objectives, scope, dependencies, deliverables, risks, acceptance criteria, exit criteria, and recommended team composition. Covers Deliverable 44.

---

## Critical-Review Note: Reordering vs. the Brief

The original brief described phases roughly as: MVP (full governance loop) → Expansion (diagrams, ADRs, research) → Advanced (AI, analytics). This roadmap makes four adjustments:

1. **PH-0 added (Discovery & Validation).** The brief had no explicit discovery phase, but several analysis workstreams are already underway (Tarseem/Keystone repo inspection on 2026-06-24, Webex feasibility, open-source landscape). These activities need a formal phase so spikes produce signed-off answers (Keycloak claim mapping, MinIO provisioning, Webex licensing) before coding begins. PH-0 is short and mostly done; its gate is written permission/access confirmation, not calendar time.

2. **Voting domain in PH-1, full voting UI also in PH-1.** The core committee loop (intake → agenda → meeting → decision) is meaningless without a vote that produces a decision. The brief hinted at separating voting into Phase 2; this roadmap keeps the full voting capability (FR-070 through FR-078) in PH-1. The voting UX is not complex: a configured ballot, a cast-vote action, and chairman approval. Deferring it would leave PH-1 without a complete governance loop — the primary value proposition.

3. **Risk and Dependency domain split across PH-1/PH-2.** Risk record creation (FR-089–FR-091, FR-093) and basic dependency recording with cross-stream flagging (FR-094–FR-095, FR-098) are in PH-1 because action items and decisions reference them. The graph visualization, impact analysis, and Tarseem-rendered dependency diagram move to PH-2.

4. **Tarseem sidecar deferred to PH-2.** The brief listed diagrams under Phase 2; this roadmap keeps that. Tarseem (v1.0.0, released 2026-06-17) is early; using it only as a render sidecar from PH-2 limits blast radius. JSON spec authoring and the render pipeline are PH-2 scope.

---

## Phase Summary Table

| Phase | Theme | Relative Size | Primary Value |
|---|---|---|---|
| PH-0 | Discovery & Validation | S | Eliminate blockers before code |
| PH-1 | MVP Governance | XL | Complete governance loop live |
| PH-2 | Governance Expansion | XL | Full governance maturity |
| PH-3 | Research & Knowledge | L | Intelligence & advanced analytics |

---

## PH-0 — Discovery & Validation

### Objectives
- Confirm Keycloak group/realm-role claim schema and obtain test credentials for ACMP role mapping.
- Confirm MinIO provisioning (storage volume, network path, credentials), and SQL Server instance allocation.
- Confirm Webex licensing (Webex Assistant enablement authority, bot-vs-integration account type, Adaptive Cards test space).
- Validate Tarseem CLI installation (`tarseem doctor`) in the target container base image; confirm Chromium raster path.
- Validate Arabic FTS quality in SQL Server FTS with sample committee terminology (OQ-AR-FTS).
- Obtain stakeholder sign-off on canonical glossary terms in both EN and AR (bilingual terminology sheet).
- Complete this planning package (all 59 deliverables).
- Confirm CI system choice (OQ-CI) and establish repository structure (docs/34).

### Scope
- Spike: Keycloak claim mapping (obtain claim names, write mapping proof-of-concept).
- Spike: MinIO S3 API compatibility test with ACMP `IFileStore` stub.
- Spike: Tarseem Docker container build + `tarseem doctor` passing inside container.
- Spike: SQL Server FTS Arabic word-breaker test with 20 sample queries.
- Stakeholder: bilingual glossary review session.
- Planning: complete all 59 deliverables in this package; initialize repository skeleton.

### Out-of-Scope (PH-0)
- Any feature code (no production endpoints, no database schema).
- Webex Adaptive Card rendering (Phase 2 concern).
- Tarseem render pipeline (Phase 2 concern).

### Dependencies
- DEP-001: Keycloak instance accessible + admin can map ACMP roles to groups.
- DEP-002: SQL Server instance allocated to ACMP (own schema/database).
- DEP-003: MinIO instance provisioned (or confirmed path to provision it in Docker Compose).
- DEP-004: Development VM(s) and Docker Compose environment available.

### Deliverables
- Signed-off spike reports: Keycloak mapping, MinIO, Tarseem, Arabic FTS.
- Bilingual glossary (EN ↔ AR) signed off by chairman/secretary.
- Complete planning package (all 59 docs + ADRs).
- Repository skeleton initialized with CLAUDE.md.
- CI pipeline stub (OQ-CI decision required).

### Risks
| Risk | Mitigation |
|---|---|
| Keycloak claim names unknown; mapping may require Keycloak admin involvement | Coordinate early; spike produces a config file, not code |
| MinIO not yet provisioned; blocks file attachment work in PH-1 | Include MinIO in Docker Compose from day 1; no external provisioning needed in dev |
| Arabic FTS inadequate in SQL Server | Spike in PH-0; if inadequate, OQ-AR-FTS escalates to consider Meilisearch alt in PH-2 |

### Acceptance Criteria
- All PH-0 spikes produce a written outcome doc marked Pass/Fail/Conditional.
- Every open decision that blocks PH-1 start is resolved or has an agreed default.
- Planning package passes Keystone validator gates G-IDS, G-DEC-STATUS, G-REQ-SRC, G-COMPLETE.

### Exit Criteria
- All PH-1-blocking OQ items are resolved (see docs/42-open-decisions.md "needed-by-phase = PH-1").
- Repository skeleton reviewed and branch strategy agreed.
- Team composition confirmed and environments accessible.

### Recommended Team Composition
- 1 Tech Lead / Architect (spikes + planning review)
- 1 Secretary (stakeholder liaison, glossary sessions)
- Part-time: Security reviewer (Keycloak/TLS spike review)
- Part-time: UX (bilingual glossary, RTL terminology validation)

---

## PH-1 — MVP Governance

### Objectives
- Deliver the complete architecture governance loop: topic intake → backlog → agenda → meeting → voting → decision → action tracking.
- Identity via Keycloak; RBAC + per-topic ABAC enforced end-to-end.
- In-app notification center (sole channel in v1).
- Full EN/AR bilingual UI with RTL; light/dark themes.
- Append-only audit log with hash-chain integrity for votes/decisions.
- Basic committee, secretary, and chairman dashboards.
- Structured logging (Seq), background jobs (Hangfire), MinIO file storage, health checks.
- Basic full-text search across topics, decisions, MoMs.
- Typed traceability panel on every artifact.
- Risk record creation and basic dependency recording.

### Scope

**Modules delivered in PH-1 (all Must-priority FRs):**

| Module | Capabilities |
|---|---|
| Platform | FR-001–FR-015: OIDC/Keycloak auth, EN/AR/RTL, light/dark, IFileStore, health checks, OpenAPI, Serilog→Seq, OTel, Hangfire, EF Core migrations, config externalization, validation UX, unsaved-work guard |
| Membership | FR-016–FR-022, FR-024: user provisioning, RBAC from Keycloak claims, stream assignment, deactivation, member directory, per-topic ABAC (Owner/Assignee/Presenter) |
| Topics | FR-025–FR-029, FR-031–FR-035, FR-037–FR-040, FR-042–FR-044: topic submission, canonical ID, attachments, comments, triage, backlog list/table/kanban/calendar views, DnD reprioritization, aging indicator, topic detail page, edit lock, Prepared/Scheduled status, rejection immutability |
| Meetings | FR-046–FR-056, FR-061: agenda creation+reorder, carry-over, agenda publish, meeting record, attendance, live notes, MoM generation+versioning+approval+distribution, manual recording attachment, meeting calendar view |
| Decisions | FR-062–FR-067, FR-069: decision record, canonical outcomes, alternatives, supersession, immutability, downstream link requirement, decision history list |
| Voting | FR-070–FR-075, FR-077–FR-078: vote configuration, quorum enforcement, attributed voting, chairman approval, immutable vote records, vote lifecycle, vote-open notification |
| Actions | FR-079–FR-088: action creation, required owner+due date, status tracking, overdue derivation, reminder/escalation Hangfire jobs, completion validation, cancellation, actions dashboard, per-topic action list |
| Risks | FR-089–FR-091, FR-093: risk creation, status lifecycle, mitigation plan, per-topic risk list |
| Dependencies | FR-094–FR-095, FR-098: dependency edge creation, cross-stream flag, per-topic dependency list |
| Notifications | FR-129–FR-132: INotificationChannel abstraction, in-app notification center, full event catalog, deep links, Webex 429 back-off (abstraction ready; Webex adapter deferred to PH-2) |
| Reporting | FR-135–FR-137: committee dashboard, secretary dashboard, chairman dashboard |
| Search & Traceability | FR-143–FR-147: global FTS search, EN+AR FTS, search result grouping, traceability panel on every artifact, create/read/delete typed edges |
| Audit & Records | FR-150–FR-153: append-only audit log, full entry schema, immutability guarantee, audit search |

### Out-of-Scope (PH-1)
- Tarseem diagram rendering, ADRs, Architecture Invariants, Research/Keystone import, Knowledge/wiki, Templates.
- Guest/Presenter time-boxed access.
- Webex API recording/transcript retrieval.
- Notification user preferences; digest jobs.
- Per-stream reporting, decision history CSV export, action completion rate chart.
- Timeline/Gantt view, conflict-of-interest tracking, topic conversion workflow.
- Audit log export; data retention policy configuration.

### Dependencies
- DEP-001 (Keycloak): claim mapping confirmed from PH-0 spike.
- DEP-002 (SQL Server): instance allocated.
- DEP-003 (MinIO): operational in Docker Compose.
- DEP-005 (Seq): self-hosted container in Compose stack.

### Deliverables
- Running ACMP application covering all PH-1 scope above, containerized via Docker Compose.
- EF Core migration scripts for PH-1 schema.
- OpenAPI document for all PH-1 endpoints.
- Automated test suite: unit tests per handler, integration tests per module, E2E smoke tests for critical paths (login → topic submit → triage → agenda → meeting → vote → decision → action).
- EN + AR UI with 100% RTL coverage validated on Chrome/Edge.
- Deployed to staging environment; committee members can perform a pilot run.

### Risks
| Risk | Likelihood | Mitigation |
|---|---|---|
| Keycloak claim mapping coordination delays | M | PH-0 spike; fallback: static role config for dev |
| Bilingual/RTL effort underestimated | H | Allocate dedicated RTL pass per sprint; RTL is first-class (P-07), not a backlog item |
| Committee adoption — members revert to text-file process | M | Involve secretary + champion member in pilot; gather feedback after first real meeting on the system |
| Scope creep toward generic PM | M | Reject any work item not traceable to a FR; reference P-01 in every sprint review |

### Acceptance Criteria
- All Must (M) priority FRs pass their AC-### criteria (docs/40).
- E2E governance loop (topic → decision → action) completed end-to-end by Secretary in staging.
- Audit log verified immutable: direct DB update attempt on an audit row fails.
- Vote record: chairman override recorded, immutable after Ratified.
- EN↔AR switch: all strings localized, no LTR artifacts in AR mode (verified by RTL visual regression run).
- Light/dark: all pages tested in both themes.
- Health check endpoints return 200 (liveness + readiness) in Docker Compose.
- Hangfire dashboard accessible to Administrator; reminder and escalation jobs fire on schedule.

### Exit Criteria
- All M-priority FRs implemented, tested, and signed off.
- Zero Sev-1 defects open.
- Committee secretary has completed one full meeting cycle on the platform (staging or production).
- Staging deployment stable for ≥5 business days without restart.

### Recommended Team Composition
- 1 Tech Lead / Senior .NET Architect
- 2 .NET Backend Engineers
- 1–2 React/TypeScript Frontend Engineers (one must have RTL/i18n experience)
- 1 QA Engineer (manual + automated E2E)
- Part-time: Security Reviewer (auth flows, audit immutability review)
- Part-time: UX Designer (bilingual wireframe review, RTL validation)
- Product Owner: Secretary (acts as PO; domain authority)

---

## PH-2 — Governance Expansion

### Objectives
- Full governance maturity: ADRs, Architecture Invariants, Research/Keystone import.
- Tarseem diagram sidecar operational; dependency graph visualization.
- Templates, Knowledge/wiki.
- Webex adapter (notifications + recording/transcript retrieval).
- Advanced traceability: impact analysis, graph traversal.
- Expanded reporting: per-stream, decision history, action trend charts.
- Notification preferences and digest jobs.
- Guest/Presenter time-boxed invitations.
- Conflict-of-interest tracking; timeline/Gantt view.
- Audit log export; data retention configuration.

### Scope

**Modules expanded/added in PH-2:**

| Module | Capabilities |
|---|---|
| Membership | FR-023: Guest/Presenter time-boxed invitation |
| Topics | FR-036, FR-030, FR-041, FR-045: Gantt-lite view, topic conversion workflow, topic templates, Reopen |
| Meetings | FR-057–FR-058: Webex recording metadata retrieval (Hangfire), transcript retrieval (Webex Assistant constraint) |
| Decisions | FR-068: Decision→ADR promotion; FR-076: conflict-of-interest flag |
| Governance — ADRs | FR-099–FR-105: ADR creation (MADR-lite), lifecycle, supersession, repository view, FTS, traceability links, templates |
| Governance — Invariants | FR-106–FR-109: Invariant creation, lifecycle, violation recording, invariant list |
| Research | FR-111–FR-115: Research Mission, Keystone import, Finding/Recommendation entities, Research Mission detail, traceability links |
| Knowledge | FR-116–FR-120: wiki pages, versioning, FTS on wiki, templates (Topic/MoM/ADR/Research Mission) |
| Diagrams | FR-121–FR-128: Tarseem sidecar, JSON spec → render pipeline, Hangfire render job, version history, error surface, artifact export, attachment to topics/ADRs/decisions |
| Dependencies | FR-096–FR-097: impact analysis query, dependency graph via Tarseem |
| Notifications | FR-133–FR-134: notification user preferences, digest jobs; Webex Adaptive Card adapter (FR-130 Phase 2 items) |
| Reporting | FR-138–FR-140, FR-142: per-stream report, decision history report + CSV export, action completion rate chart, CSV/PNG export for all reports |
| Search & Traceability | FR-148: transitive impact analysis query (configurable depth) |
| Audit & Records | FR-154–FR-155: audit log export CSV/JSON; retention policy configuration |
| Risks | FR-092: risk escalation notification |

### Out-of-Scope (PH-2)
- AI transcript extraction (PH-3).
- Invariant exception workflow (PH-3 unless prioritized early).
- Traceability matrix CSV export (PH-3).
- KPI/health dashboard (PH-3).
- Bulk topic operations (PH-3).
- Email channel (deferred until SMTP relay confirmed).
- External partner integration APIs.

### Dependencies
- DEP-001 (Keycloak): operational from PH-1.
- DEP-006 (Tarseem): containerized sidecar; `tarseem doctor` validated in PH-0 spike; v1.0.0 schema frozen. Risk: early maturity (released 2026-06-17, ~9 days old at PH-0); mitigated by JSON-spec-as-source-of-truth (ADR-0006) — ACMP stores the spec; render artifacts are regenerable.
- DEP-007 (Webex): bot/integration account, licensing confirmed, Webex Assistant enabled for transcripts (cannot be done programmatically — requires host/Control Hub decision).
- DEP-008 (Keystone, optional): Keystone package schema stable; import tool can parse it.

### Deliverables
- Tarseem sidecar container (FastAPI wrapper or CLI invocation via Hangfire) running in Compose stack.
- All PH-2 modules operational on staging.
- Webex adapter: recording metadata + optional transcript retrieval; Adaptive Card notifications.
- ADR repository; Invariant registry; Research Mission + Keystone import tool.
- Knowledge wiki; templates.
- Dependency graph visualization (Tarseem dependency family).
- Expanded test suite covering PH-2 FRs.
- Updated OpenAPI document.

### Risks
| Risk | Likelihood | Mitigation |
|---|---|---|
| Tarseem early maturity (v1.0.0, ~9 days old) | M | JSON spec is source of truth; broken render = re-render; never blocking |
| Webex Assistant not enabled org-wide; transcripts unavailable | H | Transcript retrieval is non-blocking; recording link manual upload is PH-1 fallback |
| Webex licensing/bot account procurement delays | M | Notification adapter abstraction means in-app notifications still work; Webex is additive |
| Impact analysis node cap (graph traversal performance) | L | SQL graph traversal with depth limit (default 3 hops); OQ-IMPACT-CAP for threshold |
| Keystone schema changes (optional feature) | L | Import tool versioned; ACMP pins to Keystone release tag |

### Acceptance Criteria
- All Should (S) priority FRs pass their AC-### criteria.
- Tarseem renders a flowchart, architecture/C4, and dependency diagram from JSON spec; exports SVG/PNG/draw.io.
- ADR created, promoted from a decision, linked to a topic; displayed in ADR repository.
- Webex Adaptive Card notification delivered to a test Webex space.
- Research Mission imported from a sample Keystone package; findings/recommendations visible and linkable.
- Impact analysis: given topic A → blocks topic B → blocks action C; query from A returns B and C.

### Exit Criteria
- All S-priority FRs implemented, tested, and signed off.
- Webex adapter: if Webex licensing unconfirmed, gate = adapter code complete + unit-tested + integration test skipped with explanation.
- Tarseem sidecar stable for 48h continuous in staging.
- Zero Sev-1 defects; Sev-2 defects have mitigation plan.

### Recommended Team Composition
- 1 Tech Lead
- 2 .NET Backend Engineers (may overlap with PH-1 team)
- 1–2 React/TypeScript Frontend Engineers
- 1 QA Engineer
- Part-time: Security Reviewer (Webex OAuth, transcript data handling, OWASP ASVS L2 re-check)
- Part-time: UX Designer (diagram authoring UX, wiki editor, Webex card design)

---

## PH-3 — Research & Knowledge

### Objectives
- Optional Keystone advanced import (if PH-2 import proves insufficient).
- AI-assisted extraction from transcripts: candidate action items and decision summaries, always human-reviewed (P-05, OWASP LLM01).
- Advanced transcript full-text search.
- Email notification channel (when SMTP relay available).
- KPI/health dashboard.
- Traceability matrix export.
- Invariant exception request workflow.
- Bulk topic operations.
- Advanced analytics (if demand demonstrated).

### Scope

| Module | Capabilities |
|---|---|
| Meetings | FR-059–FR-060: transcript FTS; AI candidate extraction (human-reviewed, Phase 3 gate) |
| Governance — Invariants | FR-110: exception request workflow |
| Notifications | Email adapter (when SMTP relay confirmed; FR-130 email items) |
| Reporting | FR-141: KPI/health dashboard (avg topic-to-decision days, action SLA %, backlog age, vote-to-ratification) |
| Search & Traceability | FR-149: traceability matrix CSV export |
| Topics | FR-045 (if not done in PH-2 Reopen), FR-030 bulk ops (from D-10) |
| Audit & Records | Advanced audit export formats; long-term archival strategy |

### Out-of-Scope (PH-3 and beyond — requires explicit decision to add)
- Multi-committee generalization.
- External partner portal.
- Real-time collaborative editing (OOS-07).
- Mobile-native apps (OOS-05).
- Public self-registration (OOS-06).

### Dependencies
- DEP-009 (SMTP relay): org SMTP or SendGrid/SES confirmed; email adapter only activated when available.
- DEP-010 (AI/LLM endpoint): transcript AI extraction requires an LLM endpoint reachable from ACMP container (on-prem LLM, Azure OpenAI, or other); privacy and data residency review required before activation (OWASP LLM01; see RISK-012).
- DEP-008 (Keystone, optional): advanced import if needed.
- DEP-002 (SQL Server): transcript FTS index may need configuration tuning.

### Deliverables
- AI extraction feature (candidate-only; human approval gate enforced in code; LLM endpoint configurable).
- Email adapter implementing `INotificationChannel`; zero code change to notification dispatch.
- KPI dashboard with configurable threshold baselines (OQ-KPI-THRESHOLDS).
- Traceability matrix CSV export.
- Invariant exception workflow.
- Updated deployment docs for SMTP relay and LLM endpoint configuration.

### Risks
| Risk | Likelihood | Mitigation |
|---|---|---|
| AI extraction privacy: transcript content sent to LLM (OWASP LLM01; RISK-012) | H | Human-review gate mandatory; LLM endpoint must be on-prem or data-residency compliant; feature is off by default; requires explicit Admin activation |
| SMTP relay procurement delayed | M | Email adapter is additive; in-app + Webex notifications remain active |
| KPI threshold definition unclear (no historical baseline) | H | Ship with configurable thresholds; default values from committee agreement; OQ-KPI-THRESHOLDS |
| Transcript availability still limited (Webex Assistant) | M | AI extraction degrades gracefully; manual entry always available |

### Acceptance Criteria
- AI candidate extraction: secretary presented with ≥1 candidate action from a test transcript; no candidate enters the record without explicit secretary approval.
- Email adapter: notification dispatched via SMTP without changes to INotificationChannel call site.
- KPI dashboard: four KPIs render with 90-day historical data from staging.
- Traceability matrix CSV: exported file contains correct artifact columns and relationship edges for a test topic.

### Exit Criteria
- All Could (C) priority FRs committed to this phase are implemented and tested.
- AI extraction feature toggled OFF by default; activation requires Admin configuration.
- Privacy review signed off for AI extraction (LLM endpoint data residency confirmed).
- Post-release operating model (docs/43) updated to reflect Phase 3 operational procedures.

### Recommended Team Composition
- 1 Tech Lead (part-time; platform stabilized)
- 1 .NET Backend Engineer
- 1 React/TypeScript Frontend Engineer
- 1 QA Engineer
- Part-time: Security Reviewer (AI/LLM data flow, OWASP LLM01)
- Part-time: UX (KPI dashboard design, AI extraction review UX)

---

## Traceability

- Phase definitions consumed by: `docs/06-scope-and-out-of-scope.md` (capability phase tags), `docs/07-functional-requirements.md` (phase column), `docs/38-epics-and-features.md` (epic phase), `docs/37-implementation-backlog.md` (item phase).
- Phase exit criteria → `docs/40-acceptance-criteria.md` (AC-### coverage per phase).
- Phase risks → `docs/41-raid.md` (RISK-### entries).
- Phase dependencies → `docs/41-raid.md` (DEP-### entries).
- Phase open decisions → `docs/42-open-decisions.md` (OQ-### "needed-by-phase" column).
- ADRs constraining phase content: ADR-0001 through ADR-0012 (`adr/`).
