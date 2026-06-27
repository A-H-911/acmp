# 23 — Build vs. Buy vs. Integrate Analysis (Deliverable 31)

**Purpose:** Apply the decision pattern (Problem → Constraints → Options → Trade-offs → Recommendation → Why-here → Risks → Validation) to the ACMP platform as a whole and to each major component, producing a justified verdict table and a rationale for the modular-monolith + reuse-companion-tools strategy.

---

## 1. Decision Pattern (Applied Uniformly)

Each decision below follows this structure:

1. **Problem** — what capability is needed
2. **Constraints** — what limits the option space (non-negotiable)
3. **Options** — realistic candidates evaluated
4. **Trade-offs** — meaningful differences between options
5. **Recommendation** — the verdict with label (Build / Buy / Integrate / Reuse-partially)
6. **Why-here** — why this specific context makes that verdict correct
7. **Risks** — risks accepted with the chosen option
8. **Validation** — how to confirm the choice is working (or revisit it)

---

## 2. Platform-Level Decision

### 2.1 Overall Platform Strategy

**Problem:** The Architecture Committee currently tracks its backlog in a text file. There is no single system of record linking topics, meetings, votes, decisions, ADRs, actions, risks, dependencies, and traceability. No existing product covers this exact governance workflow with EN/AR/RTL + on-prem + government sensitivity constraints.

**Constraints:**
- CON-001: self-contained; no org shared runtime infra
- SQL Server mandated
- On-prem VM + Docker Compose; no cloud
- EN/AR bilingual + RTL first-class
- ≤20 users; low traffic; right-sized
- Single committee; architecture governance, not general PM
- Budget/timeline: single team, progressive delivery

**Options:**

| Option | Description |
|---|---|
| A — Buy commercial SaaS | BoardEffect, OnBoard, Diligent, Fellow, Notion, Jira |
| B — Adopt OSS platform | Backstage, Outline + custom plugins |
| C — Fork an OSS tool | Fork Log4brains or Backstage and extend |
| D — Build modular monolith | Custom .NET + React; integrate specific tools for identity, diagramming, observability |

**Trade-offs:**

| Option | Fit for gov/on-prem | EN/AR RTL | Architecture-specific workflow | Time to first value | Long-term control | Cost |
|---|---|---|---|---|---|---|
| A — SaaS | None fit on-prem | Weak/none | Generic | Fast (if it fit) | None | Recurring licence |
| B — OSS platform | Partial (self-hostable) | Weak | None (generic dev portal) | Slow (heavy customization) | Limited | Dev cost + complexity |
| C — Fork | Self-hostable | Build it yourself | Start from wrong domain | Slow (fight the existing model) | Forks diverge | Dev cost + maintenance debt |
| D — Build | Complete | First-class | Purpose-built | Phased; core in ~3 months | Full | Dev cost; no licence |

**Recommendation: D — Build a modular monolith; integrate specific proven tools (Keycloak, Tarseem, MinIO, Seq, Hangfire, TipTap); reuse patterns (MADR, C4, docs-as-code); treat Keystone as optional companion.**

**Why-here:** No off-the-shelf product satisfies the intersection of architecture-committee governance workflow + traceability graph + EN/AR RTL + on-prem SQL Server + government sensitivity. The governance domain is specific enough that every generic tool requires more customization than building the thin domain core. The team is already .NET/React. The scale (≤20 users) means the risk of a custom build is low.

**Devil's-advocate challenge:** "Why not Backstage with plugins?" Backstage is a developer portal (IDP), not a committee governance tool. It requires Node.js + PostgreSQL (stack mismatch), has no Arabic/RTL, no committee workflow, no voting, no traceability graph. Building committee governance as Backstage plugins would require writing every feature from scratch in a foreign stack while fighting the platform's IDP assumptions. The result would be harder to maintain than a clean .NET/React monolith. Backstage is the right tool for developer self-service portals; it is the wrong tool for architecture committee governance.

