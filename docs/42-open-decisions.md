# 42 — Open Decisions

**Purpose:** Separates fully resolved decisions (Section A) from decisions still requiring organizational input (Section B). Covers Deliverable 50.

**Guidance:** Section A decisions are canon — they override anything contradicting them in any other document. Section B items each carry a recommended default so the build is never blocked; if the org provides no answer, the default is applied. Every OQ-### item has an owner and a needed-by-phase so blockers are visible.

---

## A. RESOLVED DECISIONS — 2026-06-24

Confirmed by the secretary 2026-06-24. These are treated as settled canon (mirrors README §A; ADRs in `adr/`).

| # | Topic | Decision | Note / ADR |
|---|---|---|---|
| R-01 | **Deployment model** | On-prem VM(s) + Docker Compose. No Kubernetes, no service mesh, no cloud PaaS. | ADR-0001; CON-003 |
| R-02 | **Self-contained constraint** | ACMP bundles its own background processing, observability, and notification channels. No org Hangfire, ELK, Seq, or centralized notification platform. | CON-001; README §A |
| R-03 | **Object storage** | Self-hosted MinIO (S3-compatible) as an ACMP-owned container; `IFileStore` abstraction in code; metadata in SQL Server. | ADR-0003; DEP-003 |
| R-04 | **Background jobs** | App-owned Hangfire on ACMP's own SQL Server schema (not the org's Hangfire). Hangfire dashboard accessible to Administrator. SQL outbox for durability. | README §A |
| R-05 | **Email in v1** | No email notifications in v1. In-app notification center is the sole channel. Email deferred until an SMTP relay is confirmed. `INotificationChannel` abstraction ensures zero code change when email is added. | ADR-0005; CON-006 |
| R-06 | **Identity / roles** | Keycloak (OIDC, authorization-code + PKCE) is the IdP. Global roles sourced exclusively from Keycloak group/realm-role claims mapped to ACMP canonical roles. ACMP does not store or assign roles. No self-registration. | ADR-0004; CON-007, CON-009 |
| R-07 | **Voting attribution** | Voting is always attributed in v1 (no anonymous voting). Every voter's choice is recorded and visible to Auditor and Chairman. | ADR-0010; CON-011 |
| R-08 | **Stream visibility** | All committee members may read topics across all streams by default. Write/create/edit is restricted by role and ownership. | README §C |
| R-09 | **Single committee** | ACMP models exactly one committee. No multi-committee generalization, no tenant discriminators, no sub-committees in v1. | CON-010; settled 2026-06-24 |
| R-10 | **Webex integration phase** | Webex = Phase 2 (notification Adaptive Card adapter + recording/transcript retrieval). No Webex calls in PH-1 code. | ADR-0005; CON-014 |
| R-11 | **Recordings and transcripts** | Phase 1: Secretary pastes recording link or uploads file manually. Phase 2: Webex Recordings API + Transcripts API (subject to Webex Assistant enablement). | README §A; D-02 |
| R-12 | **AI extraction** | Phase 3 only. Manual first. Always human-reviewed; no AI-extracted content enters the record without explicit Secretary approval. Feature off by default; Admin activation requires data-residency confirmation. | CON-015; ADR-0007 (Keystone principle); RISK-012 |
| R-13 | **Retention policy** | Keep all records. No automatic purge in v1. Retention is configurable so legal/compliance can set periods later. Votes, issued decisions, ADRs, and published minutes are immutable. | README §A; FR-155 |
| R-14 | **Availability target** | 24×7 / 99.9%, achieved via standby VM + nightly backups. No HA cluster, no horizontal scaling. | README §A; ASM-008 |
| R-15 | **Calendar / dates** | Gregorian only. No Hijri calendar in v1. Localized Gregorian formatting in AR is acceptable. | CON-012; settled 2026-06-24 |
| R-16 | **Keystone integration** | Optional. Research module works fully standalone. Keystone import is a Phase 2 enhancement; never a hard dependency. | ADR-0007; CON-013 |
| R-17 | **Scale ceiling** | On-prem, low traffic, ≤20 total users. Right-size everything. Reject architectural additions whose only justification is scaling beyond 20 users. | CON-004; settled 2026-06-24 |
| R-18 | **Audit hash chain** | SHA-256 chained hash over vote and issued-decision audit records. Enables independent integrity verification by Auditor. | ADR-0009; BL-067 |
| R-19 | **Tarseem integration phase** | Tarseem diagram sidecar = Phase 2. JSON spec is the version-controlled source of truth. Artifacts are generated; a broken render is never blocking. | ADR-0006; settled 2026-06-24 |
| R-20 | **Primary datastore** | Microsoft SQL Server only (transactional + reporting via columnstore + FTS). No second database in v1. ACMP owns its instance/schema. | ADR-0003 |
| R-21 | **Backend stack** | .NET 8 (LTS), ASP.NET Core, REST, Clean Architecture per module, vertical-slice handlers (MediatR), EF Core. | ADR-0002; CON-002 |
| R-22 | **Frontend stack** | React 18 + TypeScript + Vite; react-i18next (EN/AR); RTL via CSS logical properties + `dir`; light/dark; accessible DnD (@dnd-kit). | ADR-0012; CON-002, CON-008 |
| R-23 | **Architecture style** | Modular monolith. Single deployable. Modules communicate via in-process public contracts only. No cross-module DB access. Microservices explicitly rejected for v1. | ADR-0001 |
| R-24 | **Search in v1** | SQL Server Full-Text Search. App-owned self-hosted search (e.g., OpenSearch) if FTS outgrows scale — never the org's ELK. | ADR-0011 |
| R-25 | **Traceability model** | Explicit typed relationship model (directed edges over shared Artifact identity); graph traversal in SQL. | ADR-0008 |
| R-26 | **Observability** | Serilog + self-hosted Seq container (ACMP-owned) + OpenTelemetry traces/metrics. No dependency on org's ELK/Seq. | README §A |

