<p align="center"><img src="src/Acmp.Web/public/favicon.svg" alt="ACMP logo" width="72" height="72" /></p>

# Architecture Committee Management Platform (ACMP) — Planning & Execution Package

**Status:** Draft v1.0 · **Date:** 2026-06-24 · **Audience:** Engineering / execution (the lead secretary + the Claude Code execution agent) · **Downstream executor:** Claude Code · **Design executor:** Claude Design

This repository is a **planning and handoff package**, not application code. It defines *what* to build, *why*, *which decisions are settled*, *which remain open*, and *how* an execution agent should build it without overengineering. It deliberately follows the **Keystone** methodology and identifier scheme (see `docs/20-keystone-analysis-and-integration.md`) so the package is internally consistent and traceable.

> **One-line product definition:** A focused, auditable, bilingual (EN/AR) web platform that is the single system of record for the Architecture Committee — from topic intake through backlog, agenda, meeting, minutes, voting, decision, ADR, action, risk, dependency, and traceability — replacing the current text-file process. It is *architecture governance*, not generic project management.

---

## How to use this package

1. **Read this README fully.** It is the canonical reference: glossary, roles, modules, entities, IDs, status models, and the settled technology decisions. Every other document defers to it.
2. **For context and rationale**, read `docs/` in numeric order.
3. **For the build**, the execution agent reads `execution-handoff/initial-prompt.md` first, then obeys `execution-handoff/agent-guardrails.md` and the generated repo's `CLAUDE.md`.
4. **For UI**, submit `design-handoff/` to Claude Design.
5. **Decisions** are recorded as ADRs in `adr/`. Anything unsettled is in `docs/42-open-decisions.md` — these are flagged, never hidden.

**Conventions.** Facts are cited. Recommendations are labelled **Recommendation**. Unverified claims are marked `[unverified]`. Assumptions are `ASM-###` and listed in `docs/41-raid.md`. Nothing in this package should be treated as code-ready until its acceptance criteria (`docs/40-acceptance-criteria.md`) are defined.

---

## ⚠️ Self-contained deployment constraint (CON-001 — applies everywhere)

**The platform must not depend on the organization's shared runtime infrastructure.** Specifically it does **not** use the org's **Hangfire**, **ELK/Seq**, or **centralized notification platform (Email/SMS/Firebase)**. ACMP **builds and self-hosts its own** background processing, observability/logging, and notification channels as part of its own deployment.

As of **ADR-0015 (2026-06-25), ACMP owns and bundles all of its runtime dependencies** — there are **no external runtime services** in v1:
- **SQL Server** — the mandated datastore, **bundled in ACMP's own Docker Compose stack**.
- **Identity (OIDC)** — **Keycloak is self-hosted as a bundled container with an ACMP-owned realm** (authorization-code + PKCE; roles from realm-role/group claims; no self-registration). ACMP does **not** federate to an org IdP (the org has none) and does **not** build its own authorization server — it runs Keycloak. *(Updated from the original "federate to org Keycloak" — see ADR-0015.)*
- **Webex** — the **only** external dependency: a deferred **Phase-2** SaaS integration via a pluggable adapter (not a v1 runtime service).

Everything else the platform needs, it ships in its own containers.

---

## Deliverable index (all 59 required deliverables → location)