**Risks:**
- Build cost: mitigated by phased delivery (core loop first) and right-sizing (no over-engineering)
- Bus factor: mitigated by docs-as-code, CLAUDE.md, planning package as persistent context
- Feature creep: mitigated by README §G guiding principle "architecture governance, not project management"

**Validation:** Core committee loop (topic intake → backlog → agenda → meeting → vote → decision) functional by end of Phase 1. Adoption rate and secretary feedback by end of Phase 2.

---

## 3. Component-Level Decisions

### 3.1 Committee / Meeting / Agenda / Voting Domain

**Problem:** The core of ACMP — committee membership, meeting scheduling, agenda management, meeting facilitation, voting, chairman override, and MoM — is purpose-built committee governance with no adequate off-the-shelf equivalent.

**Constraints:** All general (CON-001, SQL Server, on-prem). Votes are immutable (ADR-0009). Voting is always attributed, never anonymous (resolved decision §0.11). Single committee (resolved §0.12).

**Options:**

| Option | Detail |
|---|---|
| Buy | BoardEffect, OnBoard, Diligent — SaaS only; not on-prem; not architecture-specific |
| Integrate | No existing self-hostable committee tool fits |
| Build | Custom .NET modules: Membership, Meetings, Agenda, Voting |

**Trade-offs:** No buy/integrate option is available within on-prem + sensitivity constraints. Build is the only viable path.

**Recommendation: Build.** Membership, Meetings, Agenda, and Voting modules are core domain; implement from scratch.

**Why-here:** The voting model (eligible voters, quorum, abstain, chairman override, immutability) is specific to this committee's governance. Any off-the-shelf tool would require working around its assumptions.

**Risks:** Vote integrity bugs; quorum logic errors. **Mitigated by:** dedicated test coverage for vote flow (unit + integration); immutability enforced by DB constraints + EF Core interceptors; explicit acceptance criteria (`docs/40`).

**Validation:** Voting integration tests cover quorum edge cases, abstention, and chairman override scenarios before Phase 1 release.

---

### 3.2 Backlog / Topic Management

**Problem:** The committee needs a structured, auditable backlog of topics with rich metadata, status lifecycle, DnD prioritization, stream assignment, and kanban/timeline views.

**Constraints:** Topics are the central entity; all other modules (meetings, decisions, ADRs, actions, risks) reference topics. Topic status lifecycle is specific to committee governance (see README §E).

**Options:**

| Option | Detail |
|---|---|
| Buy/integrate | Jira, Linear, Asana — generic PM tools; cloud; no traceability to ADRs/decisions; no committee governance context |
| Build | Topics module as core domain entity |

**Recommendation: Build.** Topics module is the domain anchor; its status model and relationships are committee-specific and must be owned by ACMP.

**Why-here:** A topic in ACMP is not a generic ticket. It has committee-specific statuses (Triage, Accepted, InCommittee, Decided), typed relationships (to Meetings, Decisions, ADRs, Actions, Risks, Dependencies), and Arabic/bilingual metadata. No generic PM tool models this without massive customization that would exceed the cost of building it.

**Risks:** Topic status lifecycle is complex (9 main statuses + side states + conversion); logic errors create orphaned topics or incorrect traceability. **Mitigated by:** explicit state machine in domain layer; lifecycle diagrams in `docs/12-entity-lifecycles.md`; unit tests for each transition.

**Validation:** All lifecycle transitions tested and auditable. Topics correctly drive agenda and MoM generation.

---

### 3.3 ADR (Architecture Decision Record) Module

**Problem:** The committee needs to create, review, approve, and link Architecture Decision Records to topics, systems, and invariants, with versioning, supersede lifecycle, and MADR-structured content.

**Constraints:** ADRs are immutable after approval (ADR-0009). MADR template is the standard (§B.4 of `docs/22`). ADRs must cross-link to `DECN-`, `AIV-`, `Principle`, and `System` entities.

**Options:**