---

## B. OPEN — Needs Organizational Input

Each item has a recommended default that unblocks the build. If the org provides no answer by the needed-by-phase, the default is applied and recorded as a planning decision.

**Columns:** OQ-ID | Question | Options | Recommendation (default if no answer) | Owner | Needed-by-Phase

---

### Identity & Access

| OQ-ID | Question | Options | Recommendation | Owner | Needed-by |
|---|---|---|---|---|---|
| OQ-001 | **Guest vs. Presenter split**: Should `Guest` and `Presenter` be two distinct global roles or a single role with a `canPresent` attribute? | (a) Single role: `Guest/Presenter` with a boolean flag per meeting/topic. (b) Two separate roles: `Guest` (view-only, no presentation) + `Presenter` (can present, elevated during meeting). | **Default: (a) single `Guest/Presenter` role** with a relationship attribute (`isPresenter`) on the topic/meeting invitation. Avoids role proliferation; relationship-based ABAC handles the distinction. | Tech Lead | PH-2 |
| OQ-002 | **Reviewer-may-vote**: Can a user with the `Reviewer` global role cast a vote on a topic they are reviewing? | (a) Yes — Reviewer is in the eligible-voter pool. (b) No — Reviewer role is review-only; voting is for Members, Chairman, and Secretary. | **Default: (b) No**. Reviewers annotate but do not vote; vote eligibility is configured per vote by Secretary from the Member/Chairman/Secretary pool. Prevents role-boundary ambiguity. | Secretary / Chairman | PH-1 |
| OQ-003 | **MFA and session/idle timeout**: What MFA policy and session idle-timeout should Keycloak enforce for ACMP users? | (a) MFA required for all users. (b) MFA required for Chairman and Secretary only. (c) MFA optional (user choice). Idle timeout: 30 min / 60 min / 8 hours. | **Default: (b) MFA required for Chairman + Secretary; optional for others. Idle timeout: 60 minutes.** Adjust in ACMP's **own** Keycloak realm settings (self-hosted — ADR-0015) — no ACMP code change. | Security Reviewer / Keycloak Admin | PH-0 |

---

### Topics & Triage