| # | Deliverable | File |
|---|---|---|
| 1 | Executive summary | `docs/00-executive-summary.md` |
| 2 | Understanding of org & problem | `docs/01-organization-and-problem.md` |
| 3 | Current-state process analysis | `docs/02-current-state-analysis.md` |
| 4 | Pain-point analysis | `docs/03-pain-points.md` |
| 5 | Stakeholder & user-role analysis | `docs/04-stakeholders-and-roles.md` |
| 6 | Product vision | `docs/05-product-vision-and-principles.md` |
| 7 | Product principles | `docs/05-product-vision-and-principles.md` |
| 8 | Scope definition | `docs/06-scope-and-out-of-scope.md` |
| 9 | Out-of-scope definition | `docs/06-scope-and-out-of-scope.md` |
| 10 | Functional requirements | `docs/07-functional-requirements.md` |
| 11 | Non-functional requirements | `docs/08-non-functional-requirements.md` |
| 12 | Topic taxonomy | `docs/09-topic-taxonomy.md` |
| 13 | Permission & role matrix | `docs/10-permission-role-matrix.md` |
| 14 | Domain model | `docs/11-domain-model.md` |
| 15 | Entity lifecycle models | `docs/12-entity-lifecycles.md` |
| 16 | Workflow definitions | `docs/13-workflows.md` |
| 17 | Information architecture | `docs/14-information-architecture-sitemap.md` |
| 18 | Sitemap | `docs/14-information-architecture-sitemap.md` |
| 19 | Page & screen inventory | `docs/14-information-architecture-sitemap.md` |
| 20 | Architecture recommendation | `docs/15-architecture.md` |
| 21 | Architecture diagrams | `docs/15-architecture.md` |
| 22 | Module definitions | `docs/15-architecture.md` |
| 23 | Data architecture | `docs/16-data-architecture-and-model.md` |
| 24 | Initial logical data model | `docs/16-data-architecture-and-model.md` |
| 25 | Integration architecture | `docs/17-integration-architecture.md` |
| 26 | Webex feasibility analysis | `docs/18-webex-feasibility.md` |
| 27 | Tarseem repo analysis + integration | `docs/19-tarseem-analysis-and-integration.md` |
| 28 | Keystone repo analysis + integration | `docs/20-keystone-analysis-and-integration.md` |
| 29 | Open-source landscape analysis | `docs/21-open-source-landscape.md` |
| 30 | Standards & best-practices analysis | `docs/22-standards-and-best-practices.md` |
| 31 | Build-vs-buy-vs-integrate analysis | `docs/23-build-vs-buy-vs-integrate.md` |
| 32 | Security threat model | `docs/24-security-threat-model.md` |
| 33 | Security-control plan | `docs/25-security-controls.md` |
| 34 | Audit & records-management plan | `docs/26-audit-and-records-management.md` |
| 35 | Reporting & dashboard specification | `docs/27-reporting-and-dashboards.md` |
| 36 | Metrics & KPI catalog | `docs/28-metrics-and-kpi-catalog.md` |
| 37 | Notification strategy | `docs/29-notification-strategy.md` |
| 38 | Search & traceability model | `docs/30-search-and-traceability.md` |
| 39 | Testing strategy | `docs/31-testing-strategy.md` |
| 40 | DevSecOps plan | `docs/32-devsecops-plan.md` |
| 41 | Containerization & deployment plan | `docs/33-containerization-and-deployment.md` |
| 42 | Repository structure | `docs/34-repository-structure.md` |
| 43 | Documentation plan | `docs/35-documentation-plan.md` |
| 44 | Phased roadmap | `docs/36-roadmap.md` |
| 45 | Prioritized implementation backlog | `docs/37-implementation-backlog.md` |
| 46 | Epics & features | `docs/38-epics-and-features.md` |
| 47 | User stories for initial release | `docs/39-user-stories-mvp.md` |
| 48 | Acceptance criteria | `docs/40-acceptance-criteria.md` |
| 49 | Risks, assumptions, constraints, dependencies | `docs/41-raid.md` |
| 50 | Open decisions requiring org input | `docs/42-open-decisions.md` |
| 51 | ADRs for major decisions | `adr/ADR-0001…ADR-0014` (incl. ADR-0013 self-contained, ADR-0014 jobs/observability) |
| 52 | Claude Design input package | `design-handoff/claude-design-input-package.md` |
| 53 | Claude Design prompts | `design-handoff/claude-design-prompts.md` |
| 54 | Claude Code execution package | `execution-handoff/claude-code-execution-package.md` |
| 55 | Initial Claude Code prompt | `execution-handoff/initial-prompt.md` |
| 56 | Follow-up prompts per phase | `execution-handoff/phase-prompts.md` |
| 57 | Definition of Done | `docs/44-definition-of-done.md` |
| 58 | Release-readiness checklist | `docs/45-release-readiness-checklist.md` |
| 59 | Post-release governance & operating model | `docs/43-post-release-operating-model.md` |
| + | Agent guardrails | `execution-handoff/agent-guardrails.md` |
| + | Generated-repo agent context | `CLAUDE.md` |