| Option | Detail |
|---|---|
| Adopt Log4brains | Markdown files in Git, static-site preview; no auth, no workflow, no bilingual support, no integration surface |
| Integrate dotnet-adr | CLI tool for creating Markdown files; no web UI, no workflow |
| Build | Custom ADR module using MADR template, in-app editing, lifecycle management, rich linking |

**Recommendation: Build using MADR pattern; learn from Log4brains; do not adopt either tool.**

**Why-here:** Log4brains is a static-site generator for Git-based ADR files. ACMP needs ADRs as first-class database entities with committee workflow (Proposed → committee review → Approved), voting references, affected-system links, and bilingual labels. These requirements are fundamentally incompatible with a static-site generator approach. Reuse the MADR field structure; implement the module natively.

**Risks:** Template rigidity — if the MADR template fields change or the org wants custom fields, schema migrations are needed. **Mitigated by:** storing ADR body as Markdown (flexible) with structured YAML front-matter (validated); field additions are additive migrations.

**Validation:** ADR lifecycle tested end-to-end (Draft → Proposed → Approved → Superseded). MADR field completeness checked in acceptance criteria. Supersede links verified in traceability graph.

---

### 3.4 Wiki / Documentation Module

**Problem:** The committee needs to maintain living documentation (Architecture Principles, Standards, governance policies, meeting templates, onboarding guides) with Markdown editing, versioning, and cross-links.

**Constraints:** Must use SQL Server (no separate PostgreSQL for Outline); must be bilingual/RTL; must integrate with the traceability graph; BSL licence of Outline is a risk for some gov deployments.

**Options:**

| Option | Detail |
|---|---|
| Deploy Outline | Self-hostable wiki; BSL licence; PostgreSQL-only; RTL partial; good UX |
| Deploy Confluence (Data Center) | Self-hostable; proprietary; expensive; partial RTL |
| Build simple | Lightweight Knowledge module: Markdown pages, nested structure, versioning, search via SQL FTS |
| Embed TipTap | Use TipTap OSS as the editor component within ACMP's own Knowledge module |

**Recommendation: Build a simple Knowledge module; embed TipTap (OSS core) as the Markdown editor.**

**Why-here:** The Knowledge module's requirements are modest — nested Markdown pages, versioning, cross-links to ACMP entities, search. Deploying a full wiki platform (Outline or Confluence) adds a PostgreSQL dependency (Outline) or a proprietary licence (Confluence), both of which conflict with ACMP's constraints. A thin Knowledge module using TipTap for editing and SQL Server for storage is sufficient and keeps the stack consistent.

**Risks:** TipTap OSS core lacks some Pro-extension features (AI suggestions, advanced tables). **Mitigated by:** Pro features are not required; OSS core covers Markdown, headings, lists, links, code blocks, tables, images.

**Validation:** Knowledge pages editable in both EN and AR; RTL text renders correctly; page history retrievable; cross-links to ADRs/Topics render correctly.

---

### 3.5 Diagrams Module

**Problem:** Committee topics and ADRs need architectural diagrams (C4, architecture, deployment, sequence, ER, state, dependency). Creating and versioning diagrams must be integrated into the committee workflow.

**Constraints:** Diagrams must support Arabic labels and RTL layout. JSON spec is the version-controlled source (diff, hash, re-render). No separate Structurizr deployment.

**Options:**

| Option | Detail |
|---|---|
| Deploy Structurizr | Java server; no Arabic RTL; duplicate diagram capability alongside Tarseem |
| Build diagram editor | Full diagram tool — prohibitively complex; years of work |
| Integrate Tarseem (ADR-0006) | Containerized sidecar; JSON spec → SVG/PNG/PDF/draw.io/PPTX; Arabic-first; Apache 2.0 |
| Embed draw.io | Open-source diagram editor; no code/spec-driven workflow; not JSON-spec-as-source |

**Recommendation: Integrate Tarseem (ADR-0006 — settled).** See `docs/19-tarseem-analysis-and-integration.md` for full analysis.