| OQ-ID | Question | Options | Recommendation | Owner | Needed-by |
|---|---|---|---|---|---|
| OQ-004 | **TopicRequest merged vs. separate**: Should topics submitted by `Submitter`-role users (external stream requesters) be the same entity as committee-internal topics, or a separate `TopicRequest` entity that gets converted to a `Topic` after triage? | (a) Merged: same Topic entity; Submitter creates a Draft topic; Secretary triages it directly. (b) Separate: `TopicRequest` entity → Secretary converts to `Topic` on acceptance. | **Default: (a) Merged**. A single Topic entity with Draft status and Submitter as a creator role is simpler, avoids entity duplication, and the triage workflow already handles the gating. Separation adds schema complexity with no governance benefit at this scale. | Tech Lead / Secretary | PH-1 |
| OQ-005 | **Urgency SLA thresholds**: Are the proposed aging thresholds (Critical: 3 days in status, Urgent: 7 days, Normal: 21 days) correct? [FR-038 marks these as unverified.] | Committee may propose different values. | **Default: Critical = 3 days, Urgent = 7 days, Normal = 21 days** until the committee reviews after PH-1 pilot. Thresholds are configurable at deployment time; no code change to adjust. | Secretary / Chairman | PH-1 (validate after pilot) |
| OQ-006 | **Source attribute enum**: Should `Source` on a topic be a free-text field or an enum? What values? (e.g., CommitteeMember, StreamRequest, OperationalIncident, SecurityFinding, RegulatoryRequirement, Innovation, ExternalPartner, Other) | (a) Free-text. (b) Enum with predefined values + an `Other (free-text)` escape. | **Default: (b) Enum + Other**. Enables filtering, reporting, and backlog analytics by source; free-text alone cannot be grouped. Proposed enum: `CommitteeMember | StreamRequest | OperationalIncident | SecurityFinding | RegulatoryRequirement | Innovation | ExternalPartner | Other`. Secretary validates list before PH-1. | Secretary | PH-1 |
| OQ-007 | **Invariant categories**: What are the canonical Architecture Invariant categories? FR-106 proposes `Security / Performance / Data / Interoperability / Other` but marks these as `[unverified]`. | Committee may add: Reliability, Scalability, Maintainability, Compliance, Integration, UX, Cost. | **Default: Security, Performance, Data, Interoperability, Compliance, Other**. Stored as an enum in v1 with `Other` escape. Secretary confirms list before PH-2 ADR/Invariant build. | Secretary / Chairman | PH-2 |

---

### Meetings & MoM

| OQ-ID | Question | Options | Recommendation | Owner | Needed-by |
|---|---|---|---|---|---|
| OQ-008 | **Meeting cadence**: Is the committee moving to bi-weekly meetings or staying weekly? This affects SLA calculations, aging indicator thresholds, and carry-over logic. | (a) Weekly. (b) Bi-weekly. (c) Variable (secretary sets per meeting). | **Default: bi-weekly** (digest §2 notes committee is "considering bi-weekly"). Meeting entity always has a specific date; cadence is an operational configuration, not hard-coded. SLA thresholds are independent of cadence. | Secretary / Chairman | PH-1 |
| OQ-009 | **Real-time collaborative MoM editing**: Should multiple users (e.g., Secretary + Reviewer) be able to edit the draft MoM simultaneously? | (a) No — single editor at a time (last-write-wins with a dirty-lock warning). (b) Yes — real-time collaborative editing (OT/CRDT). | **Default: (a) No** in v1. OOS-07 explicitly excludes real-time collaborative editing. The MoM approval workflow (Secretary → Reviewer annotates → Chairman approves) serializes editing. Revisit in PH-3 only if demand is demonstrated. | Tech Lead | PH-1 |
| OQ-010 | **Meeting type enum**: Beyond `regular / extraordinary / emergency`, are there other meeting types used by the committee? | Additional options: workshop, review, ad-hoc. | **Default: Regular, Extraordinary, Emergency** as the initial enum; `Other` escape. Secretary confirms before PH-1 meeting module build. | Secretary | PH-1 |
| OQ-011 | **Transcript as ArtifactType**: When Webex transcripts are retrieved (PH-2), should a `Transcript` be a first-class artifact type (with its own entity, IDs, traceability edges) or stored as a blob attachment on the meeting record? | (a) First-class artifact: `TRS-YYYY-###` ID, traceability linkable, searchable independently. (b) Blob attachment on meeting record (MIN or MTG entity). | **Default: (a) first-class artifact** for PH-2. Transcript is independently searchable (FR-059) and is the source for AI extraction (PH-3). A blob on the meeting record makes search and traceability harder. Decide before PH-2 Webex retrieval build. | Tech Lead | PH-2 |

---

### Technical — Backend

