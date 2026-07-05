# ACMP — Shared Context Digest (for document authors)

This is the shared knowledge base for everyone writing ACMP planning documents. **Read `../README.md` (canonical reference) and this file before writing.** Use canonical roles/modules/entities/IDs/status models from the README exactly. Do not contradict settled decisions; if you disagree, raise it as an `OQ-` in `docs/decisions/open-decision-register.md` rather than silently diverging.

## Writing style (engineering / execution audience)
- Dense and concise. No filler, no marketing tone. Remove any sentence that doesn't add information.
- Use tables and tight lists for structured data (requirements, matrices, fields, statuses). Use prose for reasoning, trade-offs, and recommendations.
- Label **Recommendation**, mark `[unverified]` for anything not from a cited source, and tie assumptions to `ASM-###`.
- Every doc starts with a one-line purpose and ends with a short "Traceability" note (what it links to). Keep cross-references as relative links.
- For each major recommendation use the decision pattern: Problem → Constraints → Options → Trade-offs → Recommendation → Why-here → Risks → Validation. Apply a devil's-advocate challenge to big choices.

---

## 0. RESOLVED DECISIONS (2026-06-24) — these OVERRIDE anything below
The secretary confirmed these; treat as settled canon (mirrors README §A):
1. **Self-contained (CON-001):** no org Hangfire/ELK/Seq/notification platform. ACMP bundles its own.
2. **Identity:** **Keycloak (OIDC)** for SSO; **committee roles supplied via Keycloak group/realm-role claims**, mapped to ACMP roles. No self-registration.
3. **Observability:** self-hosted **Seq** (+ Serilog + OpenTelemetry), app-owned container.
4. **Background jobs:** **app-owned Hangfire on ACMP's own SQL** (not the org's). Outbox for durability.
5. **Object storage:** self-hosted **MinIO** (S3-compatible) via `IFileStore`.
6. **Deployment:** **on-prem VM(s) + Docker Compose**, no Kubernetes.
7. **Scale:** **on-prem, low traffic, ≤20 total users.** Right-size: no HA cluster, no horizontal scaling, no heavy perf engineering. Availability target 24×7/99.9% via simple redundancy + nightly backups.
8. **Notifications:** **in-app notification center only in v1 (no email)**; **Webex adapter = Phase 2**; email later when an SMTP relay exists.
9. **Recordings/transcripts:** store **references + manual upload**; auto-fetch from Webex later.
10. **AI extraction** of decisions/actions/minutes: **Phase 3 only**, manual first, always human-reviewed.
11. **Voting:** **always attributed** (no anonymity in v1); chairman approval/override recorded by name.
12. **Committee:** **single** committee; do NOT generalize to multiple/sub-committees.
13. **Stream visibility:** **all committee members may READ all streams**; write restricted by role/ownership.
14. **Retention:** **keep everything, configurable, no auto-purge in v1.** Votes/decisions/ADRs/minutes immutable.
15. **Dates:** **Gregorian only** (localized formatting; no Hijri in v1).
16. **Keystone:** **OPTIONAL** capability; Research module fully works standalone.
17. **Tarseem:** integrated as a **Phase-2 containerized render sidecar**; JSON spec is the source of truth.

## 1. Organizational & technical context (condensed)
Large national government organization; nationwide one-stop digital platform. Started as a COVID-19 emergency initiative (permits, tracking, vaccination records) in startup/firefighting mode with weak architecture governance; now scaling to the national one-stop app for government + private-sector services via native modules, embedded services, and external integrations.