**Why-here:** Tarseem is purpose-built for schema-driven diagram generation with Arabic/RTL as a first-class capability. It covers 11 diagram families including C4/architecture. Integrating it as a sidecar is a thin layer; the platform stores the JSON spec (diffable, hashable) and calls Tarseem to render artifacts. Phase 2 (resolved decision §0.17).

**Risks:** Tarseem is a new project (v1.0.0 — 2026-06-17); early-stage maturity. **Mitigated by:** JSON spec is the source of truth (not Tarseem's internals); if Tarseem is replaced, the specs remain; the `IFileStore` abstraction means artifacts can be regenerated from specs.

**Validation:** Tarseem sidecar renders a C4 system context diagram with Arabic labels before Phase 2 release. Render errors surface via `errors[]` in the response (never silently dropped).

---

### 3.6 Research / Planning Module

**Problem:** `ResearchDiscovery` topics require a structured research workflow: research missions, findings, recommendations, hypotheses, evidence, and handoff to the committee.

**Constraints:** Research module must work fully standalone (resolved decision §0.16). Keystone is optional. Importing Keystone artifacts must be additive, not a hard dependency.

**Options:**

| Option | Detail |
|---|---|
| Hard-dependency on Keystone | Keystone embedded as a service; committee tool breaks without it |
| Integrate Keystone as optional companion (ADR-0007) | Research module standalone; optional import from Keystone-produced artifacts |
| Build standalone only | Research module with no Keystone awareness |

**Recommendation: Build standalone Research module; integrate Keystone as optional (ADR-0007 — settled).** See `docs/20-keystone-analysis-and-integration.md` for full analysis.

**Why-here:** Not all research topics will use Keystone. The committee must be able to create Research Missions, record Findings, and make Recommendations manually. Keystone import is a power-user feature for research-heavy topics where a Secretary ran a Keystone workflow before the meeting.

**Risks:** Keystone artifacts change schema over time; import mapping may break. **Mitigated by:** import is a one-time ingest (not live sync); validate against Keystone's JSON schemas at import time; surface schema-mismatch as a user-visible warning.

**Validation:** Research module fully functional without Keystone installed/configured. Keystone import tested with a sample package; all structured artifacts (findings, recommendations, decisions, risks) map correctly to ACMP entities.

---

### 3.7 Identity / SSO

**Problem:** ACMP needs authentication and authorization for ≤20 internal users. Users must be provisioned (no self-registration). Roles must flow from the identity provider.

**Constraints:** ACMP self-hosts Keycloak (ADR-0015); still no building an IdP — run the proven engine. (Supersedes the earlier "org is migrating to Keycloak; ACMP federates to it" framing of ADR-0004; OIDC + PKCE + claims-based roles + no self-registration remain in force.)

**Options:**

| Option | Detail |
|---|---|
| Build own IdP | ASP.NET Core Identity + JWT issuance; months of security-critical work; no SSO |
| Azure AD / Entra ID | Cloud; violates on-prem constraint for identity |
| Integrate Keycloak (ADR-0004) | OIDC federation; roles via group/realm-role claims; no self-registration; proven, Apache 2.0 |

**Recommendation: Integrate Keycloak (ADR-0004 — settled).**

**Why-here:** ACMP self-hosts Keycloak as a bundled container with an ACMP-owned realm (ADR-0015). ACMP consumes it via OIDC (auth-code + PKCE). Committee roles are supplied via Keycloak group/realm-role claims, mapped to ACMP's canonical roles at the middleware layer. This avoids building an IdP while keeping identity fully self-contained.

**Risks:** Keycloak is now in ACMP's own availability/ops scope (ADR-0015 — upgrades, patching, key rotation, realm/user-store backup are ACMP's). **Mitigated by:** Keycloak is bundled in ACMP's Compose stack and covered by ACMP's own backup + warm-standby + health checks; ACMP caches JWTs for active sessions; graceful error on Keycloak outage.