| OQ-ID | Question | Options | Recommendation | Owner | Needed-by |
|---|---|---|---|---|---|
| OQ-012 | **SPA serving: nginx vs. ASP.NET Core static files**: Should the React SPA be served by nginx (separate container) or by ASP.NET Core `UseStaticFiles()` / `MapFallbackToFile()` from the same app container? | (a) nginx: separate container; standard SPA serving practice; independent scaling if ever needed. (b) ASP.NET Core: single container; simpler Compose stack; one less image to maintain. | **Default: (b) ASP.NET Core** static file serving for v1. Reduces container count and operational surface. CON-004 (≤20 users) makes separate nginx unnecessary. Revisit only if proven bottleneck. | Tech Lead | PH-1 |
| OQ-013 | **OIDC client library (frontend)**: Which OIDC client library should the React app use? | (a) `oidc-client-ts` + `react-oidc-context` (active fork of `oidc-client-js`). (b) `@auth0/auth0-react` (Auth0 SDK, Keycloak-compatible). (c) Custom PKCE flow using `fetch`. | **Default: (a) `oidc-client-ts` + `react-oidc-context`**. Active community; TypeScript-first; supports authorization-code + PKCE against Keycloak natively; not tied to Auth0 branding. | Tech Lead | PH-1 |
| OQ-014 | **SQL clustered-key strategy**: Should governed entities (Topic, Decision, Vote, Action, etc.) use `GUID` (newsequentialid) or `BIGINT IDENTITY` as the clustered key? | (a) GUID (newsequentialid): avoids page fragmentation; globally unique across systems; simpler distributed extension. (b) BIGINT IDENTITY: smaller, faster joins and index; no fragmentation with sequential IDs; no cross-system distribution concern at ≤20 users. | **Default: (b) BIGINT IDENTITY** for clustered key + GUID as alternate unique key for external references (human-readable canonical IDs like `TOP-YYYY-###` are separate display IDs). At this scale, integer keys are significantly faster and simpler. | Tech Lead | PH-1 |
| OQ-015 | **Ballot storage: JSON column vs. normalized table**: Should vote options and individual vote records be stored as structured JSON in a single column, or in a fully normalized table (Voter, VoteOption, VoteCast rows)? | (a) JSON column: flexible for different ballot configurations; simpler schema; harder to query aggregate counts in SQL. (b) Normalized table: queryable, indexable, auditable per-row; more schema; immutability per-row easier to enforce and hash. | **Default: (b) Normalized table** (VoteSession, VoteOption, VoteCast rows). Immutability and hash-chain integrity are easier to enforce per-row. Audit log entries per VoteCast row are atomic. JSON column is a better fit for semi-structured data; vote records are highly structured. | Tech Lead | PH-1 |
| OQ-016 | **Notification polling interval vs. WebSocket**: In-app notification center: should the client poll the server or receive push via WebSocket/SSE? | (a) Short polling: client GETs `/api/notifications/unread` every N seconds. (b) Server-Sent Events (SSE): server pushes; simpler than WebSocket; one-directional. (c) WebSocket: bidirectional; OOS-15 explicitly excludes WebSocket infrastructure. | **Default: (a) short polling at 30-second interval** in PH-1. OOS-15 rejects WebSocket infrastructure. SSE is acceptable but adds a persistent connection per user (≤20 users: feasible; still adds server-side concurrency). Polling at 30s is simple, correct, and sufficient. Revisit in PH-3 if users report stale notifications. | Tech Lead | PH-1 |
| OQ-017 | **Relationship typed vs. generic — warning needed?**: The ADR-0008 typed relationship model defines 8 relationship types (`DerivedFrom, Supersedes, Implements, Resolves, References, Blocks, DependsOn, RelatesTo`). Is this set final, or should the system support administrator-defined custom types? | (a) Fixed enum: 8 types; any addition requires a code/migration change. (b) Semi-open: 8 predefined + admin can define custom types stored in a lookup table. | **Default: (a) fixed enum** in v1. Custom types add a type-management UI, migration risk, and potential misuse that dilutes semantic precision. The 8 types cover all use cases in the domain model. Add `Other` escape if needed. Re-evaluate in PH-3. | Tech Lead / Secretary | PH-1 |
| OQ-018 | **Impact analysis node cap**: The transitive impact analysis query (FR-148, BL-123) uses SQL graph traversal with a configurable depth limit. What is the max node cap before the query is terminated to protect performance? | (a) Depth limit only (default 3 hops). (b) Depth limit + total node cap (e.g., 100 nodes max). (c) Time limit (query timeout N seconds). | **Default: depth limit 3 hops + total node cap 200 nodes.** At ≤20 users and a governance tool (not a large dependency graph), 200 nodes at 3 hops is extremely unlikely to be hit. If hit, return results so far with a "graph truncated" warning. | Tech Lead | PH-2 |
| OQ-019 | **Swagger UI in production**: Should the OpenAPI/Swagger UI be enabled in the production environment? | (a) Yes — useful for integration partners and Auditor-role API exploration. (b) No — security risk; disable in prod; available in dev/staging only. | **Default: (b) Disabled in production** (FR-008 says "accessible in development and staging environments" — production is excluded). If an admin/auditor needs API access, they can use the staging environment's Swagger or a downloaded spec file. | Security Reviewer | PH-1 |
| OQ-020 | **Standby VM warm vs. on-demand**: Should the standby VM (for 99.9% availability) be kept warm (running, containers up, synced DB) or cold (booted only on primary failure)? | (a) Warm standby: always running; faster failover (minutes); higher cost (second VM running 24×7). (b) Cold standby: powered off; slower failover (30–60 min boot + restore); lower cost. | **Default: (a) warm standby** if budget permits; otherwise **cold standby with documented restore procedure**. 99.9% SLA (~8.7h/year downtime) is achievable with cold standby + nightly backups if restore time is ≤8h. Secretary confirms availability budget. | Secretary / Org IT | PH-0 |

