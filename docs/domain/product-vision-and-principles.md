# 05 — Product Vision and Principles

**Purpose:** Defines the product vision, measurable goals, target outcomes, and binding product principles for the Architecture Committee Management Platform (ACMP). Covers Deliverables 6 and 7.

---

## Vision Statement

> **ACMP is the single, auditable system of record for the Architecture Committee — replacing the text-file process with a focused, bilingual (EN/AR) platform that makes architecture governance traceable, reproducible, and trustworthy from topic intake through decision, ADR, action, and cross-stream impact.**

The platform digitizes the committee's existing process with fidelity. It does not reinvent architecture governance; it operationalizes it.

---

## Product Goals

Each goal is measurable at the end of Phase 1 MVP acceptance testing (see `docs/validation/acceptance-criteria.md`).

| # | Goal | Metric / Target |
|---|---|---|
| G-1 | **Eliminate the text-file backlog.** All active topics, statuses, and decisions exist in ACMP with no authoritative information remaining outside it. | Zero active topics tracked outside ACMP after go-live cutover (verified by secretary audit within 30 days). |
| G-2 | **Make every decision traceable.** Every committee decision links to the topic that prompted it, the vote record, the rationale, and at least one downstream artifact (ADR, action, or invariant). | 100 % of committee decisions recorded in ACMP have ≥1 upstream topic link and ≥1 downstream artifact link by meeting close. |
| G-3 | **Reduce MoM preparation time.** The secretary spends less time assembling meeting minutes by having attendance, agenda items, notes, and AI-candidate extractions available in-app. | MoM draft produced within 15 minutes of meeting end (vs. current manual effort baseline [unverified — establish pre-go-live baseline]). |
| G-4 | **Keep actions visible until closed.** Open actions are not silently dropped between meetings; every open action has a current owner, due date, and status visible on the dashboard without custom querying. | 0 % of open actions have null owner or null due date at the close of any meeting (enforced by system validation). |
| G-5 | **Deliver accessible, bilingual governance.** All governance artifacts (topics, decisions, ADRs, invariants, MoM) are readable and operable in both English and Arabic with full RTL layout, with no loss of function between languages. | All P1 user stories pass acceptance in both EN and AR locale; zero RTL rendering defects at go-live. |

---

## Target Outcomes

Outcomes are the change in the world that the product goals collectively produce. They are the "why" behind each measurable goal.

| Outcome | Connected Goal(s) | Timeframe |
|---|---|---|
| The committee's institutional knowledge is preserved and searchable rather than residing in individuals' memory or private files. | G-1, G-2 | Phase 1 |
| Decision provenance is auditable — every decision can be traced back to the discussion, vote, and participants, and forward to what changed. | G-2 | Phase 1 |
| The secretary role is sustainable and less error-prone; the manual toil of maintaining text files, compiling MoMs, and chasing action updates is eliminated. | G-1, G-3, G-4 | Phase 1 |
| Cross-stream impact is visible — introducing a change in one stream surfaces dependent topics, blocked work, and affected systems before a decision is issued. | G-2, G-4 | Phase 2 |
| Governance quality improves measurably — recurring decision patterns, unresolved risks, and aging topics are visible on dashboards so the committee can self-correct. | G-5 | Phase 2 |

---

## Product Principles

The 10 guiding principles from `README.md §Guiding principles` are expanded here into product terms with their implications for feature decisions, trade-off resolution, and anti-pattern identification. Three additional principles specific to product scope are appended.

### P-01 — Architecture governance, not project management.

Every feature built must serve one of: topic lifecycle, committee decision process, action follow-up, risk/dependency tracking, ADR/invariant governance, traceability, or meeting management. Features that serve generic sprint planning, resource allocation, velocity tracking, or task management outside this scope are rejected. When in doubt: does the feature help the Architecture Committee reach or implement a governance decision? If not, it is out of scope.

*Implication:* Reject any request to add Gantt charts for implementation work, team capacity planning, or general to-do lists. These belong in stream project tools.

### P-02 — Modular monolith first; no distributed complexity without evidence.

The platform is a single deployable. Modules are bounded contexts communicating via in-process public contracts only. No module reads another module's tables. This principle constrains both implementation (no cross-module SQL joins; no microservice splits in v1) and product decisions (no async event sourcing, no separate search service, no real-time collaborative editing via OT/CRDT engines).

*Implication:* If a feature requires real-time multi-user concurrent editing, classify it as a Phase 3 requirement and evaluate the architectural cost before committing.

### P-03 — SQL Server is enough; no second datastore without evidence.