**Validation:** Login via Keycloak OIDC works. Role claims map correctly to ACMP canonical roles. Token expiry and refresh work correctly. No bypass path to access ACMP without a valid Keycloak session.

---

### 3.8 Notifications

**Problem:** ACMP must notify users of agenda publications, meeting reminders, vote openings, action due dates, ADR reviews, and escalations.

**Constraints:** No email in v1 (no SMTP relay available — resolved decision §0.8). No org notification platform (CON-001). Webex = Phase 2. Notification channel must be abstracted (`INotificationChannel`).

**Options:**

| Option | Detail |
|---|---|
| Use org notification platform | Violates CON-001 |
| Email-only | No SMTP relay available in v1 |
| Webex in v1 | Too early; adapter pattern needed; Phase 2 |
| Build in-app notification center | In-app only; bell icon + notification feed; no external channel in v1 |

**Recommendation: Build in-app notification center for v1; build Webex adapter for Phase 2 (ADR-0005 — settled).**

**Why-here:** The in-app notification center requires only a database table and a WebSocket or polling endpoint — minimal complexity. The channel abstraction (`INotificationChannel`) means the Webex adapter (Phase 2) plugs in without changing the notification logic. This is the right sequencing given the resolved constraints.

**Risks:** Users may not notice in-app notifications if they are not in the browser. **Mitigated by:** ACMP is a synchronous, meeting-driven tool; the weekly/bi-weekly cadence means users check it regularly; Webex adapter in Phase 2 adds push channel.

**Validation:** Notifications appear in the in-app feed for all defined trigger events. `INotificationChannel` interface is testable with a mock; switching to Webex adapter in Phase 2 requires no changes to trigger logic.

---

### 3.9 Search

**Problem:** Users need to search topics, meeting notes, MoM, ADR content, documents, and transcripts (Phase 2+).

**Constraints:** No org ELK (CON-001). SQL Server is the mandated datastore (ADR-0003). Scale: ≤20 users; moderate search volume; no real-time search SLA.

**Options:**