---

### Technical — Frontend

| OQ-ID | Question | Options | Recommendation | Owner | Needed-by |
|---|---|---|---|---|---|
| OQ-021 | **Mobile breakpoint in MVP**: Should PH-1 include a mobile (phone) responsive breakpoint, or only tablet + desktop? | (a) All three: phone (≤768px), tablet (769–1199px), desktop (≥1200px). (b) Tablet + desktop only: ≥768px. | **Default: (b) tablet + desktop only** in PH-1. Committee use is desk/laptop/tablet. Mobile layout adds design/test surface for minimal benefit. Mobile breakpoint deferred to PH-3 if demonstrated need. | UX Designer | PH-1 |
| OQ-022 | **Chart library and RTL validation**: Which charting library should be used for dashboard charts, and has it been validated for RTL layout? | (a) Recharts: popular React chart library; RTL support partial (text direction may need manual override). (b) Apache ECharts (echarts-for-react): RTL support documented. (c) Nivo: composable, accessible, but limited RTL docs. | **Default: (a) Recharts** with RTL validation spike in PH-1 dashboard build. If RTL chart rendering fails validation, switch to ECharts before PH-1 dashboard delivery. Validate in the RTL visual regression test suite. | Frontend Engineer | PH-1 |
| OQ-023 | **PDF export library**: For diagram export (PDF via Tarseem) and report export (PDF from tables), should ACMP generate PDFs server-side or browser-side? | (a) Server-side: iTextSharp / QuestPDF (.NET) — RTL text support is variable; QuestPDF has Arabic support docs. (b) Browser-side: html2pdf.js / `window.print()` — RTL depends on browser print engine (generally good). (c) Tarseem handles diagram PDF; report PDF is browser print. | **Default: (c) Tarseem for diagram PDF; browser `window.print()` for report PDF** in PH-1/PH-2. Avoids a server-side PDF library dependency; browser print RTL is reliable. If polished PDF output is required, evaluate QuestPDF in PH-3. | Tech Lead | PH-2 |

---

### Security

