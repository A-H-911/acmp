---
status: Approved
version: 1.0.0
updated: 2026-07-06
owner: lead-secretary
---

# Open-Question Register — ACMP

These entries carry the open questions formerly recorded in the pre-migration `docs/42-open-decisions.md §B`. Every OQ carries a recommended default so the build is never blocked; the **Resolution** column is that applied default (or the recorded resolution where the item is settled). **Blocking? = Yes** marks the eight PH-0 hard blockers the org must answer before PH-1 coding starts (OQ-003, OQ-020, OQ-024, OQ-030, OQ-031, OQ-032, OQ-034, OQ-038); all others carry a working default so **No**. **Status = Approved** where the doc records the item as resolved/applied; **Deferred** where it stays open awaiting organizational input (its default is applied meanwhile). Category grouping is preserved as subheadings.

### Identity & Access

| ID | Question | Blocking? | Resolution | Status |
|---|---|---|---|---|
| OQ-001 | Should Guest and Presenter be two distinct roles or one role with a `canPresent` attribute? | No | Single `Guest/Presenter` role with an `isPresenter` relationship attribute on the invitation; relationship-based ABAC handles the distinction | Deferred |
| OQ-002 | Can a user with the Reviewer role cast a vote on a topic they are reviewing? | No | No — Reviewers annotate but do not vote; eligibility is configured per vote from the Member/Chairman/Secretary pool | Deferred |
| OQ-003 | What MFA policy and session idle-timeout should Keycloak enforce? | Yes | MFA required for Chairman + Secretary, optional for others; 60-minute idle timeout; set in ACMP's own Keycloak realm, no code change | Deferred |

### Topics & Triage

| ID | Question | Blocking? | Resolution | Status |
|---|---|---|---|---|
| OQ-004 | Should Submitter-created topics be the same Topic entity or a separate TopicRequest converted after triage? | No | Merged — one Topic entity; Submitter creates a Draft, Secretary triages directly; avoids entity duplication | Deferred |
| OQ-005 | Are the aging thresholds (Critical 3d, Urgent 7d, Normal 21d) correct? | No | Keep Critical 3d / Urgent 7d / Normal 21d until the committee reviews after the PH-1 pilot; thresholds configurable at deploy | Deferred |
| OQ-006 | Should topic `Source` be free-text or an enum, and what values? | No | Enum + Other escape (CommitteeMember, StreamRequest, OperationalIncident, SecurityFinding, RegulatoryRequirement, Innovation, ExternalPartner, Other); enables reporting; Secretary validates list before PH-1 | Deferred |
| OQ-007 | What are the canonical Architecture Invariant categories? | No | Security, Performance, Data, Interoperability, Compliance, Other — enum with Other escape; Secretary confirms before the PH-2 build | Deferred |

### Meetings & MoM

| ID | Question | Blocking? | Resolution | Status |
|---|---|---|---|---|
| OQ-008 | Is the committee moving to bi-weekly meetings or staying weekly? | No | Bi-weekly default; cadence is operational config, not hard-coded; SLA thresholds are independent of cadence | Deferred |
| OQ-009 | Should multiple users edit the draft MoM simultaneously? | No | No — single editor at a time (last-write-wins with a dirty-lock warning); OOS-07 excludes real-time collab; revisit PH-3 if demanded | Deferred |
| OQ-010 | Are there meeting types beyond regular / extraordinary / emergency? | No | Regular, Extraordinary, Emergency as the initial enum with an Other escape; Secretary confirms before the PH-1 build | Deferred |
| OQ-011 | Should a Webex Transcript be a first-class artifact type or a blob attachment? | No | First-class artifact (`TRS-YYYY-###`, traceable, independently searchable) for PH-2; a blob makes search and traceability harder | Deferred |

### Technical — Backend