All data — transactional, reporting (columnstore), search (FTS), and graph traversal (dependency/traceability) — lives in one SQL Server instance. No Redis, Elasticsearch, or graph DB in v1 without a measured performance gap. This eliminates operational complexity and keeps the footprint appropriate for a low-traffic internal tool.

*Implication:* FTS for transcript and document search is the default; if search outgrows FTS at measured scale, stand up the platform's own self-hosted search (e.g., an OpenSearch container, app-owned) — never the org's ELK.

### P-04 — Reuse over rebuild.

App-owned Hangfire on ACMP's own SQL Server for background jobs; Serilog → self-hosted Seq (app-owned) for observability; in-app notification center (v1); Webex adapter (Phase 2); Tarseem for diagrams; Keystone (optional) for research/discovery packages. Do not build a diagramming engine. Do not build a background scheduler. Do not depend on the org's Hangfire, ELK/Seq, or notification platform (CON-001).

*Implication:* Any proposal to replace an integrated component must show a concrete deficiency of the existing one, not a preference.

### P-05 — Human-reviewed automation; AI output is a candidate, not a decision.

AI-extracted meeting content (action candidates, decision candidates, topic summaries from transcripts) is always surfaced as a draft requiring explicit human approval before it enters the official record. No AI-generated content is committed to the audit log automatically.

*Implication:* The transcript AI extraction feature is a productivity tool, not an automation. The UI must clearly distinguish AI-candidate items from confirmed items, and the confirmation action must be an explicit user gesture.

### P-06 — Auditable and immutable where it matters.

Votes and issued decisions are append-only — they are superseded or complemented, never silently edited. The audit log records who changed what and when for every state transition on governed entities. Immutability is a non-negotiable product invariant, not a "nice to have."

*Implication:* The product must not expose an "edit decision" button on issued decisions. Correction is always via a new decision that supersedes. The UI must make the difference between drafts (editable) and issued records (immutable) unambiguous.

### P-07 — Bilingual and RTL are first-class, not bolted on.

English and Arabic are equal product languages. RTL layout, correct bidirectional text rendering (HarfBuzz-shaping in diagrams via Tarseem; logical CSS properties in frontend), and bilingual terminology consistency are acceptance criteria on every feature, not post-release polish. The glossary (README §G) is the single source of canonical EN↔AR term pairs.

*Implication:* No feature ships without an AR locale test pass. Design tokens and CSS must use logical properties (`padding-inline-start`, not `padding-left`). Any hardcoded LTR-only layout is a blocker.

### P-08 — Progressive delivery; prove the core loop before expanding.

Phase 1 proves the committee's core governance loop: topic intake → backlog management → agenda → meeting → voting → decision → action. Phase 2 adds ADRs, invariants, risk/dependency graph depth, and Keystone research integration. Phase 3 adds advanced reporting, external integrations, and optional extended capabilities. Each phase must prove its value before the next begins.

*Implication:* Phase 2 work is explicitly not started until Phase 1 is accepted. Any Phase 2 item proposed for MVP must go through a scope-change process.

### P-09 — Explicit domain concepts over generic abstractions.

The domain model uses committee-native language: `Topic`, `Agenda`, `Meeting`, `Minutes`, `Vote`, `Decision`, `Action`, `ADR`, `ArchitectureInvariant`, `Risk`, `Dependency`, `ResearchMission`, `Finding`, `Recommendation`. Generic concepts like "item", "task", "record", "entity" are forbidden in UI labels and public API contracts. Code names must reflect domain terms.

*Implication:* A `Topic` is never renamed to a `ticket` or `card` in UI copy. An `Action` is not a `task`. Canonical IDs (`TOP-YYYY-###`, `DECN-YYYY-###`) are exposed to users and used in all communications.

### P-10 — Surface assumptions and open decisions; never mask them.

Every unchecked assumption is documented as `ASM-###` in `docs/risks/risk-register.md`. Every unresolved product or technical question is an `OQ-###` in `docs/decisions/open-decision-register.md`. Nothing remains a silent assumption. If a feature depends on an open decision (e.g., blob storage provider), that dependency is explicit and the feature is not committed as "done" until the decision is resolved.

*Implication:* Status fields must never be set to misleadingly positive values when their underlying assumptions are unvalidated. The product backlog must carry open-decision blockers as explicit work items.

---

### P-11 — Traceability is the product, not a feature.

Traceability — the ability to navigate from any artifact to everything it relates to, depends on, or affected — is the core product value proposition, not an add-on or a reporting dashboard. Every entity in the platform has typed, directional relationships to other entities. The relationship model (see ADR-0008, `docs/domain/domain-model.md`, `docs/domain/search-and-traceability.md`) is designed first; data entry screens are designed to populate it, not the reverse.