| OQ-ID | Question | Options | Recommendation | Owner | Needed-by |
|---|---|---|---|---|---|
| OQ-024 | **TLS / cipher / mTLS policy**: What TLS version and cipher suite policy applies to ACMP's HTTPS endpoint, and is mTLS required for any service-to-service call? | (a) TLS 1.2+ (standard). (b) TLS 1.3 only (stricter). (c) mTLS between ACMP app ↔ SQL Server and ACMP app ↔ Tarseem sidecar. | **Default: TLS 1.2+ on the public HTTPS endpoint (nginx/ASP.NET); TLS 1.3 preferred if all clients support it. No mTLS in v1 for internal Compose-network services** (all containers on the same Docker network; internal traffic is not exposed externally). Security reviewer confirms. | Security Reviewer | PH-0 |
| OQ-025 | **Dual-control vs. anomaly-alerting for sensitive ops**: For high-sensitivity operations (e.g., Administrator deactivating a Chairman account, Secretary deleting a topic), should the system require dual-control (two approvals) or rely on audit log + alerting? | (a) Dual-control: two authorized users must approve before execution. (b) Audit log + anomaly alert: single authorized user executes; action is immutably logged; Auditor is notified. | **Default: (b) audit log + anomaly alert** in v1. Dual-control adds significant workflow complexity for a ≤20-user system. Immutable audit log + hash chain + Auditor notification provides adequate detective controls. Review if classification escalates. | Security Reviewer / Secretary | PH-1 |
| OQ-026 | **Malware scan approach for uploaded files**: Should ACMP scan uploaded files (attachments, recordings) for malware before storage? | (a) ClamAV: open-source antivirus; containerizable; REST API available. (b) Rely on org AV scanning at the network/VM level. (c) No scan in v1 (accept risk; file types restricted by MIME type validation). | **Default: (c) MIME type + extension whitelist validation in v1** (restrict to: PDF, DOCX, PPTX, XLSX, PNG, JPG, SVG, MP4, MP3, ZIP, JSON). If the org requires antivirus scanning, ClamAV as a Compose sidecar is the recommended option. Org security policy governs. | Security Reviewer | PH-1 |
| OQ-027 | **SAST / DAST / scan tool choices and CI gating**: Which SAST and DAST tools should run in CI, and should failing scans block merges? | (a) SAST: Semgrep (free, .NET rules) / SonarQube (enterprise). DAST: OWASP ZAP (self-hosted). (b) Org's existing toolchain (if any). | **Default: Semgrep OSS for SAST on every PR (block on high-severity findings); OWASP ZAP baseline scan on staging post-deploy (alert, don't block in v1 — too many false positives initially). Dependency scanning: `dotnet list package --vulnerable` + `npm audit`.** Org may substitute its own tools. | Security Reviewer / DevSecOps | PH-0 CI setup |
| OQ-028 | **Strict CSP nonce/hash**: Should the Content Security Policy use nonces/hashes for inline scripts (strict CSP) or a domain allowlist? | (a) Strict CSP with nonces: best practice; harder with a React SPA + bundler (requires server-side nonce injection or hash generation). (b) Domain allowlist CSP: `script-src 'self'`; simpler; allows pre-built SPA with no inline scripts. | **Default: (b) `script-src 'self'` CSP** (no `unsafe-inline`, no `unsafe-eval`) for the Vite-built React SPA. Vite bundles eliminate inline scripts. Add `report-uri` endpoint to log violations. If inline scripts are required (e.g., analytics injected by a 3rd party), switch to nonce. | Security Reviewer / Frontend Engineer | PH-1 |
| OQ-029 | **ZAP network approvals**: Does the org's network security team require approval / penetration test sign-off before ACMP is deployed to the production VM? | (a) Yes — formal network scan + pen-test required. (b) Informal security review by internal team. (c) No formal gate. | **Default: (b) internal security review** by the designated security reviewer before PH-1 go-live. If the org's security policy requires a formal pen-test, schedule it as a PH-1 gate item. | Security Reviewer / Secretary | PH-1 go-live |

---

### Infrastructure & DevSecOps