---

## Canonical reference (single source of truth)

### A. Settled technology decisions (each has an ADR)

*Resolved with the secretary on 2026-06-24 (these override any contradicting detail elsewhere):* self-contained (CON-001, no org runtime infra); IdP **Keycloak** with **committee roles supplied via Keycloak group/role claims**; observability **Seq** (self-hosted); background jobs **app-owned Hangfire on ACMP's own SQL**; storage **MinIO**; deployment **on-prem VM + Docker Compose** (no K8s); **no email in v1** (in-app notification center only); **Webex = Phase 2** adapter; recordings/transcripts = **reference + manual upload** (Webex fetch later); **AI extraction = Phase 3** (manual first); **voting always attributed**; **single committee** (not generalized); **all committee members may read all streams**; **retention = keep everything, configurable, no auto-purge in v1**; availability **24×7 / 99.9%**; dates **Gregorian only**; **Keystone optional**; **scale = on-prem, low traffic, ≤20 total users** — right-size everything (no HA cluster, no horizontal scaling, no heavy performance engineering; 99.9% achieved via simple redundancy + nightly backups).

| Area | Decision | ADR |
|---|---|---|
| Macro-architecture | **Modular monolith** (single deployable, logically modular). Microservices explicitly rejected for v1. | ADR-0001 |
| Backend | **.NET 8 (LTS), ASP.NET Core, REST**. Clean Architecture per module + vertical-slice handlers (MediatR). EF Core. | ADR-0002 |
| Primary datastore | **Microsoft SQL Server** only (transactional + reporting via columnstore + full-text search). ACMP runs its own DB instance. No second DB in v1. | ADR-0003 |
| Identity | **Keycloak (OIDC)** is the identity provider — **self-hosted as a bundled container with an ACMP-owned realm** (ADR-0015; authorization-code + PKCE; roles from realm-role/group claims; ACMP does **not** build its own authorization server, and does **not** federate to an org IdP). **No public self-registration**; invitation/provisioned onboarding (manual in the Keycloak admin console). | ADR-0004, ADR-0015 |
| Notifications | **Channel-abstraction** (`INotificationChannel`). **v1 channel = in-app notification center only (no email in v1).** **Webex adapter = Phase 2.** Email deferred until an SMTP relay is available. Webex never hard-coded; no org notification platform. | ADR-0005 |
| Diagrams | **Tarseem** as a containerized render sidecar; **JSON spec is the version-controlled source of truth**, artifacts are generated. | ADR-0006 |
| Research/planning | **Keystone is OPTIONAL** — a companion Claude Code workflow for Research/Discovery topics. The Research module works **standalone**; when used, the platform **imports** Keystone's structured artifacts and **adopts its ID scheme**. Never embedded as a service, never a hard dependency. | ADR-0007 |
| Traceability | Explicit **typed relationship** model (directed edges) over a shared `Artifact` identity; impact analysis by graph traversal in SQL. | ADR-0008 |
| Audit/immutability | Append-only **audit log**; **votes and issued decisions are immutable** (event-recorded, superseded never edited). | ADR-0009 |
| Voting | **Simple model, always attributed** (no anonymity in v1): eligible voters, options, quorum, abstentions; **chairman final approval/override** recorded explicitly by name. | ADR-0010 |
| Search | **SQL Server Full-Text Search** in v1; if it outgrows that, stand up the platform's **own self-hosted search** (e.g., an OpenSearch container) — **app-owned, never the org's ELK**. | ADR-0011 |
| Frontend | **React 18 + TypeScript + Vite**; `react-i18next` (EN/AR); RTL via logical CSS + `dir`; light/dark; accessible DnD (`@dnd-kit`). | ADR-0012 |