*Implication:* A feature that stores data without populating the traceability graph (e.g., attaching a decision without linking it to the topic) is incomplete. Traceability links are required fields, not optional metadata.

### P-12 — The committee's existing process is the spec — digitize it, don't reinvent it.

The current committee process (text-file backlog, weekly meeting, voting, MoM, chairman approval) is the authoritative specification for the platform's workflows. ACMP must faithfully reproduce that process in digital form before proposing improvements or extensions. Any deviation from the documented current-state process (see `docs/domain/current-state.md`) must be explicitly proposed, discussed, and recorded as a process change, not silently assumed.

*Implication:* If the committee uses a specific voting mechanic or MoM format, the platform must implement that mechanic first. Improvements are Phase 2 proposals, not Phase 1 defaults.

### P-13 — Every artifact has an owner and a status.

Every governed artifact — topic, action, ADR, risk, dependency, invariant, research mission — has exactly one current owner and one canonical status at all times. Orphaned or status-ambiguous artifacts are a system defect. The platform enforces ownership and status completeness as a workflow invariant: creation always assigns owner; deletion is not available for in-flight artifacts; status transitions require explicit user action and are recorded.

*Implication:* Bulk imports, migrations, and API endpoints must validate owner and status before persisting. The "unknown owner" and "unknown status" states do not exist in the domain model.

---

## Anti-Goals

Anti-goals are explicit statements of what ACMP will never do. They are as important as goals because they define the boundary that keeps the product focused.

| Anti-Goal | Rationale |
|---|---|
| **Not a general project/sprint management tool.** No velocity tracking, team capacity, sprint boards, or backlog grooming for engineering teams. | Those belong in stream PM tools; including them makes ACMP a second Jira and dilutes its governance focus. |
| **Not a diagramming engine.** No custom diagram renderer, shape editor, or canvas. | Tarseem exists and covers all required diagram families (ADR-0006). Building a diagramming engine is expensive and off-mission. |
| **Not a research methodology platform.** No custom research workflow engine beyond what Keystone produces as importable artifacts. | Keystone is the companion workflow (ADR-0007). ACMP imports its outputs; it does not replace it. |
| **Not a meeting/video platform.** No video conferencing, real-time audio/video, breakout rooms, or chat. | Webex handles meetings. ACMP integrates with Webex for metadata, notifications, and recording/transcript retrieval. |
| **Not a mobile-native app.** No iOS/Android native app in v1 or v2. | The committee is a desktop-use scenario (secretarys, reviewers, technical directors). A responsive web app on desktop+tablet is sufficient. |
| **Not publicly accessible.** No public self-registration, no public topic submission portal, no anonymous browsing of decisions. | This is a sensitive internal governance tool. All access is invitation/provisioned via OIDC/Keycloak (ADR-0004). |
| **Not a real-time collaborative editor.** No Google-Docs-style concurrent editing (OT/CRDT) in v1. | The committee artifact lifecycle (draft → review → approve) does not require simultaneous editing. OT/CRDT adds significant architectural complexity; classify as a future option only if demand is demonstrated. |
| **Not a workflow/BPM engine.** No BPMN modeling, no configurable process engine, no custom workflow designer. | The committee's workflow is known and stable. Hard-coded, well-tested state machines in the domain model are simpler, more reliable, and easier to audit than a general-purpose BPM runtime. |
| **Not an SSO/IdP.** No identity provider, no user directory management, no credential storage. | ACMP consumes identity from Keycloak/OIDC (ADR-0004). Authentication and IdP management are out of scope. |
| **Not a data warehouse or BI platform.** No OLAP cubes, no ETL pipelines, no ad-hoc SQL workbench for end users. | Reporting is via read models + SQL Server columnstore. If advanced analytics are needed, the org's existing BI stack handles it with ACMP as a data source (future, not v1). |

---

## Traceability

- Vision and goals → `docs/00-charter.md`, `docs/domain/organization-and-problem.md`, `docs/domain/pain-points.md`
- Principles → `README.md §Guiding principles` (expanded here); `docs/adrs/adr-0001` through `ADR-0012` (each principle maps to ≥1 settled ADR)
- Anti-goals → `docs/domain/scope-and-out-of-scope.md §Out-of-scope` (anti-goals are repeated there with implementation-level detail)
- Measurable goals → `docs/validation/acceptance-criteria.md` (acceptance criteria operationalize each goal)
- Product goals G-1–G-5 → `docs/domain/metrics-kpi-catalog.md` (KPIs that track goal achievement post-release)