| OQ-ID | Question | Options | Recommendation | Owner | Needed-by |
|---|---|---|---|---|---|
| OQ-030 | **CI system**: Which CI system will run ACMP's automated test + build pipeline? | (a) GitHub Actions self-hosted runner (on the dev VM). (b) GitLab CI runner (if the org uses GitLab). (c) Azure DevOps pipelines. (d) Jenkins / other. | **Default: (a) GitHub Actions self-hosted runner** if network allows outbound GitHub connectivity; otherwise **(b) GitLab CI** if org uses GitLab. Decision required before PH-0 repository initialization. | Tech Lead / DevSecOps | PH-0 |
| OQ-031 | **Local Docker / container registry**: Should container images be pushed to and pulled from a private registry, or is Docker Hub / public registry acceptable? | (a) Private registry: mirrored on-prem (Harbor / GitLab Registry / ACR). (b) Public registry (Docker Hub) with image-digest pinning. | **Default: (b) public registry with digest pinning** for dev/staging; **private registry mirror strongly recommended for production** in an air-gapped or security-sensitive on-prem environment. Coordinate with org IT in PH-0. | Tech Lead / Org IT | PH-0 |
| OQ-032 | **NuGet / npm mirror**: Do NuGet and npm packages need to be mirrored to an internal feed (e.g., Azure Artifacts, ProGet, Nexus) to comply with org policy or network constraints? | (a) Yes — internal mirror required. (b) No — direct internet access to NuGet.org and registry.npmjs.org is available and permitted. | **Default: (b) direct access** assumed. If the dev VM is air-gapped or org policy requires package mirroring, set up NuGet + npm mirror in PH-0 before any package restore runs. Blocking if not addressed early. | DevSecOps / Org IT | PH-0 |
| OQ-033 | **KPI threshold baselining**: What are the initial threshold values for KPI dashboard indicators (avg topic-to-decision days, action SLA compliance %, backlog age limit, vote-to-ratification time)? There is no historical baseline before go-live. | (a) Hardcoded defaults. (b) Configurable by Admin; no default until committee sets them post-pilot. (c) Derived from the first 90 days of live data. | **Default: (b) Admin-configurable thresholds; display KPI trend without RAG status until the committee sets thresholds after 90 days of live data (PH-3 pilot period).** Do not show red/amber/green on a KPI that has no agreed baseline. | Secretary / Chairman | PH-3 |

---

### Data & Domain

| OQ-ID | Question | Options | Recommendation | Owner | Needed-by |
|---|---|---|---|---|---|
| OQ-034 | **Arabic FTS quality in SQL Server** (also logged as RISK-010): Is SQL Server's built-in Arabic word-breaker adequate for committee terminology (technical terms, transliterations from English)? | (a) SQL Server FTS Arabic is adequate. (b) FTS Arabic is inadequate; replace with Meilisearch (self-hosted, app-owned). | **Default: run PH-0 spike.** If ≥80% search recall on sample Arabic queries, proceed with SQL FTS. If <80%, escalate to Meilisearch behind the same search abstraction in PH-2. Do not replace proactively; validate first. | Tech Lead | PH-0 spike |
| OQ-035 | **Confirm Urgency SLA thresholds** (duplicate of OQ-005 but raised as a separate validation item for FR-038): The committee must explicitly confirm or revise the aging SLA values. | Same as OQ-005. | **Default: Critical = 3d, Urgent = 7d, Normal = 21d.** Configurable; adjust post-PH-1 pilot. | Secretary / Chairman | PH-1 (validate post-pilot) |
| OQ-036 | **Confirm Invariant categories** (duplicate validation item for FR-106): The Architecture Invariant category enum must be confirmed before PH-2 invariant build. | Same as OQ-007. | **Default: Security, Performance, Data, Interoperability, Compliance, Other.** | Secretary / Chairman | PH-2 |

---

### Phase 3 — AI & Analytics