**Background jobs:** **app-owned Hangfire** running **in-process inside ACMP, backed by ACMP's own SQL Server** (its own schema) — **not** the org's Hangfire. Gives a dashboard, retries, and job history. Used for reminders, escalations, digests, and diagram-render jobs. A SQL-backed **outbox** guarantees notification/render durability.
**Observability:** **Serilog** structured logging + **OpenTelemetry** traces/metrics, shipped to an **app-owned, self-hosted Seq** container bundled in the ACMP deployment (Seq is the chosen log/trace backend). **No dependency on the org's ELK/Seq.** ASP.NET health checks.
**Object storage:** **self-hosted MinIO** (S3-compatible) container, app-owned, via an `IFileStore` abstraction; metadata in SQL. Pre-signed, time-limited URLs for sensitive files (recordings/transcripts).
**Deployment:** **on-prem VM(s) + Docker Compose** (no Kubernetes). Containers: ACMP app + **SQL Server** + self-hosted **Seq** + **MinIO** + (Phase 2) **Tarseem** sidecar. Availability target **24×7 / 99.9%**.
**Retention:** **keep all records, no automatic purge in v1**; retention is **configurable** so legal can set periods later. Votes, issued decisions, ADRs, and published minutes are immutable (ADR-0009).

### B. Canonical modules (bounded contexts inside the monolith)

Core domain: **Membership · Topics · Meetings · Decisions · Actions · Risks · Dependencies · Governance (ADRs + Invariants) · Research · Knowledge · Diagrams**
Cross-cutting/platform: **Notifications · Reporting · Search&Traceability · Audit&Records · Platform (Shared Kernel: IDs, localization, file storage, base entities, background jobs)**

Rule (enforced): a module may not read another module's tables; modules communicate via in-process public contracts (interfaces / MediatR) only. See ADR-0001, `docs/15-architecture.md`.

### C. Canonical roles (global RBAC) + per-topic capabilities (ABAC)

**Global roles:** `Chairman`, `Secretary` (display name: **Secretary of the Committee** — *أمين سر اللجنة*; the committee lead / primary user; renamed from "Coordinator" on 2026-06-25), `Member`, `Reviewer`, `Auditor`, `Administrator`, `Submitter` (stream requester), `Guest/Presenter` (time-boxed).
**Per-topic capabilities (relationship-based, not global roles):** `Owner`, `Assignee/Contributor`, `Presenter`.
Authorization = role policy **+** topic/stream scope (ABAC). **Roles are sourced from Keycloak group/realm-role claims** and mapped to the canonical roles. **Default stream visibility: all committee members may READ topics across all streams**; create/edit is restricted by role + ownership. **Single committee** (not generalized to multiple). Full matrix in `docs/10-permission-role-matrix.md`.

### D. Canonical topic taxonomy (kept deliberately small)

**Topic Type (4):** `ResearchDiscovery`, `ArchitectureDecision`, `EnhancementInnovation`, `GovernanceStandardization`.
**Urgency (attribute, not a type):** `Normal | Urgent | Critical` (drives SLA). *Rationale: "Urgent" is a handling speed, not a kind of work — modelling it as an attribute avoids type explosion.* Full taxonomy + devil's-advocate in `docs/09-topic-taxonomy.md`.

### E. Canonical status models

