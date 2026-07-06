# Architecture Committee Management Platform (ACMP) — Planning & Execution Package

**Status:** Approved · **Date:** 2026-06-24 · **Updated:** 2026-07-06 (Keystone v1.0.0 package layout; build shipped through P12) · **Audience:** Engineering / execution (the lead secretary + the Claude Code execution agent) · **Downstream executor:** Claude Code

This repository holds the **ACMP application** (`src/`, `deploy/`, `tests/`) together with its **planning & governance package** (`docs/` — a Keystone v1.0.0 package). The package defines *what* to build, *why*, *which decisions are settled*, *which remain open*, and *how* an execution agent should build it without overengineering; it follows the **Keystone** methodology and identifier scheme (see `docs/domain/keystone-analysis.md`) so it is internally consistent and traceable.

> **One-line product definition:** A focused, auditable, bilingual (EN/AR) web platform that is the single system of record for the Architecture Committee — from topic intake through backlog, agenda, meeting, minutes, voting, decision, ADR, action, risk, dependency, and traceability — replacing the current text-file process. It is *architecture governance*, not generic project management.

---

## How to use this package

1. **Read this README fully.** It is the GitHub landing copy of the canonical reference: glossary, roles, modules, entities, IDs, status models, and the settled technology decisions. The package entry point is [`docs/README.md`](docs/README.md).
2. **For context and rationale**, follow the reading order in [`docs/README.md`](docs/README.md) (charter → architecture → roadmap → acceptance criteria; deep detail under `docs/domain/`).
3. **For the build**, the execution agent reads `CLAUDE.md` (which imports `AGENTS.md`), then [`docs/handoff/initial-prompt.md`](docs/handoff/initial-prompt.md); the binding constraints are the invariants in [`docs/requirements/invariant-register.md`](docs/requirements/invariant-register.md).
4. **For UI**, read the local `.dc.html` design references in [`ACMP product context/`](ACMP%20product%20context/) **directly with file tools** (INV-014); the Usage Map is the per-screen index. (The original `design-handoff/` Claude-Design route is archived/superseded.)
5. **Decisions** are recorded as ADRs in [`docs/adrs/`](docs/adrs/). Anything unsettled is in `docs/decisions/open-question-register.md` — these are flagged, never hidden.

