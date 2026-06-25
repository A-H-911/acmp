# 45 — Release Readiness Checklist (Deliverable 58)

**Purpose:** Concrete pre-release gate checklist for every production deployment of ACMP. Must be completed in full and signed off before the release secretary approves the deployment window. Items marked **[BLOCK]** are hard blockers; deployment must not proceed if any [BLOCK] item is unchecked or failing.

**How to use:** Print or copy this checklist into the release ticket. Check each item with the responsible party's initials and date. Attach evidence (scan results, test run links, backup confirmation) to the release ticket before sign-off.

---

## Group 1 — Functionality

| # | Check | Owner | Status |
|---|---|---|---|
| F-01 | **[BLOCK]** All Must-priority (M) FR-IDs in this release scope have a passing AC-### (G-TRACE gate verified). | QA Lead | ☐ |
| F-02 | **[BLOCK]** All PH-1 user stories (US-001 through US-084) are in "Done" status with QA-verified acceptance criteria. | QA Lead | ☐ |
| F-03 | **[BLOCK]** End-to-end governance loop test passed in staging: topic submit → triage → accept → prepare → schedule → agenda publish → meeting create → attendance record → live notes → vote configure/open/close → chairman ratify → decision record (Issued) → action create → action verify → MoM draft/approve/publish. | QA Lead + Secretary | ☐ |
| F-04 | **[BLOCK]** All automated unit and integration tests pass on CI (zero failures; zero flaky tests on last 3 runs). | Tech Lead | ☐ |
| F-05 | **[BLOCK]** All E2E tests (Playwright or equivalent) pass on staging CI run. | QA Lead | ☐ |
| F-06 | Permission matrix tests pass: every role × action combination from `docs/10-permission-role-matrix.md §C` verified (Allow cells allow, Deny cells return HTTP 403). | QA Lead | ☐ |
| F-07 | SoD rule tests pass: SoD-1 (Action.Verify ≠ owner), SoD-2 (MoM sole-author flag), SoD-3 (chairman not sole vote-counter), SoD-5 (Administrator cannot act on committee content) all verified by integration tests. | QA Lead | ☐ |
| F-08 | Carry-over suggestions tested: agenda builder suggests unresolved items from prior meeting. | QA Lead | ☐ |
| F-09 | Aging indicator tested: topic aged past SLA shows visual badge and Secretary notification fired. | QA Lead | ☐ |
| F-10 | Overdue action derivation tested: action past due date shows Overdue badge without user action. | QA Lead | ☐ |
| F-11 | Hangfire reminder job tested: action reminder notification sent to owner N days before due date at configured threshold. | QA Lead | ☐ |
| F-12 | Hangfire escalation job tested: overdue-past-threshold escalation notification sent to Secretary + Chairman. | QA Lead | ☐ |
| F-13 | Decision immutability tested: attempt to edit Issued decision via API returns error; field unchanged (AC-027). | QA Lead | ☐ |
| F-14 | Vote immutability tested: attempt to modify closed vote ballot returns error; tally unchanged (AC-025). | QA Lead | ☐ |
| F-15 | MoM versioning tested: correction to Published MoM creates a new version; prior version remains readable (AC-036). | QA Lead | ☐ |
| F-16 | Unsaved-work guard tested: navigating away from a modified form triggers confirmation prompt (AC-047). | QA Lead | ☐ |
| F-17 | Global FTS search tested: EN and AR queries return relevant results grouped by artifact type (AC-060, AC-061). | QA Lead | ☐ |
| F-18 | Traceability panel tested: artifact detail pages show upstream and downstream typed relationships (AC-062). | QA Lead | ☐ |
| F-19 | All three dashboards (committee, secretary, chairman) display live data matching database state (AC-064, AC-065, AC-066). | QA Lead | ☐ |

---

## Group 2 — Security