| ID | Question | Blocking? | Resolution | Status |
|---|---|---|---|---|
| OQ-012 | Serve the React SPA via nginx or ASP.NET Core static files? | No | Resolved (2026-06-25): a separate nginx `web` container proxying `/api`→api ships in v1, overriding the earlier ASP.NET-static default (docs/domain/repository-structure.md §8) | Approved |
| OQ-013 | Which OIDC client library should the React app use? | No | `oidc-client-ts` + `react-oidc-context` — active, TypeScript-first, native auth-code + PKCE against Keycloak, not Auth0-branded | Deferred |
| OQ-014 | GUID (newsequentialid) or BIGINT IDENTITY as the clustered key for governed entities? | No | BIGINT IDENTITY clustered key + GUID alternate unique key for external refs; human-readable `TOP-YYYY-###` remain separate display IDs | Deferred |
| OQ-015 | Store ballots as a JSON column or a fully normalized table? | No | Normalized table (VoteSession, VoteOption, VoteCast rows); per-row immutability and hash-chain integrity are easier to enforce and audit | Deferred |
| OQ-016 | Should the notification center poll the server or receive WebSocket/SSE push? | No | Short polling at a 30-second interval in PH-1; OOS-15 rejects WebSocket infrastructure; revisit PH-3 if users report staleness | Deferred |
| OQ-017 | Is the 8-type relationship enum final, or should admins define custom types? | No | Fixed enum of 8 types in v1 (add an Other escape if needed); custom types add UI, migration risk, and semantic dilution; re-evaluate PH-3 | Deferred |
| OQ-018 | What max node cap protects the transitive impact-analysis query? | No | Depth limit of 3 hops + a 200-node total cap; on overflow, return partial results with a "graph truncated" warning | Deferred |
| OQ-019 | Should the OpenAPI/Swagger UI be enabled in production? | No | Disabled in production (available in dev and staging only per FR-008); auditors use the staging Swagger or a downloaded spec | Deferred |
| OQ-020 | Keep the standby VM warm or cold? | Yes | Warm standby if budget permits, else cold standby with a documented ≤8h restore procedure; Secretary confirms the availability budget | Deferred |

### Technical — Frontend

| ID | Question | Blocking? | Resolution | Status |
|---|---|---|---|---|
| OQ-021 | Include a mobile (phone) breakpoint in PH-1, or tablet + desktop only? | No | Tablet + desktop only (≥768px) in PH-1; committee use is desk/laptop/tablet; mobile deferred to PH-3 if a need is demonstrated | Deferred |
| OQ-022 | Which charting library should dashboards use, and is it RTL-validated? | No | Resolved (2026-07-05, ADR-0022): no chart library — charts are CSS primitives (`rpt-*` renderers); RTL via logical properties + `dir`, verified by live VR | Approved |
| OQ-023 | Generate export PDFs server-side or browser-side? | No | Tarseem for diagram PDF; browser `window.print()` for report PDF in PH-1/PH-2; avoids a server-side PDF library; evaluate QuestPDF in PH-3 if needed | Deferred |

### Security

| ID | Question | Blocking? | Resolution | Status |
|---|---|---|---|---|
| OQ-024 | What TLS / cipher / mTLS policy applies to the HTTPS endpoint? | Yes | TLS 1.2+ on the public endpoint (1.3 preferred if all clients support it); no mTLS in v1 for internal Compose-network services | Deferred |
| OQ-025 | Require dual-control or audit + alert for high-sensitivity operations? | No | Audit log + anomaly alert in v1; dual-control is too heavy for a ≤20-user system; immutable log + hash chain + Auditor notice suffice; review if classification escalates | Deferred |
| OQ-026 | Should ACMP scan uploaded files for malware before storage? | No | MIME-type + extension whitelist validation in v1 (PDF, DOCX, PPTX, XLSX, PNG, JPG, SVG, MP4, MP3, ZIP, JSON); ClamAV Compose sidecar if the org requires scanning | Deferred |
| OQ-027 | Which SAST/DAST tools run in CI, and do failing scans block merges? | No | Semgrep OSS SAST on every PR (block on high severity); OWASP ZAP baseline on staging (alert, not block in v1); dependency scan via `dotnet list package --vulnerable` + `npm audit` | Deferred |
| OQ-028 | Use a strict nonce/hash CSP or a domain allowlist? | No | `script-src 'self'` (no unsafe-inline, no unsafe-eval) for the Vite-built SPA + a report-uri endpoint; switch to nonce only if inline scripts become required | Deferred |
| OQ-029 | Does the org require a formal pen-test sign-off before production deploy? | No | Internal security review by the designated reviewer before PH-1 go-live; schedule a formal pen-test as a PH-1 gate if org policy requires it | Deferred |

### Infrastructure & DevSecOps

| ID | Question | Blocking? | Resolution | Status |
|---|---|---|---|---|
| OQ-030 | Which CI system runs the automated test + build pipeline? | Yes | GitHub Actions self-hosted runner if outbound GitHub is allowed, else GitLab CI if the org uses GitLab; decide before PH-0 repo initialization | Deferred |
| OQ-031 | Push and pull images via a private registry or a public one? | Yes | Public registry with digest pinning for dev/staging; a private on-prem mirror is strongly recommended for production; coordinate with org IT in PH-0 | Deferred |
| OQ-032 | Must NuGet and npm packages be mirrored to an internal feed? | Yes | Direct internet access assumed; set up a NuGet + npm mirror in PH-0 if the VM is air-gapped or policy requires it (blocking if not addressed early) | Deferred |
| OQ-033 | What initial KPI dashboard threshold values apply with no historical baseline? | No | Admin-configurable thresholds; show KPI trend without RAG status until the committee sets thresholds after 90 days of live data | Deferred |

