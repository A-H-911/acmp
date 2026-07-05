---
status: Approved
version: 1.0.0
updated: 2026-07-06
owner: lead-secretary
---

# Non-Functional Requirements — ACMP

**Purpose:** Measurable non-functional requirements (NFR-###) for ACMP, grouped by quality attribute. Every requirement states a metric/target and a verification method. Covers Deliverable 11.

**Context:** ACMP is a low-traffic, high-sensitivity internal governance tool. **Scale: on-prem, ≤20 total users, ≤~15 concurrent.** Typical concurrent users: 5–15 during a committee meeting. Topics per year: ~200–500. Governed artifacts (total across all types): estimated low thousands at year 3 [unverified — validate with committee secretary]. These numbers set realistic scale targets; do not over-engineer for national-scale throughput. No HA cluster, no horizontal scaling, no heavy performance engineering — 99.9% availability achieved via simple redundancy + nightly backups.

**Note on columns:** Each row's measurable **Target** (metric/threshold) is preserved inline within the Requirement cell; the **Verification** column carries the original verification method. **Priority** is MoSCoW Must for every NFR (each is a binding "shall" quality gate at this scale). The **Source** column records the provenance derived from the quality-attribute section, the settled decisions (`README §A`), the relevant ADR, and the `docs/requirements/non-functional.md` traceability block.

---

## Group 1 — Performance

| ID | Requirement | Source | Priority | Verification |
|---|---|---|---|---|
| NFR-001 | Page load time (P95) for any page rendered with full data (topic list, dashboard, meeting detail) shall not exceed 2 seconds on a standard office network connection (≥10 Mbps). **Target:** P95 ≤ 2 000 ms. | docs/requirements/non-functional.md §Performance; README §A | Must | Load test with Locust or k6: 15 concurrent users, typical navigation paths; measure P95 via OpenTelemetry trace duration. |
| NFR-002 | API response time (P95) for list/query endpoints (backlog list, search results, audit log query) with up to 10 000 topic records shall not exceed 1 second. **Target:** P95 ≤ 1 000 ms. | docs/requirements/non-functional.md §Performance; FR-002 | Must | Integration test: seed 10 000 topics, run 15 concurrent list requests; assert P95 ≤ 1 s via OTel metrics. |
| NFR-003 | API response time (P95) for individual artifact detail retrieval (topic, decision, meeting) shall not exceed 500 ms. **Target:** P95 ≤ 500 ms. | docs/requirements/non-functional.md §Performance; FR-003 | Must | Integration test: measure single-entity GET with full joins under light concurrent load. |
| NFR-004 | Full-text search query (SQL FTS) over topic titles, descriptions, and decision rationale with up to 10 000 records shall return results in ≤ 800 ms P95. **Target:** P95 ≤ 800 ms. | docs/requirements/non-functional.md §Performance; ADR-0011 | Must | Integration test: FTS query on a 10 000-row dataset; measure via OTel trace span for the SQL query step. |
| NFR-005 | Tarseem diagram render (via sidecar Hangfire job) for a diagram spec of typical complexity (≤50 nodes, ≤100 edges) shall complete within 30 seconds of job enqueue. **Target:** P95 ≤ 30 s (end-to-end from enqueue to artifact stored). | docs/requirements/non-functional.md §Performance; ADR-0006 | Must | Integration test: enqueue 10 render jobs simultaneously; measure time from Hangfire enqueue to `IFileStore` write completion. |
| NFR-006 | Meeting notes auto-save (debounce on typing pause) shall complete a server-side persist within 2 seconds of the debounce trigger. **Target:** ≤ 2 000 ms round-trip from debounce trigger to server ACK. | docs/requirements/non-functional.md §Performance; FR-052 | Must | Integration test: simulate typing pause, measure HTTP round-trip for the PATCH request. |
| NFR-007 | Background job execution (Hangfire): action-overdue detection job shall process all open actions and send notifications within 5 minutes of scheduled run time. **Target:** Processing lag ≤ 5 min from scheduled start. | docs/requirements/non-functional.md §Performance; ADR-0014 | Must | Monitor Hangfire job history; assert completion timestamp vs. scheduled time across 10 runs. |

---

## Group 2 — Scale and Capacity

| ID | Requirement | Source | Priority | Verification |
|---|---|---|---|---|
| NFR-008 | The system shall support up to 20 committee user accounts in v1 without degradation in authentication or authorization latency. Right-sized for on-prem, low-traffic deployment. **Target:** 20 user accounts; auth check ≤ 200 ms P95. | docs/requirements/non-functional.md §Scale and Capacity; README §A | Must | Load test: 20 users simultaneously authenticated; measure OIDC token validation + RBAC check via OTel. |
| NFR-009 | The system shall support up to 500 topics per year (2 500 total over a 5-year operational life) without schema migration, index degradation, or query plan regression. **Target:** 2 500 topic records; list query P95 ≤ 1 s (see NFR-002). | docs/requirements/non-functional.md §Scale and Capacity; README §A | Must | Load test with 2 500 seeded topic records; re-run NFR-002 target assertion. |
| NFR-010 | The system shall support 5 active streams with no hard-coded stream limit; stream count is configuration-driven. **Target:** 5 streams at go-live; configurable to ≥20. | docs/requirements/non-functional.md §Scale and Capacity | Must | Code review: confirm no magic number for stream count; integration test with 10 streams. |
| NFR-011 | File attachment storage shall support individual file uploads up to 100 MB; cumulative attachment storage shall be bounded only by the provisioned blob storage capacity (not ACMP application code). **Target:** Single file ≤ 100 MB; no application-level cap on total storage. | docs/requirements/non-functional.md §Scale and Capacity; ADR-0014 | Must | Integration test: upload a 100 MB file; verify successful storage and retrieval; confirm no application-layer total-size enforcement. |
| NFR-012 | The audit log shall support append-only writes at ≥ 100 events/second sustained for 60 seconds without data loss or lock contention on the primary transaction. **Target:** ≥ 100 audit log inserts/s under sustained load. | docs/requirements/non-functional.md §Scale and Capacity; ADR-0009 | Must | Load test: fire 100 concurrent state transitions for 60 s; verify all audit log rows present, no deadlocks in SQL Server error log. |
| NFR-013 | The SQL Server database shall support columnstore-indexed reporting queries (dashboard aggregations) over 3 years of data (estimated 10 000–50 000 rows across governed entities) with result times ≤ 3 seconds. **Target:** Dashboard query P95 ≤ 3 000 ms with 50 000-row dataset. | docs/requirements/non-functional.md §Scale and Capacity; ADR-0003 | Must | Integration test: seed 50 000 rows across governed entity tables; run dashboard aggregation queries; assert duration via OTel. |

---

## Group 3 — Availability and Reliability

| ID | Requirement | Source | Priority | Verification |
|---|---|---|---|---|
| NFR-014 | The system shall be available **24×7** with a monthly availability target of **≥ 99.9%**, achieved via simple redundancy and nightly backups (no HA cluster, no horizontal scaling). Right-sized for on-prem, ≤20 users. **The bundled, self-hosted Keycloak and SQL Server are now within ACMP's own availability and backup scope (ADR-0015)** — not external dependencies. **Target:** ≥ 99.9% monthly uptime; ≤ ~44 min downtime/month. | docs/requirements/non-functional.md §Availability and Reliability; ADR-0015; README §A | Must | Monitor via ASP.NET health check endpoint polled every 60 s; alert on 3 consecutive failures; monthly availability report from self-hosted Seq. |
| NFR-015 | Scheduled maintenance windows (for upgrades, backups, or ops tasks) are acceptable with ≥ 24 h advance notice to committee members; planned windows do not count against availability SLA. **Target:** Maintenance windows documented in runbook; notification template for committee members. | docs/requirements/non-functional.md §Availability and Reliability | Must | Deployment runbook specifies maintenance window process. |
| NFR-016 | A single application process failure (unhandled exception, OOM) shall not result in data loss; the system shall recover from restart without manual intervention within 2 minutes. **Target:** Recovery time after process restart ≤ 2 min; zero data loss for committed transactions. | docs/requirements/non-functional.md §Availability and Reliability | Must | Chaos test: kill the container process mid-request (non-write); restart; verify no orphaned or corrupted rows; measure restart time. |
| NFR-017 | Hangfire background job failures shall be retried automatically up to 3 times with exponential backoff (default Hangfire retry policy); persistent failures shall alert Administrator via self-hosted Seq alert rule. **Target:** Retry on failure; alert after 3 consecutive failures. | docs/requirements/non-functional.md §Availability and Reliability; ADR-0014 | Must | Inject artificial job failure; observe Hangfire retry in history; verify Seq alert fires after 3rd failure. |

---

## Group 4 — Security

| ID | Requirement | Source | Priority | Verification |
|---|---|---|---|---|
| NFR-018 | The application shall meet OWASP ASVS 5.0 Level 2 requirements for all applicable chapters. **Target:** 100% of L2 mandatory requirements pass. | docs/domain/security-controls.md; OWASP ASVS 5.0 | Must | ASVS-guided security review (see `docs/domain/security-controls.md`); automated DAST scan (OWASP ZAP or equivalent) on staging; manual penetration test before production go-live. |
| NFR-019 | All inter-service communication (application ↔ SQL Server, application ↔ Tarseem sidecar, application ↔ Keycloak) shall use TLS 1.2 or higher; TLS 1.0 and 1.1 shall be disabled. **Target:** TLS ≥ 1.2 on all connections. | docs/domain/security-controls.md; docs/requirements/non-functional.md §Security | Must | TLS scan (e.g., `nmap --script ssl-enum-ciphers`) on all internal endpoints; configuration review of `appsettings`. |
| NFR-020 | Authentication tokens (JWT from Keycloak) shall be validated on every API request; requests without a valid token return HTTP 401; requests with insufficient role/scope return HTTP 403. No endpoint is unauthenticated except health checks and the OIDC callback. **Target:** 0 unauthenticated non-health endpoints; 401/403 on invalid/insufficient token. | docs/domain/security-controls.md; ADR-0004 | Must | Automated integration tests: call every endpoint without token (expect 401), with wrong role (expect 403); verified in CI. |
| NFR-021 | All user input shall be validated server-side; SQL parameters shall use parameterized queries or EF Core LINQ only; raw SQL string concatenation with user input is prohibited. **Target:** 0 SQL injection vectors detected by SAST scan. | docs/domain/security-controls.md; docs/requirements/non-functional.md §Security | Must | SAST scan (e.g., Semgrep, SonarQube) in CI pipeline; code review gate prohibits raw string SQL with user input. |
| NFR-022 | The application shall implement CSRF protection for all state-changing requests (POST, PUT, PATCH, DELETE) using the ASP.NET Core anti-forgery token or SameSite cookie policy. **Target:** 0 CSRF vulnerabilities per OWASP ZAP scan. | docs/domain/security-controls.md; OWASP ASVS V4 | Must | DAST scan on staging; manual CSRF test per ASVS V4. |
| NFR-023 | Session tokens shall expire after 8 hours of inactivity [unverified — confirm with org security policy]; refresh tokens shall not extend sessions beyond 24 hours without re-authentication. **Target:** Session idle timeout ≤ 8 h; absolute max ≤ 24 h. | docs/domain/security-controls.md; ADR-0004 | Must | Integration test: issue token, idle for 8 h (simulated via clock manipulation), verify 401 on next call; validate Keycloak session configuration. |
| NFR-024 | All application secrets (database connection strings, Keycloak client secret, S3/Blob credentials, API keys) shall be managed via environment variables or a secrets manager; no secrets shall appear in source code, build artifacts, or container images. **Target:** 0 secrets in source code or images (validated by secret scanning). | docs/domain/security-controls.md; docs/requirements/non-functional.md §Security | Must | SAST secret scan (e.g., `gitleaks`, `trufflehog`) in CI; Docker image scan in CI pipeline. |
| NFR-025 | Transcript and recording data shall be accessible only to roles explicitly authorized (Chairman, Secretary, Auditor); all access to transcript content shall generate an audit log entry. **Target:** Role-gated access; 100% of transcript reads logged. | docs/domain/security-threat-model.md; ADR-0009 | Must | Integration test: attempt transcript access with each role; verify 403 for unauthorized; verify audit log entry for authorized access. |
| NFR-026 | The application shall be protected against OWASP LLM01 (Prompt Injection) in all AI-extraction features: all LLM inputs from meeting transcripts shall be treated as untrusted data; LLM-generated outputs shall never be committed to the system record without explicit human approval action. **Target:** 0 AI-generated content auto-committed; every AI candidate has status=Candidate until human approves. | docs/domain/security-threat-model.md; OWASP LLM Top 10 | Must | Code review and integration test: verify no pathway from AI extraction to committed record without explicit `approve` API call by authorized user. |

---

## Group 5 — Privacy

| ID | Requirement | Source | Priority | Verification |
|---|---|---|---|---|
| NFR-027 | Meeting recording files and transcripts are sensitive data; they shall be stored in the object store (self-hosted MinIO) with access-controlled pre-signed URLs (time-limited, ≤ 1 h expiry); the URL is never embedded in logs or notifications. **Target:** Pre-signed URLs expire ≤ 1 h; URLs absent from logs. | docs/requirements/non-functional.md §Privacy; ADR-0014 | Must | Code review; integration test: generate URL, wait 1 h, verify URL no longer valid; log search: verify URL pattern not in Seq output. |
| NFR-028 | Structured logs and OTel traces shall not record personally identifiable information (PII) beyond user ID (pseudonymous); full names, email addresses, and vote content shall not appear in log payloads. **Target:** 0 PII fields in log/trace output. | docs/requirements/non-functional.md §Privacy | Must | Log review: run privacy-sensitive operations (vote, login), inspect Seq output; verify no name/email in log body. |
| NFR-029 | Voting is always attributed in v1 (vote anonymity is out of scope for v1, per ADR-0010). Individual voter choices are recorded in the audit log and accessible to Auditor and Chairman; the results view displays both individual choices and aggregate totals. **Target:** 100% of vote records are attributed; no anonymization pathway in v1. | docs/requirements/non-functional.md §Privacy; ADR-0010 | Must | Integration test: cast votes; call results API; verify voter attribution is recorded; verify audit log entries are attributed. |

---

## Group 6 — Accessibility

| ID | Requirement | Source | Priority | Verification |
|---|---|---|---|---|
| NFR-030 | All user interface components shall conform to WCAG 2.2 Level AA (W3C Recommendation, October 2023). **Target:** 0 WCAG 2.2 AA failures per automated + manual audit. | ADR-0012; WCAG 2.2 AA | Must | Automated: axe-core or Lighthouse CI on all page templates in CI; manual: keyboard-only navigation test, screen-reader test (NVDA/JAWS) on critical flows (login, backlog, vote, search). |
| NFR-031 | All interactive elements (buttons, links, form fields, DnD handles) shall be keyboard accessible; drag-and-drop operations shall have a keyboard-accessible alternative (move up/down controls). **Target:** 100% of interactive elements reachable and operable via keyboard. | ADR-0012; WCAG 2.2 AA | Must | Keyboard-only navigation test: tab through every page; verify no keyboard trap; verify DnD alternative controls present. |
| NFR-032 | All non-decorative images, icons, and data visualizations (charts, diagrams) shall have appropriate alternative text or ARIA labels in both EN and AR. **Target:** 0 non-decorative images without alt text per axe-core scan. | ADR-0012; WCAG 2.2 AA | Must | Automated: axe-core in CI; manual: screen reader spot-check on dashboard and diagram views. |
| NFR-033 | Color contrast ratio for all text on background and UI component states (normal, hover, focused, disabled) shall meet WCAG 2.2 AA minimum (4.5:1 for body text, 3:1 for large text and UI components). **Target:** Contrast ratio ≥ 4.5:1 (body), ≥ 3:1 (large/UI). | ADR-0012; WCAG 2.2 AA | Must | Automated: Lighthouse contrast check in CI; manual: contrast check (e.g., Colour Contrast Analyser) on light and dark themes, both EN/AR layouts. |
| NFR-034 | Status indicators (topic urgency, action overdue, risk severity) shall not rely on color alone; they shall also use text labels and/or icons with accessible names. **Target:** 0 color-only status indicators per WCAG 1.4.1. | ADR-0012; WCAG 2.2 (1.4.1) | Must | Code review: verify every status badge has a text label or icon with aria-label; axe-core check. |

---

## Group 7 — Localization and Internationalization (EN/AR, RTL)

| ID | Requirement | Source | Priority | Verification |
|---|---|---|---|---|
| NFR-035 | All UI strings shall be externalized in `react-i18next` resource files; no hardcoded display strings in component code. **Target:** 0 hardcoded EN strings in component JSX/TSX. | ADR-0012; docs/requirements/non-functional.md §Localization | Must | Static analysis: ESLint rule or grep for JSX string literals not in i18n functions; enforced in CI. |
| NFR-036 | The Arabic locale shall trigger full RTL layout via the `dir="rtl"` attribute on `<html>` and CSS logical properties throughout; no `left`/`right` CSS properties shall appear in component stylesheets. **Target:** 0 `padding-left`, `margin-right`, etc. in component CSS when RTL active. | ADR-0012 | Must | Automated: CSS lint rule prohibiting physical direction properties; visual regression test: screenshot in AR/RTL vs. EN/LTR and compare layout direction. |
| NFR-037 | All date, time, and number formats shall use locale-appropriate formatting: Gregorian calendar in both EN and AR (v1 only; Hijri display is a future option, not in v1). **Target:** Gregorian dates rendered correctly in both locales; no raw ISO strings visible. | ADR-0012; docs/requirements/non-functional.md §Localization | Must | Integration test: create a topic in EN, switch to AR, verify date display format changes correctly (Gregorian); no raw ISO strings visible. |
| NFR-038 | Tarseem diagram output shall render Arabic text correctly (HarfBuzz shaping, RTL geometry mirror) when topic language is AR or diagram content includes Arabic labels. **Target:** 0 garbled Arabic text or incorrect glyph ordering in diagram exports. | ADR-0012; ADR-0006 | Must | Manual: generate a diagram with Arabic node labels; inspect SVG text elements for correct Unicode code point order; compare with Tarseem's RTL test output. |
| NFR-039 | The EN↔AR terminology glossary (README §G, localized in design handoff) shall be the single source of canonical bilingual terms; no term shall have more than one AR translation within the application. **Target:** 0 terminology inconsistencies detected in the i18n resource files. | README §G; ADR-0012 | Must | Translation review: compare i18n `ar.json` keys against glossary; flag duplicates; manual review by Arabic-speaking stakeholder [unverified — confirm reviewer availability]. |

---

## Group 8 — Auditability and Immutability

| ID | Requirement | Source | Priority | Verification |
|---|---|---|---|---|
| NFR-040 | The audit log table shall have no `UPDATE` or `DELETE` triggers, constraints, or ORM operations defined; it is insert-only at the application layer. **Target:** 0 UPDATE/DELETE statements targeting the audit log table in application code. | ADR-0009; docs/domain/audit-and-records.md | Must | Code review: search codebase for any EF Core or raw SQL touching audit log with UPDATE/DELETE; SAST rule. |
| NFR-041 | Votes (individual vote records) and issued decisions (DECN-…) shall be immutable after their respective immutability events (vote close, decision issue); no API endpoint shall permit field mutation on these records after the event. **Target:** 0 API endpoints permitting mutation of closed vote or issued decision fields. | ADR-0009; docs/domain/audit-and-records.md | Must | Integration test: close a vote, attempt PUT/PATCH on vote fields, expect HTTP 409 Conflict; repeat for issued decision. |
| NFR-042 | Every state transition on a governed entity shall generate a corresponding audit log entry within the same database transaction; no state transition without an audit log entry is a system defect. **Target:** 100% of state transitions have a corresponding audit log entry within the same TX. | ADR-0009; docs/domain/audit-and-records.md | Must | Integration test: intercept DB commit, verify audit log row present for every transition type; DB-level assertion. |

---

## Group 9 — Observability

| ID | Requirement | Source | Priority | Verification |
|---|---|---|---|---|
| NFR-043 | Every inbound HTTP request shall generate an OTel trace with spans for: HTTP handler, DB query, Hangfire job dispatch, and external HTTP calls (Webex, Tarseem sidecar). **Target:** 100% of requests traced (sample rate 100% for this traffic volume). | ADR-0014; README §A | Must | OTel dashboard: verify trace completeness for a sample of requests across all module endpoints. |
| NFR-044 | Structured log entries (Serilog) shall include: `CorrelationId`, `UserId` (pseudonymous), `Module`, `Operation`, `ElapsedMs`, and `LogLevel`; minimum log level in production is `Information`. All logs shipped to the app-owned self-hosted Seq container. **Target:** All log entries contain required fields. | ADR-0014; README §A | Must | Log integration test: trigger each module's main operation; verify required fields present in Seq output. |
| NFR-045 | Application health check endpoints shall respond within 1 second and return: application status, database connectivity, Hangfire connectivity, Seq connectivity, MinIO connectivity, and Tarseem sidecar status (Phase 2). **Target:** Health check response ≤ 1 000 ms; includes all configured sub-checks. | ADR-0014; docs/requirements/non-functional.md §Observability | Must | Integration test: call `/health`; verify response time and all sub-check fields; fail if any sub-check missing. |
| NFR-046 | Alerts shall be configured in self-hosted Seq for: application error rate > 5 errors/min, Hangfire failed jobs > 3 in 10 min, health check failure ≥ 3 consecutive, and audit log write failure (any). **Target:** Alert fires within 60 s of threshold crossing. | ADR-0014; docs/requirements/non-functional.md §Observability | Must | Alert integration test: inject errors artificially; verify alert fires within 60 s via Seq alert sink. |

---

## Group 10 — Maintainability

| ID | Requirement | Source | Priority | Verification |
|---|---|---|---|---|
| NFR-047 | Each canonical module (Membership, Topics, Meetings, Decisions, Actions, Risks, Dependencies, Governance, Research, Knowledge, Diagrams, Notifications, Reporting, Search&Traceability, Audit&Records, Platform) shall be implemented as a distinct project or folder boundary; no module shall reference another module's internal types — only its public contract interface or MediatR command/query. **Target:** 0 cross-module internal type references detected by ArchUnit or dependency analysis rule. | ADR-0001; ADR-0002; docs/domain/repository-structure.md | Must | CI: ArchUnit (or NDepend) rule: no `using` of `<ModuleA>.Internal.*` from `<ModuleB>.*`; enforced as build failure. |
| NFR-048 | Unit test coverage for domain logic (entity state machines, business rules, validation) shall be ≥ 80% line coverage. **Target:** ≥ 80% line coverage on domain layer projects. | ADR-0016; docs/validation/test-strategy.md | Must | CI: `dotnet test --collect:\"XPlat Code Coverage\"` with coverage threshold gate; fails build below threshold. |
| NFR-049 | Integration test coverage shall include at least one end-to-end happy-path test and one failure-path test per canonical workflow (topic intake, agenda creation, meeting + MoM, vote + decision, action lifecycle). **Target:** ≥ 1 happy-path + 1 failure-path integration test per workflow (5 workflows × 2 = ≥ 10 integration tests). | ADR-0016; docs/validation/test-strategy.md | Must | CI: integration test suite count ≥ 10; test report reviewed in PR. |
| NFR-050 | Database schema migrations shall be backward-compatible with the running application version (expand-contract pattern); no migration shall drop a column or table that the current application version still reads. **Target:** 0 breaking schema changes in migration without a corresponding application code change in the same PR. | docs/requirements/non-functional.md §Maintainability; docs/domain/repository-structure.md | Must | PR review gate: any migration dropping a column/table requires explicit reviewer sign-off and a matching code change. |
| NFR-051 | All third-party dependencies shall be declared in `package.json` / `.csproj` with pinned or semver-constrained versions; Dependabot or equivalent shall be configured to alert on known CVEs within 48 hours. **Target:** 0 unpinned wildcard dependencies; CVE alerts ≤ 48 h. | docs/requirements/non-functional.md §Maintainability; docs/domain/security-controls.md | Must | Dependency audit in CI (`npm audit`, `dotnet list package --vulnerable`); Dependabot config in repo. |

---

## Group 11 — Portability and Deployment

| ID | Requirement | Source | Priority | Verification |
|---|---|---|---|---|
| NFR-052 | The application shall be fully containerized (Docker); a `docker-compose.yml` shall run the complete local development stack (app, SQL Server, self-hosted Seq, self-hosted MinIO, Tarseem sidecar [Phase 2]) with a single `docker compose up` command. **Target:** Single-command local startup ≤ 5 min on a standard developer machine (8-core, 16 GB RAM). | ADR-0013; README §A | Must | Manual test: fresh clone → `docker compose up`; verify all services healthy and app accessible within 5 min. |
| NFR-053 | All application configuration shall be externalized; switching deployment environments (dev/staging/prod) requires only environment variable changes, not code changes or image rebuilds. **Target:** 0 environment-specific values hardcoded in source code or Dockerfile. | ADR-0013; docs/requirements/non-functional.md §Portability | Must | Code review: grep for known environment values (dev hostname, staging DB name); SAST scan. |
| NFR-054 | Container images shall be built from a minimal base image (e.g., `mcr.microsoft.com/dotnet/aspnet:8.0-alpine` or equivalent distroless); image size shall not exceed 500 MB. **Target:** Image size ≤ 500 MB. | ADR-0013; docs/requirements/non-functional.md §Portability | Must | CI: `docker image inspect` after build; assert size ≤ 500 MB; base image layer verified. |
| NFR-055 | The system shall not require Kubernetes, a service mesh, or a message broker to run; Docker Compose or equivalent single-host container orchestration is the supported deployment model in v1. **Target:** 0 Kubernetes-specific manifests or broker dependencies in v1 deployment artifacts. | ADR-0013; CON-001 | Must | Architecture review; docker-compose.yml is the deployment artifact for v1. |

---

## Group 12 — Backup, Disaster Recovery, and Data Retention

| ID | Requirement | Source | Priority | Verification |
|---|---|---|---|---|
| NFR-056 | Recovery Point Objective (RPO): in the event of a catastrophic failure, data loss shall not exceed 4 hours (i.e., database backups occur at least every 4 hours during business days). **Target:** RPO ≤ 4 h. | docs/domain/deployment.md; docs/requirements/non-functional.md §Backup and DR | Must | Backup schedule configuration review; restore test from most recent backup; verify data completeness to within 4 h of failure. |
| NFR-057 | Recovery Time Objective (RTO): the system shall be restorable to a functional state within 8 business hours of a declared disaster. **Target:** RTO ≤ 8 h. | docs/domain/deployment.md; docs/requirements/non-functional.md §Backup and DR | Must | DR drill: restore from backup to a clean environment; measure time from restore initiation to verified-healthy health check. |
| NFR-058 | Database backups shall be stored in a location separate from the primary database server; backups shall be tested for restorability at least quarterly. **Target:** Backups on separate storage; quarterly restore test. | docs/domain/deployment.md; docs/requirements/non-functional.md §Backup and DR | Must | Backup policy review; quarterly restore test log. |
| NFR-059 | All records (including audit log data) are **retained indefinitely in v1; no automatic purge**. Retention periods are **configurable** so legal/compliance can set them later. Immutable records (votes, decisions, ADRs, MoM) are never purged. Specific retention periods (e.g., years) will be determined by org compliance/legal [unverified — validate with org]. **Target:** No automatic purge in v1; configurable retention; immutable records protected. | docs/domain/audit-and-records.md; ADR-0009 | Must | Data retention policy document; archive procedure in `docs/domain/audit-and-records.md`; verify no automated deletion job exists in v1. |
| NFR-060 | File attachments (presentations, recordings, diagrams) are **retained; no automatic purge in v1**. Retention periods are configurable and will be set by legal/ops. No automated archival job deletes attachments in v1. **Target:** Configurable retention; no automatic purge in v1. | docs/domain/audit-and-records.md; ADR-0014 | Must | Data retention policy document; verify no automated attachment-deletion job exists in v1. |

---

## Group 13 — Compatibility

| ID | Requirement | Source | Priority | Verification |
|---|---|---|---|---|
| NFR-061 | The web application shall support the latest two major versions of: Google Chrome, Microsoft Edge, Mozilla Firefox, and Apple Safari as of the release date. No Internet Explorer support. **Target:** Functional on Chrome N/N-1, Edge N/N-1, Firefox N/N-1, Safari N/N-1. | docs/requirements/non-functional.md §Compatibility | Must | Manual cross-browser test of critical workflows (login, backlog, vote, MoM) on all 4 browsers × 2 versions; verified pre-release. |
| NFR-062 | The application shall be usable on tablet devices (iPad or equivalent, landscape orientation, ≥1024 px viewport width) with no horizontal scroll and no overlapping UI elements. **Target:** 0 UI defects on 1024 px viewport (tablet landscape). | docs/requirements/non-functional.md §Compatibility; ADR-0012 | Must | Manual: render on iPad (or 1024 px browser window) in both EN/LTR and AR/RTL; verify layout on backlog kanban, topic detail, and meeting MoM views. |
| NFR-063 | The application is not required to support mobile phone viewports (< 768 px) in v1; a "not optimized for mobile" notice is acceptable on narrow viewports. **Target:** Mobile (< 768 px) displays a graceful not-optimized notice; no broken layout. | docs/requirements/non-functional.md §Compatibility | Must | Manual: open on 375 px viewport; verify notice; verify no JS error or crash. |

---

## Traceability

- NFR-001–007 (Performance) → `docs/requirements/functional.md` FR-002–004 (page/query operations); `docs/planning/roadmap.md` Phase 1 acceptance criteria
- NFR-018–026 (Security) → `docs/domain/security-threat-model.md`, `docs/domain/security-controls.md`; references OWASP ASVS 5.0 (https://owasp.org/www-project-application-security-verification-standard/) and OWASP LLM Top 10
- NFR-030–034 (Accessibility) → WCAG 2.2 AA (W3C Recommendation, Oct 2023); `docs/domain/information-architecture.md` (design constraints)
- NFR-035–039 (Localization) → ADR-0012; `README.md §G` (glossary); `design-handoff/` (EN↔AR term pairs)
- NFR-040–042 (Auditability) → ADR-0009; `docs/domain/audit-and-records.md`
- NFR-047–051 (Maintainability) → ADR-0001 (modular monolith); `docs/domain/repository-structure.md`; `docs/validation/test-strategy.md`
- NFR-014 (Availability) → 24×7 / 99.9%; on-prem, ≤20 users; simple redundancy + nightly backups; see README §A
- NFR-056–060 (Backup/DR) → `docs/domain/audit-and-records.md`; `docs/domain/deployment.md`
- [unverified] items → raise as `OQ-###` in `docs/decisions/open-decision-register.md`