**Conventions.** Facts are cited. Recommendations are labelled **Recommendation**. Unverified claims are marked `[unverified]`. Assumptions are `ASM-###` and listed in `docs/risks/risk-register.md`. Nothing in this package should be treated as code-ready until its acceptance criteria (`docs/validation/acceptance-criteria.md`) are defined.

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
| 1 | Executive summary | `docs/00-charter.md` |
| 2 | Understanding of org & problem | `docs/domain/organization-and-problem.md` |
| 3 | Current-state process analysis | `docs/domain/current-state.md` |
| 4 | Pain-point analysis | `docs/domain/pain-points.md` |
| 5 | Stakeholder & user-role analysis | `docs/domain/stakeholders.md` |
| 6 | Product vision | `docs/domain/product-vision-and-principles.md` |
| 7 | Product principles | `docs/domain/product-vision-and-principles.md` |
| 8 | Scope definition | `docs/domain/scope-and-out-of-scope.md` |
| 9 | Out-of-scope definition | `docs/domain/scope-and-out-of-scope.md` |
| 10 | Functional requirements | `docs/requirements/functional.md` |
| 11 | Non-functional requirements | `docs/requirements/non-functional.md` |
| 12 | Topic taxonomy | `docs/domain/topic-taxonomy.md` |
| 13 | Permission & role matrix | `docs/domain/permission-role-matrix.md` |
| 14 | Domain model | `docs/domain/domain-model.md` |
| 15 | Entity lifecycle models | `docs/domain/entity-lifecycles.md` |
| 16 | Workflow definitions | `docs/domain/workflows.md` |
| 17 | Information architecture | `docs/domain/information-architecture.md` |
| 18 | Sitemap | `docs/domain/information-architecture.md` |
| 19 | Page & screen inventory | `docs/domain/information-architecture.md` |
| 20 | Architecture recommendation | `docs/domain/architecture-detail.md` |
| 21 | Architecture diagrams | `docs/domain/architecture-detail.md` |
| 22 | Module definitions | `docs/domain/architecture-detail.md` |
| 23 | Data architecture | `docs/domain/data-architecture.md` |
| 24 | Initial logical data model | `docs/domain/data-architecture.md` |
| 25 | Integration architecture | `docs/domain/integration-architecture.md` |
| 26 | Webex feasibility analysis | `docs/domain/webex-feasibility.md` |
| 27 | Tarseem repo analysis + integration | `docs/domain/tarseem-analysis.md` |
| 28 | Keystone repo analysis + integration | `docs/domain/keystone-analysis.md` |
| 29 | Open-source landscape analysis | `docs/domain/open-source-landscape.md` |
| 30 | Standards & best-practices analysis | `docs/domain/standards-and-best-practices.md` |
| 31 | Build-vs-buy-vs-integrate analysis | `docs/domain/build-vs-buy-vs-integrate.md` |
| 32 | Security threat model | `docs/domain/security-threat-model.md` |
| 33 | Security-control plan | `docs/domain/security-controls.md` |
| 34 | Audit & records-management plan | `docs/domain/audit-and-records.md` |
| 35 | Reporting & dashboard specification | `docs/domain/reporting-dashboards.md` |
| 36 | Metrics & KPI catalog | `docs/domain/metrics-kpi-catalog.md` |
| 37 | Notification strategy | `docs/domain/notification-strategy.md` |
| 38 | Search & traceability model | `docs/domain/search-and-traceability.md` |
| 39 | Testing strategy | `docs/validation/test-strategy.md` |
| 40 | DevSecOps plan | `docs/domain/devsecops-plan.md` |
| 41 | Containerization & deployment plan | `docs/domain/deployment.md` |
| 42 | Repository structure | `docs/domain/repository-structure.md` |
| 43 | Documentation plan | `docs/domain/documentation-plan.md` |
| 44 | Phased roadmap | `docs/planning/roadmap.md` |
| 45 | Prioritized implementation backlog | `docs/execution/backlog.md` |
| 46 | Epics & features | `docs/planning/work-breakdown.md` |
| 47 | User stories for initial release | `docs/domain/user-stories-mvp.md` |
| 48 | Acceptance criteria | `docs/validation/acceptance-criteria.md` |
| 49 | Risks, assumptions, constraints, dependencies | `docs/risks/risk-register.md` |
| 50 | Open decisions requiring org input | `docs/decisions/open-decision-register.md` |
| 51 | ADRs for major decisions | `docs/adrs/adr-0001…adr-0022` (incl. ADR-0013 self-contained, ADR-0015 self-hosted Keycloak) |
| 52 | Claude Design input package | `design-handoff/claude-design-input-package.md` *(archived — superseded by direct-read of `ACMP product context/`)* |
| 53 | Claude Design prompts | `design-handoff/claude-design-prompts.md` *(archived)* |
| 54 | Claude Code execution package | distributed post-migration: `AGENTS.md` + `docs/handoff/initial-prompt.md` + the registers *(original file in git history)* |
| 55 | Initial Claude Code prompt | `docs/handoff/initial-prompt.md` |
| 56 | Follow-up prompts per phase | `docs/handoff/follow-up-prompts.md` (per-slice prompts; ladder in `docs/planning/roadmap.md`) |
| 57 | Definition of Done | `docs/execution/definition-of-done.md` |
| 58 | Release-readiness checklist | `docs/execution/checkpoints.md` |
| 59 | Post-release governance & operating model | `docs/domain/post-release-operating-model.md` |
| + | Agent guardrails | `docs/requirements/invariant-register.md` (`INV-001…014`; former guardrails 1:1) |
| + | Generated-repo agent context | `CLAUDE.md` → `AGENTS.md` |

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

Rule (enforced): a module may not read another module's tables; modules communicate via in-process public contracts (interfaces / MediatR) only. See ADR-0001, `docs/domain/architecture-detail.md`.

### C. Canonical roles (global RBAC) + per-topic capabilities (ABAC)