- **Topic:** `Draft → Submitted → Triage → Accepted → Prepared → Scheduled → InCommittee → Decided → Closed`; side states `Rejected`, `Deferred`, `Reopened`, `Converted`.
- **Committee decision outcome:** `Approved`, `ConditionallyApproved`, `Rejected`, `MoreInfoRequired`, `FeedbackProvided`, `EnhancementsRequired`, `DesignChangesRequired`, `ResearchRequired`, `Deferred`, `Escalated`, `Converted`.
- **Action:** `Open → InProgress → Blocked → Completed → Verified`; side `Cancelled`, `Overdue` (derived).
- **ADR:** `Draft → Proposed → Approved → (Superseded | Deprecated)`.
- **Architecture Invariant:** `Draft → Proposed → Active → (Retired | Superseded)`; violations tracked separately.
- **Risk:** `Open → Mitigating → Closed`; side `Accepted`, `Escalated`.
- **Vote:** `Configured → Open → Closed → Ratified` (immutable after close).

### F. Identifier scheme

**Planning-package IDs (used in *this* package, Keystone-aligned):**
`FR-###` functional req · `NFR-###` non-functional req · `CON-###` constraint · `ASM-###` assumption · `DEP-###` dependency · `OQ-###` open question · `DEC-###` planning decision · `ADR-####` decision record · `RISK-###` risk · `HYP-###` hypothesis · `AC-###` acceptance criterion · `PH-#` phase · `EPIC-##` · `US-###` user story · `KPI-##` metric · `W-##` workflow (defined in `docs/13-workflows.md`).
Statuses: `Draft → Proposed → Approved | Rejected | Superseded | Deferred → Implemented`. A *proposed* item is never rendered as *approved*.

**Runtime (in-app) entity keys** (human-readable, year-scoped): `TOP-YYYY-###` topic · `MTG-YYYY-###` meeting · `AGN-YYYY-###` agenda · `MIN-YYYY-###` minutes · `VOTE-…` · `DECN-YYYY-###` committee decision · `ACT-…` action · `RSK-…` risk · `DPN-…` dependency edge · `ADR-…` in-app ADR · `AIV-…` architecture invariant · `DOC-…` · `TPL-…` template · `DGM-…` diagram · `RMS-…` research mission · `FND-…` finding · `REC-…` recommendation. (Note: in-app ADRs and the product's "Architecture Invariant" `AIV-` are distinct from this package's planning `ADR-####` usage.)

### G. Glossary (committee terms; EN ↔ AR pairing finalized in design handoff)

Architecture Committee · Backlog · Topic · Agenda · Meeting · Minutes (MoM) · Decision · Vote · Quorum · Action · Risk · Dependency · ADR (Architecture Decision Record) · Architecture Invariant · Principle · Standard · Stream · System/Service · Research Mission · Finding · Recommendation · Traceability. Distinctions between *principle / standard / policy / constraint / invariant / decision / ADR* are defined once in `docs/22-standards-and-best-practices.md` §"Concept disambiguation" and must not be duplicated.

---

## Guiding principles for this package (and the build)

1. **Architecture governance, not project management.** Every feature must serve committee governance/traceability; reject generic PM creep.
2. **Modular monolith first.** No distributed architecture without a demonstrated, measured need.
3. **SQL Server is enough.** No second datastore without evidence.
4. **Self-contained, but don't reinvent solved problems.** The platform is **self-hosted and does not depend on the org's runtime infrastructure** (no shared Hangfire / ELK / Seq / notification platform — see CON-001). It builds its own background processing, observability, and notification channels using standard open-source libraries; it integrates **Tarseem** (diagrams) and **optionally Keystone** (research/discovery — not required), and **self-hosts Keycloak (OIDC)** for identity (ADR-0015).
5. **Human-reviewed automation.** AI-extracted transcript content is *candidate* until a human approves it.
6. **Auditable & immutable where it matters.** Votes and issued decisions cannot be silently changed.
7. **Bilingual and RTL are first-class**, not bolted on.