| # | Check | Owner | Status |
|---|---|---|---|
| S-01 | **[BLOCK]** OWASP ASVS L2 checklist reviewed against `docs/25-security-controls.md`; no unmitigated High or Critical findings. Evidence: signed ASVS L2 review sheet attached. | Security Reviewer | ☐ |
| S-02 | **[BLOCK]** SAST scan (Semgrep or equivalent) clean: zero unmitigated High/Critical code-level findings. Low/Medium findings triaged with documented rationale. | Security Reviewer | ☐ |
| S-03 | **[BLOCK]** DAST scan (OWASP ZAP or equivalent) run against staging environment: zero unmitigated High/Critical dynamic findings. Scan report attached. | Security Reviewer | ☐ |
| S-04 | **[BLOCK]** Dependency vulnerability scan (`dotnet list package --vulnerable` + `npm audit`): zero unmitigated High/Critical CVEs in production dependencies. | Tech Lead | ☐ |
| S-05 | **[BLOCK]** Secret scan (truffleHog or gitleaks) run against full repository history and Docker image layers: zero confirmed secrets detected. | Tech Lead | ☐ |
| S-06 | **[BLOCK]** Container image vulnerability scan (e.g., Trivy) on all images (ACMP app, SQL Server, Seq, MinIO base): zero unmitigated High/Critical CVEs. | Tech Lead | ☐ |
| S-07 | **[BLOCK]** All secrets externalized: connection strings, Keycloak client secret, MinIO credentials, and any API keys are supplied via environment variables or secrets manager; none appear in `docker-compose.yml`, Dockerfiles, or committed config files (FR-013). | Tech Lead | ☐ |
| S-08 | **[BLOCK]** TLS enforced on all external-facing ACMP endpoints; certificate valid (not expired, issued by a trusted CA, matching hostname); HTTPS redirect active. | Tech Lead | ☐ |
| S-09 | **[BLOCK]** Audit immutability verified in staging: (a) test confirms direct UPDATE/DELETE on audit_log table is rejected by DB constraint or application guard; (b) hash-chain integrity check tool returns 0 broken links on full staging audit log. Evidence attached. | Tech Lead + Security Reviewer | ☐ |
| S-10 | **[BLOCK]** Authentication tested: unauthenticated requests to any protected endpoint return HTTP 401; unauthorized role requests return HTTP 403; test for each boundary in permission matrix (AC-005 through AC-011). | QA Lead | ☐ |
| S-11 | Keycloak OIDC token validation confirmed: expired tokens are rejected; tokens with invalid signatures are rejected; role claims map to ACMP roles correctly for all 8 canonical roles. | Security Reviewer + Tech Lead | ☐ |
| S-12 | ABAC scope enforcement confirmed: stream-scope ABAC test passes (Member in Stream-A cannot mutate Stream-B-only topic); confidentiality=Restricted topic not visible to out-of-scope Member (AC-009, AC-010). | QA Lead | ☐ |
| S-13 | Input validation confirmed: SQL injection attempt on search and form fields returns 400 (no DB error); XSS payload in Markdown fields is sanitized on render (no script execution). | Security Reviewer | ☐ |
| S-14 | Pre-signed file URLs confirmed: MinIO attachment download URLs are time-limited (configurable, default 1 hour); expired URLs return 403 from MinIO. | Tech Lead | ☐ |

---

## Group 3 — Data

| # | Check | Owner | Status |
|---|---|---|---|
| D-01 | **[BLOCK]** All EF Core migration scripts for this release applied to staging cleanly (zero errors, no data-loss warnings in migration output log). | Tech Lead | ☐ |
| D-02 | **[BLOCK]** Rollback plan confirmed: rollback migration script (or documented restore-from-backup procedure) applied to staging and prior release confirmed functional. Estimated rollback time documented. | Tech Lead | ☐ |
| D-03 | **[BLOCK]** Pre-release backup of production database taken (if production exists); backup verified by partial restore test (at minimum: database comes up, schema version matches pre-release schema). Backup filename and timestamp recorded. | Tech Lead | ☐ |
| D-04 | Seed/reference data verified in staging: canonical roles, canonical topic types, canonical decision outcomes, canonical relationship types, default system notification event codes are all present and correct. | Tech Lead + QA Lead | ☐ |
| D-05 | Database-level constraints verified: audit log table has a DB-level guard preventing UPDATE/DELETE; immutable entity tables (Vote after Closed, Decision after Issued) have equivalent guards or are enforced exclusively at the application layer (with the DAST/mutation test from S-09 as evidence). | Tech Lead | ☐ |
| D-06 | SQL Server FTS indexes confirmed built for EN and AR word-breakers on required columns (topics, decisions, MoM); a test query returns results in both languages (AC-060, AC-061). | Tech Lead | ☐ |
| D-07 | MinIO bucket configuration confirmed: ACMP bucket exists; access policy is private (no public-read); bucket versioning enabled (or documented rationale for why not). | Tech Lead | ☐ |

---

## Group 4 — Localization