**Global roles:** `Chairman`, `Secretary` (display name: **Secretary of the Committee** — *أمين سر اللجنة*; the committee lead / primary user; renamed from "Coordinator" on 2026-06-25), `Member`, `Reviewer`, `Auditor`, `Administrator`, `Submitter` (stream requester), `Guest/Presenter` (time-boxed).
**Per-topic capabilities (relationship-based, not global roles):** `Owner`, `Assignee/Contributor`, `Presenter`.
Authorization = role policy **+** topic/stream scope (ABAC). **Roles are sourced from Keycloak group/realm-role claims** and mapped to the canonical roles. **Default stream visibility: all committee members may READ topics across all streams**; create/edit is restricted by role + ownership. **Single committee** (not generalized to multiple). Full matrix in `docs/domain/permission-role-matrix.md`.

### D. Canonical topic taxonomy (kept deliberately small)

**Topic Type (4):** `ResearchDiscovery`, `ArchitectureDecision`, `EnhancementInnovation`, `GovernanceStandardization`.
**Urgency (attribute, not a type):** `Normal | Urgent | Critical` (drives SLA). *Rationale: "Urgent" is a handling speed, not a kind of work — modelling it as an attribute avoids type explosion.* Full taxonomy + devil's-advocate in `docs/domain/topic-taxonomy.md`.

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
`FR-###` functional req · `NFR-###` non-functional req · `CON-###` constraint · `ASM-###` assumption · `DEP-###` dependency · `OQ-###` open question · `DEC-###` planning decision · `ADR-####` decision record · `RISK-###` risk · `HYP-###` hypothesis · `AC-###` acceptance criterion · `PH-#` phase · `EPIC-##` · `US-###` user story · `KPI-##` metric · `W-##` workflow (defined in `docs/domain/workflows.md`).
Statuses: `Draft → Proposed → Approved | Rejected | Superseded | Deferred → Implemented`. A *proposed* item is never rendered as *approved*.

**Runtime (in-app) entity keys** (human-readable, year-scoped): `TOP-YYYY-###` topic · `MTG-YYYY-###` meeting · `AGN-YYYY-###` agenda · `MIN-YYYY-###` minutes · `VOTE-…` · `DECN-YYYY-###` committee decision · `ACT-…` action · `RSK-…` risk · `DPN-…` dependency edge · `ADR-…` in-app ADR · `AIV-…` architecture invariant · `DOC-…` · `TPL-…` template · `DGM-…` diagram · `RMS-…` research mission · `FND-…` finding · `REC-…` recommendation. (Note: in-app ADRs and the product's "Architecture Invariant" `AIV-` are distinct from this package's planning `ADR-####` usage.)

### G. Glossary (committee terms; EN ↔ AR pairing finalized in design handoff)

Architecture Committee · Backlog · Topic · Agenda · Meeting · Minutes (MoM) · Decision · Vote · Quorum · Action · Risk · Dependency · ADR (Architecture Decision Record) · Architecture Invariant · Principle · Standard · Stream · System/Service · Research Mission · Finding · Recommendation · Traceability. Distinctions between *principle / standard / policy / constraint / invariant / decision / ADR* are defined once in `docs/domain/standards-and-best-practices.md` §"Concept disambiguation" and must not be duplicated.

---

## Guiding principles for this package (and the build)

1. **Architecture governance, not project management.** Every feature must serve committee governance/traceability; reject generic PM creep.
2. **Modular monolith first.** No distributed architecture without a demonstrated, measured need.
3. **SQL Server is enough.** No second datastore without evidence.
4. **Self-contained, but don't reinvent solved problems.** The platform is **self-hosted and does not depend on the org's runtime infrastructure** (no shared Hangfire / ELK / Seq / notification platform — see CON-001). It builds its own background processing, observability, and notification channels using standard open-source libraries; it integrates **Tarseem** (diagrams) and **optionally Keystone** (research/discovery — not required), and self-hosts identity via **Keycloak (OIDC)** (ADR-0015 — bundled container, ACMP-owned realm).
5. **Human-reviewed automation.** AI-extracted transcript content is *candidate* until a human approves it.
6. **Auditable & immutable where it matters.** Votes and issued decisions cannot be silently changed.
7. **Bilingual and RTL are first-class**, not bolted on.
8. **Progressive delivery.** Prove the core committee loop (intake→decision→action) before expanding.
9. **Explicit domain concepts over generic abstraction.** Prefer named committee concepts (Topic, Decision, ADR, Invariant) to speculative generic frameworks; if reaching for an enterprise pattern, justify it against an actual requirement or don't add it. *(Ending reconstructed 2026-07-06 — the original line was truncated in every historical copy; wording follows the execution package §15 and INV-012.)*