Tech estate (the *governed* environment, not this platform's stack): native iOS + Android apps; .NET backend; microservices; **Apigee** API gateway; internal auth service migrating to **Keycloak**; **Redis**; many **MS SQL Server** clusters; limited **PostgreSQL**; **ELK + Seq** observability; **Hangfire** background jobs; **RabbitMQ + Kafka**; private networks/VPNs for gov + private-sector integration; DevSecOps pipelines; centralized **notification platform** (Email/SMS/Firebase); embedded services + embedded web server inside the mobile app; embedded-services lifecycle with external partners.

**Implication (CON-001 — self-contained, MANDATORY):** ACMP is **self-hosted and does NOT depend on the org's shared runtime infrastructure** — no org Hangfire, no org ELK/Seq, no org notification platform. It builds and bundles its **own** background processing, observability/logging, and notification channels in its own containers. Two deliberate, allowed exceptions: it uses **SQL Server** (the mandated datastore, app-owned instance) and **federates identity to Keycloak via OIDC** (consume SSO; do not build an IdP). **Webex** is an external SaaS integration via a pluggable adapter (allowed). The committee tool is low-traffic, internal, high-sensitivity, so its footprint stays small. *[Updated by ADR-0015: Keycloak + SQL Server now bundled; zero external runtime services]*

## 2. Org structure & delivery model
Stream-based + Agile. CEO → VP → General Managers (Technology, Business, Delivery, Quality, Operations) → Stream Directors (Technical / Delivery / Business) → Engineering Teams (iOS, Android, .NET, QC). **5 streams**, each a business/technical domain containing multiple services. A service may include backend APIs, background workers, databases, native mobile modules, embedded-service modules, external integrations, observability components.

## 3. Current Architecture Committee (the thing being governed)
- Members: VP (**chairman**), all technical directors, selected senior engineers, **iOS SME**, **Android SME**, invited specialists.
- Cadence: weekly today; **considering bi-weekly**.
- Backlog: a **text file**. Each week 1+ topics are selected for presentation/discussion/decision.
- Decisions via **voting + chairman final approval** (chairman has stronger authority).
- A topic has one **owner** + optional members; may affect one/many streams, shared platforms, mobile, backend, infra, security, external partners, gov integrations, org-wide principles.
- End of meeting: backlog updated; topics modified/added; decisions recorded; actions created; **MoM** prepared; topics approved/rejected/deferred/returned/converted.
- Backlog sources: committee members, stream business/technical requests, urgent org needs, operational incidents, security findings, modernization, innovation, cross-stream problems, regulatory/external.
- Decision outcomes (canonical list in README §E): Approved, Rejected, More-info, Feedback, Enhancements, Design-changes, Research, Deferred, Conditionally-approved, Escalated, Converted (to execution / research / ADR).
- Topic fields (current/needed): title, description, scope, type, source, created/target/scheduled dates, status, priority, owner, assignees, affected streams, affected systems/services, dependencies, risks, notes, comments, feedback, supporting research, attachments, presentations, diagrams, decisions, voting results, follow-up actions, due dates, progress, related ADRs, related invariants.

## 4. Functional scope (initial) → maps to canonical modules
Identity/Access/Membership; Topic submission/intake; Backlog management (list/table/kanban/calendar/timeline, DnD prioritization, aging); Agenda management (DnD, time-box, presenter, publish, carry-over); Meeting & MoM (attendance, notes, MoM versioning/approval, recordings/transcripts, transcript search, AI candidate extraction with human review); Decision management (rationale, alternatives, conditions, authority, supersede, → ADR); Voting (eligible voters, quorum, abstain, anonymity, chairman approval, immutable audit, conflict-of-interest); Actions/follow-up (owners, due, progress, reminders, escalation, completion validation); Risk management; Dependency management (graph, blocked work, cross-stream impact); ADRs (lifecycle, templates, repository); Architecture invariants (categories, scope, exceptions, violations); Template management; Documentation/wiki (Markdown, versioning, cross-links); Diagrams (Tarseem); Research/planning (Keystone); Dashboards/reporting (high priority); Notifications (Webex as adapter); Search & end-to-end traceability.

UX: simple for average users; minimal steps; DnD where it helps + keyboard-accessible alternatives; light/dark; EN + AR full RTL; consistent bilingual terminology; sitemap; role-based nav; responsive (desktop + tablet); clear statuses; understandable (not overwhelming) traceability; prevent lost unsaved work; accessible errors/validation; recognized accessibility practices.

Stack constraints (settled — see README §A): .NET/ASP.NET Core, Clean Architecture, REST, background processing, structured logging, distributed tracing, health checks, API docs, automated tests; React + TS, EN/AR, RTL, light/dark, accessible DnD, responsive; **MS SQL Server**; containerized/Docker, externalized config, secrets, health/readiness, migrations, backup/restore. **Do not** add Kubernetes, service mesh, brokers, distributed DB, microservices unless a clear requirement justifies it. **Start with a modular monolith** as the default hypothesis and confirm it.

---

## 5. RESEARCH FINDINGS (cite these; they are primary-source-checked)

### 5.1 Tarseem — `github.com/A-H-911/tarseem` (inspected 2026-06-24)
- **What:** Schema-driven **Python** diagram engine (Python ≥3.10, **Apache-2.0**, **v1.0.0 released 2026-06-17**, schema frozen at v1.0). Validated JSON spec → publication-quality diagram. Thin orchestration over mature engines (ELK layout, own SVG renderer, Chromium for raster/PDF).
- **Families (11):** flowchart, architecture/C4, dependency, swimlane, sequence, ER, state, deployment, UML class, mindmap, activity.
- **Exports:** SVG (canonical), PNG, PDF, **draw.io** (editable mxGraph), **PPTX** (editable). Every writer emits a **capability report** (never silent drops).
- **Arabic/RTL first-class:** HarfBuzz shaping before layout; RTL = geometry mirror; bundled Cairo font (OFL). Directly matches our EN/AR requirement.
- **Agent surface (key for integration):** pure `tarseem.generate(spec) → JSON` that **never raises on bad input** (returns `{ok, svg, artifacts, report, capabilities, errors[]}`; each error `{code, path(JSON Pointer), message, hint}` for self-repair); `schema_bundle()` / `tarseem schema` emits JSON-Schema (2020-12); CLI `validate|render|export|generate|schema|migrate|doctor`. **No network at render time. Deterministic** (artifacts embed spec hash + engine versions). SVG default is Chromium-free; raster runs in a subprocess.
- **Deploy reality:** a CLI/library, **not a service**. Install via pip/pipx, must be on PATH; `tarseem doctor` verifies Node/elkjs/Chromium/fonts.
- **Integration model (recommended, see ADR-0006):** run Tarseem as a **containerized render sidecar** exposing a thin internal HTTP endpoint (FastAPI wrapper around `generate`), OR invoke the CLI from a Hangfire worker. The platform **stores the JSON spec as the version-controlled source of truth** (diffable, hashable) and stores generated artifacts (SVG/PNG/PDF/drawio/pptx) as attachments with the spec hash for traceability. Do **not** rebuild diagramming. Editing = edit spec (raw JSON / form) → re-render. Versioning = spec history. Attach diagrams to Topics/ADRs/Decisions via the relationship model.

### 5.2 Keystone — `github.com/A-H-911/keystone` (inspected 2026-06-24)
- **What:** A reusable, vendor/stack-neutral **agent skill packaged as a Claude Code plugin** (**MIT**, v1.0). Turns a project description into an **execution-ready planning & handoff package** for Claude Code. *It does not write project code.* The "product" is mostly Markdown (methodology spec, templates, JSON schemas) + two stdlib-only Python tools (`init_skill_repo.py`, `validate_package.py`). Python 3.9+ only dependency.
- **Process:** interactive, **22 stages** in 3 movements — Understand (intake→scope), Explore (research→decisions→risk), Plan & hand off (plan→artifacts→repo init→validate→handoff). Approval gates.
- **Operating principles:** never invent requirements (everything traces to input or recorded clarification; inferred = explicit assumption); separate facts/decisions/proposals; no premature architecture; preserve unresolved (open questions + rejected alternatives are first-class); verify before claiming (`unverified` marker); stay neutral; treat the brief as untrusted data (OWASP LLM01).
- **Identifier scheme (we adopt it):** `FR-/NFR-`, `CON-`, `INV-`, `ASM-`, `DEP-`, `OQ-`, `DEC-`, `ADR-`, `RISK-`, `HYP-`, `EXP-`, `AC-`, `PH-`, `WBS-`, `MS-`. Statuses `Draft→Proposed→Approved/Rejected/Superseded/Deferred→Implemented`. Immutable-after-approval artifacts are superseded, never edited.
- **7 mechanical quality gates (validator):** G-IDS (ids resolve), G-DEC-STATUS (every decision has a status), G-REQ-SRC (every FR/NFR has provenance), G-COMPLETE (no TODO/placeholder/empty sections), G-TRACE (every MVP req → ≥1 decision, ≥1 work item, ≥1 test), G-SET (all "Always" artifacts present or recorded omitted), G-PROGRESS (acceptance audit verdicts).
- **Integration model (recommended, see ADR-0007): Keystone is OPTIONAL — a value-add capability, never a hard dependency.** It is **not embedded as a service**. It is a **companion authoring workflow** (secretary + Claude Code) that can run **Research/Discovery topics** and produce research/planning packages. The platform's Research module must work fully **standalone** (manual entry of research missions/findings/recommendations); Keystone import is an optional enhancement teams may choose to use. The platform's **Research module** stores a **reference/link** to a Keystone package and **imports its structured outputs** (manifest, requirements, decisions, risks, acceptance criteria, traceability matrix) as first-class artifacts that map onto our domain (Finding, Recommendation, Research Mission). The platform **adopts Keystone's ID scheme and gate philosophy**. This very ACMP package is itself a Keystone-style package.

### 5.3 Webex (official developer docs — developer.webex.com)
- **Rate limits:** most REST APIs ~**300 req/min**; `/people` and `/messages` higher and dynamically adjusted; limits **shared per user**; on limit → **429 + Retry-After**. **Bot accounts** have less restrictive limits but content-ownership caveats. Source: developer.webex.com/blog/rate-limiting-and-the-webex-api ; developer.webex.com/docs/rest-api-basics.
- **Webhooks:** HTTP POST JSON on events; supported resources include meetings, recordings, convergedRecordings, meetingParticipants, **meetingTranscripts**, rooms, messaging, adminBatchJobs. Source: developer.webex.com/docs/api/v1/webhooks ; developer.webex.com/admin/docs/api/guides/webhooks.
- **Recordings API:** retrieve via downloadUrl/playbackUrl. Source: developer.webex.com/admin/docs/api/v1/recordings.
- **Transcripts API (critical constraint):** transcripts require **Webex Assistant turned ON**, which **cannot be enabled programmatically** — host/participant or Control Hub default only. Machine-readable speaker-attributed content via the **snippets API**. Source: developer.webex.com/docs/api/v1/meeting-transcripts ; access-meeting-resources guide.
- **Messaging / Buttons & Cards:** interactive cards use **Microsoft Adaptive Cards** spec; **Webex supports Adaptive Cards v1.3**; card JSON+images should stay **≤80 KB**, **≤10 image links**. Source: developer.webex.com/messaging/docs/buttons-and-cards.
- **Auth:** OAuth2 integrations (user-scoped) or bot tokens. **Feasibility takeaway:** scheduling/invitations/metadata/recordings/webhooks/messaging+cards are viable; **transcript automation is gated by Webex Assistant** and licensing/privacy — do not assume it. Treat all Webex features behind the notification/integration **adapter** so the platform never hard-depends on Webex.

### 5.4 Open-source landscape (for `docs/domain/open-source-landscape.md` + build-vs-buy)
- **ADR tooling:** **Log4brains** (docs-as-code ADR mgmt + static site, **MADR** default template, timeline, search; thomvaill/log4brains). **adr-tools** (bash, Nygard format). **dotnet-adr** (.NET global tool). **MADR** template at adr.github.io/madr. Canonical hub: adr.github.io. *Reusable ideas, not a platform to adopt wholesale.*
- **Backstage** (backstage/backstage; CNCF incubation; created at Spotify; 3,400+ orgs): software **catalog/entity model**, **TechDocs** (docs-as-code Markdown), software templates, plugin architecture (200+). *Heavy (React+Node IDP); reuse the **entity-catalog and docs-as-code ideas**, not the framework — adopting Backstage to run a weekly committee is overkill and off-mission.*
- **Structurizr / C4** (c4model.com; Structurizr DSL): architecture-as-code; relevant as a **comparator to Tarseem** (we already have Tarseem — do not add Structurizr).
- **Board/committee/governance SaaS** (BoardEffect, OnBoard, Diligent, Decisions for Teams, Hugo, Fellow): commercial, cloud, not deployable on-prem in a sensitive gov network, not architecture-specific → **learn from, don't adopt**.
- **Conclusion:** no single OSS/commercial product covers architecture-committee governance + traceability + EN/AR/RTL + on-prem gov constraints. **Build the thin domain core; integrate Tarseem + Keystone + reuse org infra.** Adopt patterns (docs-as-code, catalog, MADR), not platforms.

### 5.5 Standards & frameworks (for `docs/domain/standards-and-best-practices.md`)
- **ISO/IEC/IEEE 42010:2022** — architecture description (stakeholders, concerns, viewpoints). Use its vocabulary for the Governance/ADR/Invariant model. (quality.arc42.org/standards/iso-42010 ; iso.org/standard/50508.html — note 2022 supersedes 2011.)
- **arc42** — 12-section pragmatic architecture-doc template; we structure `docs/domain/architecture-detail.md` along arc42. (arc42.org)
- **C4 model** — System Context / Container / Component / Code (+ dynamic/deployment); our diagrams use C4 levels (rendered via Tarseem's architecture/C4 family). (c4model.com)
- **ADR formats** — **MADR** (recommended template) vs **Nygard** (lightweight). Recommend MADR-lite for the in-app ADR template. (adr.github.io)
- **OWASP ASVS 5.0** (May 2025; ~350 reqs, 17 chapters, modular, levels L1/L2/L3) — target **L2** for this sensitive internal system. (owasp.org/www-project-application-security-verification-standard ; github.com/OWASP/ASVS)
- **OWASP Top 10** + **ASVS** + **OWASP LLM Top 10** (LLM01 prompt injection — relevant to transcript/AI extraction). 
- **WCAG 2.2 AA** — accessibility target (W3C Recommendation). [verify exact criteria when writing a11y reqs]
- **.NET architecture** — **Modular monolith + Clean Architecture + vertical slice** is well-supported and Microsoft-endorsed in community/On-.NET guidance (Ardalis/Steve Smith; milanjovanovic.tech; antondevtips.com). Modules talk only via public APIs; no cross-module DB access. Supports ADR-0001/0002.
- **Records management / auditability** — align audit + retention with general records-management practice and the org's gov requirements; immutability for votes/decisions.

### 5.6 SQL Server sufficiency (for `docs/domain/data-architecture.md` + ADR-0003/0011)
- **Full-text search:** SQL Server FTS is adequate for moderate search (topics, docs, transcripts) but weaker than Elasticsearch on fuzzy/typo/autocomplete/customization. For this app's scale it is **sufficient in v1**; if search outgrows it, stand up the platform's **own self-hosted search** (e.g., an OpenSearch container, app-owned) — **not** the org's ELK. (airbyte/influxdata/medium comparisons; mauridb "From ElasticSearch back to SQL Server".)
- **JSON:** SQL Server has native JSON support (good for storing Tarseem specs + flexible metadata).
- **Reporting/analytics:** **columnstore indexes** handle dashboard/reporting loads well; SSAS/SSRS exist if needed. Reporting via read models + columnstore is sufficient — **no separate analytics DB** in v1.
- **Conclusion:** **single SQL Server** covers transactional + reporting + search + traceability for v1. Revisit only with measured evidence.

---

## 6. Citation URLs (use as needed)
- Webex: https://developer.webex.com/blog/rate-limiting-and-the-webex-api · https://developer.webex.com/docs/api/v1/webhooks · https://developer.webex.com/admin/docs/api/v1/recordings · https://developer.webex.com/docs/api/v1/meeting-transcripts · https://developer.webex.com/docs/api/guides/access-meeting-resources-guide · https://developer.webex.com/messaging/docs/buttons-and-cards
- ADR/EA: https://adr.github.io · https://adr.github.io/madr/ · https://github.com/thomvaill/log4brains · https://quality.arc42.org/standards/iso-42010 · https://arc42.org · https://c4model.com
- Backstage: https://backstage.io · https://github.com/backstage/backstage
- OWASP: https://owasp.org/www-project-application-security-verification-standard/ · https://github.com/OWASP/ASVS
- .NET: https://learn.microsoft.com/en-us/shows/on-dotnet/on-dotnet-live-clean-architecture-vertical-slices-and-modular-monoliths-oh-my · https://www.milanjovanovic.tech/blog/vertical-slice-architecture-dotnet
- Repos: https://github.com/A-H-911/tarseem · https://github.com/A-H-911/keystone