| # | Check | Owner | Status |
|---|---|---|---|
| L-01 | **[BLOCK]** 100% of i18n keys present in both EN (`en.json`) and AR (`ar.json`) resource files; build-time i18n completeness check passes with zero missing keys. | Frontend Engineer | ☐ |
| L-02 | **[BLOCK]** RTL visual regression test passed on all routes for the AR locale: automated screenshot comparison or QA sign-off by AR-fluent reviewer confirms no LTR artifacts, no cropped Arabic text, no misaligned directional icons. Evidence (screenshot set or sign-off) attached. | Frontend Engineer + AR Reviewer | ☐ |
| L-03 | **[BLOCK]** Zero hardcoded English (or Arabic) strings in rendered HTML verified by i18n linting or automated DOM scan on all routes. | Frontend Engineer | ☐ |
| L-04 | Bilingual glossary terms consistent across the release: all committee terms (Topic, Agenda, Minutes, Decision, Vote, Action, Risk, ADR, etc.) match the signed-off glossary from PH-0 in both EN and AR. | Secretary + AR Reviewer | ☐ |
| L-05 | Locale switch tested: switching from EN to AR mid-session with unsaved form data preserves data; all labels change to AR; RTL layout activates immediately (AC-039, AC-040). | QA Lead | ☐ |
| L-06 | Date formatting verified: all date/time values render in Gregorian format in both EN and AR locales; no Hijri dates present (resolved decision: Gregorian only). | QA Lead | ☐ |
| L-07 | Error and validation messages verified in AR: required-field errors, file-size errors, role-denied errors all render in correct Arabic in the AR locale (FR-014, AC-030). | QA Lead | ☐ |

---

## Group 5 — Accessibility

| # | Check | Owner | Status |
|---|---|---|---|
| A-01 | **[BLOCK]** Automated accessibility scan (axe-core or equivalent) run on all page routes in both EN and AR locales: zero Critical violations; High violations documented and either fixed or accepted with explicit rationale. Scan report attached. | QA Lead | ☐ |
| A-02 | **[BLOCK]** Keyboard-only navigation test on critical paths completed: login → topic submit → triage → agenda build → vote cast → chairman approve → MoM approve. No keyboard traps detected; all interactive elements reachable via Tab; focus rings visible throughout (AC-043, AC-044, AC-045). | QA Lead | ☐ |
| A-03 | **[BLOCK]** Drag-and-drop alternatives verified: backlog kanban move-up/move-down controls function via keyboard only; agenda-item reorder keyboard alternative functions (FR-034, FR-047, AC-043, AC-044). | QA Lead | ☐ |
| A-04 | All form controls have programmatically associated labels; all icon-only buttons have `aria-label`; verified by automated scan + manual review of any newly added components (AC-046). | QA Lead | ☐ |
| A-05 | Color contrast ratio ≥ 4.5:1 for normal text and ≥ 3:1 for large text verified on all new or modified UI components in both light and dark themes; verified via automated check or manual contrast tool. | Frontend Engineer | ☐ |
| A-06 | Screen reader smoke test (NVDA or VoiceOver) completed on: committee dashboard, topic detail page, voting UI; major interactive flows are announced correctly and operable without a mouse. | QA Lead | ☐ |
| A-07 | Reading order in DOM matches visual order in both LTR (EN) and RTL (AR) layouts, verified by manual inspection of new components (AC-046). | Frontend Engineer | ☐ |

---

## Group 6 — Performance

| # | Check | Owner | Status |
|---|---|---|---|
| P-01 | Smoke-load test run against staging with representative data (≥50 topics, ≥10 meetings, ≥200 audit log entries, all modules populated): all pages load within 3 seconds at single-user load; no 5xx errors. | Tech Lead | ☐ |
| P-02 | Memory and CPU stable over 30-minute soak test at normal single-user load (matching target: ≤20 total users); no memory leak detected in ACMP container. | Tech Lead | ☐ |
| P-03 | Hangfire job queue clear at rest: no jobs stalled or queued for >5 minutes under idle conditions after the soak test. | Tech Lead | ☐ |
| P-04 | SQL Server query plans reviewed for any new queries introduced in this release; no unexpected table scans on tables with >100 rows; query hints or indexes added where needed. | Tech Lead | ☐ |

---

## Group 7 — Observability

| # | Check | Owner | Status |
|---|---|---|---|
| O-01 | **[BLOCK]** Serilog → Seq pipeline verified: a test log entry at Error level generated in staging appears in the self-hosted Seq container within 30 seconds; entry includes correlation ID, masked user ID, module name, and operation (FR-009). | Tech Lead | ☐ |
| O-02 | **[BLOCK]** Health check endpoints verified: GET `/health/live` and GET `/health/ready` both return HTTP 200 with expected body in the staging Docker Compose stack (FR-007). | Tech Lead | ☐ |
| O-03 | OpenTelemetry traces visible in Seq (or configured trace backend) for at least one inbound HTTP request and one Hangfire background job (FR-010). | Tech Lead | ☐ |
| O-04 | Hangfire dashboard accessible to Administrator role in staging; last-run timestamps of reminder and escalation jobs are correct; no failed jobs without acknowledged resolution. | Tech Lead | ☐ |
| O-05 | Error-rate alerting in Seq confirmed: test 5xx event triggers the configured alert rule; alert visible in Seq within 1 minute. | Tech Lead | ☐ |