### Data & Domain

| ID | Question | Blocking? | Resolution | Status |
|---|---|---|---|---|
| OQ-034 | Is SQL Server's Arabic word-breaker adequate for committee terminology? | Yes | Run a PH-0 spike; proceed with SQL FTS if ≥80% recall, else escalate to app-owned OpenSearch behind the `ISearchIndex` abstraction in PH-2; validate before replacing | Deferred |
| OQ-035 | Confirm the Urgency SLA thresholds (validation item for FR-038). | No | Critical 3d, Urgent 7d, Normal 21d; configurable; adjust post-PH-1 pilot (duplicate of OQ-005) | Deferred |
| OQ-036 | Confirm the Architecture Invariant category enum (validation item for FR-106). | No | Security, Performance, Data, Interoperability, Compliance, Other (duplicate of OQ-007) | Deferred |

### Phase 3 — AI & Analytics

| ID | Question | Blocking? | Resolution | Status |
|---|---|---|---|---|
| OQ-037 | Which LLM endpoint powers PH-3 AI extraction, and is data residency confirmed? | No | Not determined; the feature is off by default; the org must confirm the LLM provider + data-residency before Admin can activate; no code is blocked | Deferred |

### Self-Hosted Keycloak & Bundled Dependencies (ADR-0015)

| ID | Question | Blocking? | Resolution | Status |
|---|---|---|---|---|
| OQ-038 | Where does the self-hosted Keycloak keep its own operational store? | Yes | Dedicated Postgres-for-Keycloak container (confirm via a PH-0 spike); app data stays SQL-only (ADR-0003); backup/restore must cover the chosen store | Deferred |
| OQ-039 | Should ACMP's Keycloak later broker/federate to an organizational IdP? | No | No upstream federation in v1; keep the ACMP realm broker-capable so a future org IdP can be added without rework | Deferred |
| OQ-040 | Which SQL Server edition runs the bundled production instance? | No | Evaluate at deploy phase P18: start with Express but verify columnstore + FTS availability and that the 10GB/DB + memory caps fit ≤20 users; escalate to Standard if limits bind | Deferred |
| OQ-041 | Does production need a self-hosted CI runner + package/registry mirrors? | No | Provision a self-hosted runner before prod cutover (P18); GitHub-hosted runners stay fine for PH-1 CI; ties to OQ-031 and OQ-032 | Deferred |
| OQ-042 | Should ACMP build any in-app user-invite / provisioning affordance? | No | Resolved (2026-06-27): deep-link to the Keycloak admin console only (`_blank`, `rel="noopener"`), carrying no in-app account-creation form; honors ADR-0015 | Approved |
| OQ-043 | Do mutable aggregate roots carry a RowVersion optimistic-concurrency token? | No | Resolved (2026-06-30): added RowVersion + `IsRowVersion()` config + migrations + `DbUpdateConcurrencyException`→409 mapping + a concurrency test; recorded in ADR-0018 | Approved |
| OQ-044 | Should marking notifications read emit AuditEvents? | No | Resolved (2026-07-01): audit the bulk `read-all` sweep only (`Notifications.AllRead`, emitted only when ≥1 item flips); single mark-read stays un-audited | Approved |
| OQ-045 | Enforce the AC-029 downstream-link-required-to-issue gate once Actions exist? | No | Resolved (2026-07-02, P8d): a handler precondition in `IssueDecisionHandler` via the Actions-owned `IActionLinkDirectory`; supersession is auto-exempt; AC-029 → Met | Approved |
| OQ-046 | Mirror each Dependency as a Relationship edge, or compose at read time? | No | Resolved (2026-07-03, P10d): read-time composition — store the Dependency once, merge both modules at read time, no mirror edge; recorded via ASM-016 + docs/domain/search-and-traceability.md §1.1 | Approved |
| OQ-047 | How should non-Topic artifacts carry a stream for the impact graph's cross-stream signal? | No | Default adopted (2026-07-03, P10f): Topic-scope via an `ITopicStreamReader` seam; FR-095 recorded partial; the inherit model stays open pending the multi/zero-topic semantic (ADR-0020) | Deferred |
| OQ-048 | Does the Invariant aggregate need a `Kind` field? | No | Resolved (2026-07-04, P11c): drop Kind — model Category + Scope only per docs/domain/standards-and-best-practices.md §A + FR-106 + the design form; a docs/domain/entity-lifecycles.md §9 correction is owed | Approved |