| Option | Detail |
|---|---|
| SQL Server FTS (Full-Text Search) | Built-in; SQL Server CONTAINS/FREETEXT; adequate for moderate search; weaker on fuzzy/autocomplete |
| Self-hosted OpenSearch container | Richer search; adds complexity + resource; app-owned (not org's ELK) |
| Typesense / Meilisearch | Fast, typo-tolerant; additional container; MIT/Apache |
| Integrate org's ELK | Violates CON-001 |

**Recommendation: Build on SQL Server FTS in v1; adopt self-hosted OpenSearch only if FTS demonstrably insufficient (ADR-0011 — settled).**

**Why-here:** At ≤20 users and a content volume measured in hundreds (not millions) of documents, SQL Server FTS is entirely adequate. Adding a search engine in v1 adds deployment complexity, resource overhead, and an index-sync problem without evidence of need. The decision to adopt OpenSearch is deferred to when FTS is measurably insufficient (ADR-0011).

**Risks:** FTS weaker on fuzzy/typo-tolerance; Arabic stemming quality depends on SQL Server's Arabic word-breaker. **Mitigated by:** pilot search with Arabic content during development; if Arabic FTS quality is poor, evaluate Meilisearch (has Arabic tokenizer support [unverified]) as a lower-overhead alternative to OpenSearch.

**Validation:** Search returns relevant results for English and Arabic queries on topics, MoM, and ADR content. Arabic search tested with native-speaker queries before Phase 1 release.

**OQ candidate:** OQ-XXX — Is SQL Server's Arabic word-breaker sufficient for ACMP's search quality requirements? Evaluate with a sample of Arabic topic titles and MoM content.

---

### 3.10 Reporting / Dashboards

**Problem:** The committee needs dashboards (topic throughput, decision rates, action completion, aging backlog) and point-in-time reports (meeting outcomes, ADR registry, KPI trends).

**Constraints:** No separate analytics DB in v1 (resolved — SQL Server covers reporting, ADR-0003). No PowerBI/SSAS/Tableau unless org mandates it.

**Options:**

| Option | Detail |
|---|---|
| PowerBI / Tableau | Cloud BI tools; may have on-prem versions; heavy; overkill for ≤20 users |
| SSRS | SQL Server Reporting Services; on-prem; significant setup; Java/config overhead |
| Build on SQL columnstore + read models | Columnstore indexes handle analytical queries; read models via EF Core; dashboard in React |
| Self-hosted Metabase | Open-source BI; additional container; PostgreSQL or SQL Server supported |

**Recommendation: Build reporting on SQL columnstore indexes + application-layer read models; render dashboards in the React frontend.**

**Why-here:** At this scale, SQL Server columnstore indexes handle all required analytical queries without a separate OLAP or BI layer. Read models (denormalized projections updated by Hangfire jobs or EF Core interceptors) provide dashboard-ready data. This keeps the stack minimal and the reporting tightly integrated with the ACMP UI (role-based, bilingual).

**Risks:** Complex analytical queries may be slow if columnstore optimization is not applied correctly. **Mitigated by:** identify the 5–10 dashboard queries during design; add columnstore indexes on the specific columns queried; test query performance with realistic data volumes.

**Validation:** Dashboard queries return in under 2 seconds on a dataset representing 2 years of committee activity (~500 topics, ~100 meetings, ~200 decisions). If this benchmark is not met, revisit.

---

### 3.11 Object Storage (Files, Attachments, Diagrams, Recordings)

**Problem:** ACMP must store file attachments (presentations, documents), generated diagram artifacts (SVG/PNG/PDF), and references to meeting recordings/transcripts.

**Constraints:** Self-contained (CON-001). On-prem. S3-compatible for portability. Metadata in SQL Server; files not in SQL Server (FILESTREAM was considered and rejected — binary blobs in SQL degrade performance).

**Options:**

| Option | Detail |
|---|---|
| SQL Server FILESTREAM | On-prem; no separate service; known performance limits; not S3-compatible |
| Local file system | Simple; no redundancy; not S3-compatible; tight coupling |
| Org object storage (if any) | Violates CON-001 |
| Self-hosted MinIO | S3-compatible; Docker; Apache 2.0; `IFileStore` abstraction; pre-signed URLs; versioning |

**Recommendation: Integrate self-hosted MinIO (resolved decision §0.5 — settled).**

**Why-here:** MinIO is a proven, lightweight, S3-compatible object store that runs as a Docker container with minimal resource requirements. Pre-signed, time-limited URLs for sensitive files (recordings, confidential attachments) are a first-class feature. The `IFileStore` abstraction means MinIO can be swapped for another S3-compatible store without changing application logic.

**Risks:** MinIO container disk management; backup must include MinIO volume alongside SQL Server volume. **Mitigated by:** nightly backup procedure covers both SQL Server and MinIO data volumes; MinIO versioning for file recovery.

**Validation:** File upload/download works via pre-signed URLs. Diagram artifacts (SVG/PNG) stored and retrieved correctly. Backup/restore tested before Phase 1 release.

---

### 3.12 Background Jobs / Scheduled Tasks

**Problem:** ACMP needs background processing for: meeting reminders, action escalations, notification digests, diagram render jobs, retention policy enforcement (Phase 2), and outbox message relay.

**Constraints:** CON-001 — must not use the org's Hangfire. Must be app-owned, backed by ACMP's own SQL (resolved decision §0.4).

**Options:**

| Option | Detail |
|---|---|
| Use org's Hangfire | Violates CON-001 |
| Quartz.NET | Open-source job scheduler; less integrated with .NET ecosystem; no built-in dashboard |
| Build own scheduler | Reinventing a solved problem |
| App-owned Hangfire on ACMP's SQL | Runs in-process; backed by ACMP's own SQL Server schema; dashboard built-in; Apache 2.0 |

**Recommendation: Build/configure app-owned Hangfire on ACMP's SQL (resolved decision §0.4 — settled).**

**Why-here:** Hangfire solves the exact problem (durable background jobs, retries, dashboard) in the .NET ecosystem. Running it in-process on ACMP's own SQL adds minimal overhead for ≤20 users. The org's Hangfire is off-limits (CON-001), but running our own Hangfire is straightforward and standard.

**Risks:** In-process Hangfire means background jobs contend with web traffic on the same process. **Mitigated by:** at ≤20 users, this contention is negligible; if it becomes a problem, Hangfire supports a separate worker-service process using the same SQL backend.

**Validation:** Reminder notifications fire within 5 minutes of their scheduled time. Diagram render jobs complete and store artifacts before the next meeting. Failed jobs retry and alert via Seq.

---

### 3.13 Observability

**Problem:** ACMP needs structured logging, distributed tracing, and metrics. Alerting on errors and anomalies.

**Constraints:** CON-001 — must not use the org's ELK/Seq. Self-hosted, app-owned (resolved decision §0.3).

**Options:**

| Option | Detail |
|---|---|
| Use org's ELK/Seq | Violates CON-001 |
| Grafana + Loki + Tempo | Modern OSS stack; more complex to operate; overkill for ≤20 users |
| Datadog / New Relic | Cloud SaaS; violates on-prem constraint |
| Self-hosted Seq + Serilog + OpenTelemetry | Seq is the chosen log/trace backend; Serilog for structured logging; OTLP for traces/metrics |

**Recommendation: Integrate self-hosted Seq + Serilog + OpenTelemetry (resolved decision §0.3 — settled).**

**Why-here:** Seq is a purpose-built structured log and trace backend that integrates seamlessly with Serilog and OpenTelemetry in .NET. Its Docker image is small, its query language is intuitive, and it requires no additional infrastructure (unlike Grafana + Loki + Tempo). For ≤20 users, Seq is perfectly right-sized.

**Risks:** Seq's free tier limits ingestion rate; paid licence may be required for high-volume logs. **Mitigated by:** at ≤20 users, ingestion volume is small; log level filtering (Information in prod) reduces volume further. Evaluate licence need at Phase 1 launch.

**Validation:** All application errors appear in Seq within 30 seconds. Traces correlate HTTP requests through Hangfire jobs. Health-check endpoint reflects Seq connectivity.

---

## 4. Summary Verdict Table

| Component | Verdict | Tool/Approach | Rationale | Key Risk |
|---|---|---|---|---|
| **Platform overall** | Build | .NET 8 modular monolith + React 18 | No off-the-shelf product covers the intersection of requirements | Build cost; mitigated by phased delivery |
| **Committee / Meeting / Agenda / Voting** | Build | Custom modules (Membership, Meetings, Agenda, Voting) | Domain-specific; immutable vote model; chairman authority model unique | Vote integrity bugs; mitigated by tests |
| **Backlog / Topics** | Build | Topics module (.NET/SQL Server) | Central domain anchor; committee-specific status lifecycle + relationships | Complex state machine; mitigated by explicit domain model |
| **ADR module** | Build (MADR pattern) | ADR module; MADR template extended | Needs committee workflow + bilingual labels + traceability; Log4brains is static-site-only | Template rigidity; mitigated by Markdown body |
| **Wiki / Knowledge** | Build (thin) + Embed TipTap | Knowledge module + TipTap OSS editor | Outline needs PostgreSQL (stack mismatch); thin module sufficient | TipTap Pro features absent; OSS core sufficient |
| **Diagrams** | Integrate | Tarseem render sidecar (ADR-0006, Phase 2) | Arabic-first; 11 diagram families; JSON spec as source | Early maturity; mitigated by spec-as-source |
| **Research / Discovery** | Build + Integrate (optional) | Research module standalone + optional Keystone import (ADR-0007) | Research module fully standalone; Keystone is additive capability | Keystone schema drift; mitigated by ingest-time validation |
| **Identity / SSO** | Integrate (self-hosted) | Self-hosted Keycloak OIDC (ADR-0015; ADR-0004) | ACMP self-hosts Keycloak (ACMP-owned realm); roles via claims; no self-registration | Keycloak now in ACMP's own ops/availability scope; mitigated by bundled backup + warm standby + JWT caching |
| **Notifications** | Build (v1 in-app) + Integrate Phase 2 | In-app notification center + Webex adapter Phase 2 (ADR-0005) | No email available in v1; org notification platform off-limits | Users may miss in-app only; Webex Phase 2 adds push | 
| **Search** | Build (SQL FTS) | SQL Server Full-Text Search (ADR-0011) | Sufficient for ≤20 users; no evidence of need for OpenSearch yet | Arabic FTS quality; OQ raised |
| **Reporting / Dashboards** | Build | SQL columnstore + EF Core read models + React dashboard | Sufficient at this scale; no BI tool needed | Slow analytical queries; mitigated by columnstore |
| **Object Storage** | Integrate | Self-hosted MinIO (resolved decision §0.5) | S3-compatible; pre-signed URLs; Docker; `IFileStore` abstraction | Backup coverage; mitigated by joint SQL+MinIO backup |
| **Background Jobs** | Build/configure | App-owned Hangfire on ACMP's SQL (resolved decision §0.4) | CON-001 forbids org Hangfire; in-process Hangfire is right-sized | Job/web contention; negligible at ≤20 users |
| **Observability** | Integrate | Self-hosted Seq + Serilog + OpenTelemetry (resolved decision §0.3) | CON-001 forbids org ELK/Seq; Seq is right-sized; .NET native | Seq licence at scale; negligible at this scale |

---

## 5. Conclusion

ACMP's build-vs-buy-vs-integrate strategy reduces to a single principle: **build the thin domain core; integrate proven, right-sized companion tools; reuse patterns from the ecosystem.**

The domain core — topics, meetings, agenda, voting, decisions, ADRs, actions, risks, dependencies, invariants, traceability — is specific enough to ACMP's committee governance context that no off-the-shelf product covers it. The appropriate response is a focused, phased build.

The companion tools — Keycloak (identity), Tarseem (diagrams), MinIO (storage), Seq (observability), Hangfire (jobs) — are each proven, self-hostable, right-sized for the scale, and have clear integration points. They are integrated via abstractions (`INotificationChannel`, `IFileStore`, `INotificationChannel`) so they can be replaced without rewriting application logic.

The patterns — MADR, docs-as-code, entity-catalog model, immutable audit trail, arc42, C4, OWASP ASVS L2, WCAG 2.2 AA — are adopted from the ecosystem as intellectual inputs, not as software dependencies.

This strategy keeps the deployment simple (a Docker Compose stack, on-prem, no Kubernetes), the codebase understandable (a modular monolith in .NET), and the future options open (every major component is behind an abstraction or a swappable sidecar).

---

## Traceability

- Deliverable 31 (`docs/23-build-vs-buy-vs-integrate.md`)
- Informs: `execution-handoff/` (build plan), `docs/36-roadmap.md`, `docs/37-implementation-backlog.md`, `docs/38-epics-and-features.md`
- ADRs: ADR-0001 (modular monolith), ADR-0002 (.NET), ADR-0003 (SQL Server), ADR-0004 (Keycloak), ADR-0005 (notifications), ADR-0006 (Tarseem), ADR-0007 (Keystone optional), ADR-0009 (immutability), ADR-0011 (SQL FTS), ADR-0012 (React)
- Informed by: `docs/21-open-source-landscape.md`, `docs/19-tarseem-analysis-and-integration.md`, `docs/20-keystone-analysis-and-integration.md`
- Open questions raised: OQ on SQL Server Arabic FTS quality (§3.9)
- Risks confirmed: diagram sidecar early maturity; Arabic search quality; Keycloak availability dependency