| OQ-ID | Question | Options | Recommendation | Owner | Needed-by |
|---|---|---|---|---|---|
| OQ-037 | **AI extraction LLM endpoint provider**: If the org activates AI transcript extraction in PH-3, which LLM endpoint will be used, and has data residency been confirmed? | (a) On-prem LLM (Ollama + open-weight model on the org's GPU server). (b) Azure OpenAI (data-processing agreement with Microsoft). (c) Not determined. | **Default: (c) not determined**. AI extraction feature is off by default. Org must confirm LLM provider + data-residency compliance before Admin can activate. No code is blocked by this decision. | Security Reviewer / Secretary | PH-3 |

---

### Self-Hosted Keycloak & Bundled Dependencies (ADR-0015)

Opened by **ADR-0015** (ACMP self-hosts Keycloak with an ACMP-owned realm; SQL Server is also bundled; v1 has zero external runtime services).

| OQ-ID | Question | Options | Recommendation | Owner | Needed-by |
|---|---|---|---|---|---|
| OQ-038 | **Keycloak datastore**: Where does the self-hosted Keycloak keep its own operational store? (Application data stays SQL-Server-only per ADR-0003 — this concerns only Keycloak's internal store.) | (a) Dedicated Postgres-for-Keycloak container in the Compose stack. (b) Keycloak runs on the bundled SQL Server instance. | **Default: (a) dedicated Postgres-for-Keycloak container**; confirm via a **PH-0 spike**. App data stays SQL-only (ADR-0003); the spike decides only where Keycloak's own store lives. Backup/restore must cover whichever store is chosen. | Tech Lead | PH-0 spike |
| OQ-039 | **Future upstream federation/brokering**: Should ACMP's self-hosted Keycloak later broker/federate to an organizational IdP if one appears? | (a) No upstream federation in v1; keep the realm broker-capable for the future. (b) Plan upstream brokering now. | **Default: (a) no upstream federation in v1**; keep the ACMP realm broker-capable so a future org IdP can be added without rework. Deferred — login is an ACMP-specific credential until/unless an org IdP exists. | Tech Lead / Secretary | Deferred |
| OQ-040 | **Bundled SQL Server production edition / licensing**: Which SQL Server edition does the bundled production instance run? | (a) Express (free; 10 GB/DB limit, memory caps — verify columnstore + FTS availability). (b) Standard (licensed). | **Default: evaluate at deploy phase P18** — start with Express but **verify columnstore + Full-Text Search are available and that the 10 GB/DB + memory caps fit the ≤20-user footprint**; escalate to Standard (licensed) if limits bind. Confirm licensing with secretary / IT. | Tech Lead / Secretary / IT | P18 (deploy) |

---

## Summary: Decisions Blocking Each Phase

### PH-0 Blockers (must resolve before PH-1 coding starts)
- OQ-030 (CI system)
- OQ-031 (container registry)
- OQ-032 (NuGet/npm mirror)
- OQ-024 (TLS policy — for network security review)
- OQ-003 (MFA + session timeout — for Keycloak realm configuration)
- OQ-034 (Arabic FTS spike — determines PH-2 search strategy)
- OQ-020 (standby VM warm vs. cold — for infrastructure provisioning)
- OQ-038 (Keycloak datastore spike — Postgres-for-Keycloak vs bundled SQL Server; ADR-0015)

### PH-1 Build-Start Blockers (default applied if no org answer)
- OQ-002 (Reviewer-may-vote default: No)
- OQ-004 (TopicRequest merged default: Merged)
- OQ-006 (Source attribute enum — need Secretary validation of proposed list)
- OQ-008 (Meeting cadence — affects SLA and carry-over logic)
- OQ-010 (Meeting type enum)
- OQ-012 (SPA serving default: ASP.NET Core)
- OQ-013 (OIDC client library default: oidc-client-ts)
- OQ-014 (SQL clustered-key default: BIGINT IDENTITY)
- OQ-015 (Ballot storage default: normalized table)
- OQ-016 (Notification polling default: 30s poll)
- OQ-017 (Relationship types default: fixed enum)
- OQ-021 (Mobile breakpoint default: tablet + desktop only)
- OQ-022 (Chart library default: Recharts with RTL spike)
- OQ-025 (Dual-control default: audit log + alert)
- OQ-026 (Malware scan default: MIME type whitelist)
- OQ-027 (SAST/DAST tooling)
- OQ-028 (CSP default: domain allowlist)

### PH-2 Blockers
- OQ-001 (Guest vs. Presenter)
- OQ-007 / OQ-036 (Invariant categories)
- OQ-011 (Transcript as ArtifactType)
- OQ-018 (Impact analysis node cap)
- OQ-023 (PDF export library)

### PH-3 Blockers
- OQ-033 (KPI thresholds)
- OQ-037 (AI LLM endpoint)

### Deploy-Phase / Deferred
- OQ-040 (bundled SQL Server production edition/licensing — deploy phase P18; ADR-0015)
- OQ-039 (future upstream Keycloak federation/brokering — deferred; ADR-0015)

---

## Traceability

- Resolved decisions (Section A) → `README.md §A` (canonical source) and `adr/ADR-0001` through `ADR-0012`.
- OQ-### items → `docs/41-raid.md` (ASM-### for assumptions underlying defaults; RISK-### for risk of wrong choice).
- OQ-### "needed-by-phase" → `docs/36-roadmap.md` (phase exit criteria require blocking OQs to be resolved).
- OQ-### → `docs/37-implementation-backlog.md` (BL-### items that cannot start until their blocking OQ is resolved).
- Resolution of any OQ-### creates a new entry in Section A of this document and updates the relevant docs/ADR.