---

## Group 8 — Operability

| # | Check | Owner | Status |
|---|---|---|---|
| OP-01 | **[BLOCK]** Operational runbook updated and reviewed: covers startup/shutdown sequence, log viewing in Seq, backup/restore procedure, Keycloak claim troubleshooting, known issues/mitigations, and emergency contact list. Runbook location documented. | Tech Lead | ☐ |
| OP-02 | **[BLOCK]** Rollback procedure documented and tested (see D-02); rollback confirmed completable within 30 minutes. | Tech Lead | ☐ |
| OP-03 | **[BLOCK]** Docker Compose file (`docker-compose.yml`) validated: all containers (ACMP app, SQL Server, Seq, MinIO) start in the correct order with healthcheck dependencies; no manual intervention required for a clean startup from scratch. | Tech Lead | ☐ |
| OP-04 | Container resource limits (CPU, memory) set in `docker-compose.yml`; limits validated against target VM specifications; no OOM under normal load during soak test. | Tech Lead | ☐ |
| OP-05 | MinIO bucket configuration correct: private access policy; ACMP app can read and write objects; download URL generation (pre-signed) tested end-to-end (file upload → retrieve pre-signed URL → download → content matches). | Tech Lead | ☐ |
| OP-06 | Nightly backup job confirmed operational in staging: backup runs on schedule, backup file produced, restore of backup tested (see D-03). | Tech Lead | ☐ |
| OP-07 | OpenAPI document accessible and valid in staging environment; all PH-1 endpoints documented (FR-008). | Tech Lead | ☐ |
| OP-08 | Environment configuration checklist confirmed for production target: all required environment variables/secrets defined in the target VM's secrets store or `.env` (not committed); no staging credentials in production config. | Tech Lead | ☐ |

---

## Group 9 — Sign-off

All items below must be signed off before production deployment is authorized.

| # | Sign-off | Responsible | Signature / Date |
|---|---|---|---|
| SO-01 | **[BLOCK]** Secretary (Product Owner) UAT sign-off: end-to-end governance loop completed in staging. All secretary-role flows confirmed working. | Secretary | ☐ |
| SO-02 | **[BLOCK]** Chairman sign-off: voting flow, chairman approval, override recording, and decision immutability confirmed in staging. | Chairman | ☐ |
| SO-03 | **[BLOCK]** Security sign-off: all [BLOCK] security items (S-01 through S-10) checked and confirmed; ASVS L2 review sheet signed. | Security Reviewer | ☐ |
| SO-04 | **[BLOCK]** QA Lead sign-off: all AC-### criteria verified passing; E2E test suite green; permission matrix tests green; no open Sev-1 defects. | QA Lead | ☐ |
| SO-05 | Tech Lead sign-off: architectural integrity confirmed; no module boundary violations; all [BLOCK] data and operability items checked. | Tech Lead | ☐ |
| SO-06 | Deployment window scheduled and communicated to all committee members via in-app notification (maintenance window: Sunday 00:00–04:00 per `docs/43-post-release-operating-model.md §2.2`). | Secretary | ☐ |
| SO-07 | Post-deployment verification plan confirmed: responsible party, monitoring window (48h), escalation contact list, rollback trigger criteria documented. | Tech Lead | ☐ |

---

## Traceability

- Implements **Deliverable 58** (Release-readiness checklist).
- [BLOCK] security items enforce: ADR-0009 (audit immutability, S-09), ADR-0004 (Keycloak OIDC, S-11), FR-013 (secrets externalized, S-07), `docs/25-security-controls.md` (ASVS L2, S-01).
- Localization items enforce: FR-003 (EN/AR, L-01), FR-004 (RTL, L-02), FR-014 (localized errors, L-07).
- Accessibility items enforce: FR-034/FR-047 (DnD alternatives, A-03), WCAG 2.2 AA (A-01, A-05).
- Data items enforce: FR-012 (EF Core migrations, D-01), `README.md §A` (backup/restore target, D-03).
- Observability items enforce: FR-007 (health checks, O-02), FR-009 (Serilog→Seq, O-01), FR-010 (OTel, O-03), FR-011 (Hangfire, O-04).
- Sign-off items tie to: `docs/36-roadmap.md §PH-1 Exit Criteria` (SO-01, SO-02), `docs/44-definition-of-done.md §Level 3 Release DoD`.
- All [BLOCK] items are strict gates; the release secretary (Secretary role) is the authority to proceed to deployment only after all [BLOCK] items are checked and signed.
