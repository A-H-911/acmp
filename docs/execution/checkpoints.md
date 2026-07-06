---
status: Approved
version: 1.1.0
updated: 2026-07-06
owner: lead-secretary
---

# Checkpoints & Release Gates — ACMP

The hard review/approval gates applied at every phase and release boundary. These are the **[BLOCK]** items from the pre-migration release-readiness checklist: a deployment must not proceed while any of them is unchecked or failing. The release secretary (Secretary role) is the sole authority to proceed to deployment, and only after every [BLOCK] gate below is checked and signed.

**How to use:** copy this gate set into the release ticket. Each gate is a hard blocker — check it with the responsible party's initials and date, and attach evidence (scan results, test-run links, backup confirmation, sign-off sheets). Non-[BLOCK] supporting checks (functional spot-checks, performance soak tests, screen-reader smoke tests, etc.) are not gates but are still required by the [Definition of Done](definition-of-done.md), which carries the non-gating checks (the original consolidated checklist, `45-release-readiness-checklist.md`, is in git history). This document carries only the gates.

---

## Gate 1 — Functionality

| # | Gate | Owner | Status |
|---|---|---|---|
| F-01 | All Must-priority (M) FR-IDs in this release scope have a passing AC-### (G-TRACE gate verified). | QA Lead | ☐ |
| F-02 | All PH-1 user stories (US-001 through US-084) are in "Done" status with QA-verified acceptance criteria. | QA Lead | ☐ |
| F-03 | End-to-end governance loop test passed in staging: topic submit → triage → accept → prepare → schedule → agenda publish → meeting create → attendance record → live notes → vote configure/open/close → chairman ratify → decision record (Issued) → action create → action verify → MoM draft/approve/publish. | QA Lead + Secretary | ☐ |
| F-04 | All automated unit and integration tests pass on CI (zero failures; zero flaky tests on last 3 runs). | Tech Lead | ☐ |
| F-05 | All E2E tests (Playwright or equivalent) pass on the staging CI run. | QA Lead | ☐ |

_Supporting (non-gating) checks F-06 – F-19 in the full checklist: permission-matrix tests, SoD-rule tests, carry-over suggestions, aging indicator, overdue derivation, reminder/escalation jobs, decision/vote immutability, MoM versioning, unsaved-work guard, FTS search, traceability panel, and all three dashboards live-data check._

---

## Gate 2 — Security

| # | Gate | Owner | Status |
|---|---|---|---|
| S-01 | OWASP ASVS L2 checklist reviewed against [docs/domain/security-controls.md](../domain/security-controls.md); no unmitigated High or Critical findings. Evidence: signed ASVS L2 review sheet attached. | Security Reviewer | ☐ |
| S-02 | SAST scan (Semgrep or equivalent) clean: zero unmitigated High/Critical code-level findings; Low/Medium triaged with documented rationale. | Security Reviewer | ☐ |
| S-03 | DAST scan (OWASP ZAP or equivalent) run against staging: zero unmitigated High/Critical dynamic findings; scan report attached. | Security Reviewer | ☐ |
| S-04 | Dependency vulnerability scan (`dotnet list package --vulnerable` + `npm audit`): zero unmitigated High/Critical CVEs in production dependencies. | Tech Lead | ☐ |
| S-05 | Secret scan (truffleHog or gitleaks) run against full repository history and Docker image layers: zero confirmed secrets detected. | Tech Lead | ☐ |
| S-06 | Container image vulnerability scan (e.g., Trivy) on all images (ACMP app, SQL Server, Seq, MinIO base): zero unmitigated High/Critical CVEs. | Tech Lead | ☐ |
| S-07 | All secrets externalized: connection strings, Keycloak client secret, MinIO credentials, and any API keys supplied via env vars or secrets manager; none in `docker-compose.yml`, Dockerfiles, or committed config files (FR-013). | Tech Lead | ☐ |
| S-08 | TLS enforced on all external-facing ACMP endpoints; certificate valid (not expired, trusted CA, matching hostname); HTTPS redirect active. | Tech Lead | ☐ |
| S-09 | Audit immutability verified in staging: (a) direct UPDATE/DELETE on audit_log table rejected by DB constraint or application guard; (b) hash-chain integrity check tool returns 0 broken links on full staging audit log. Evidence attached. | Tech Lead + Security Reviewer | ☐ |
| S-10 | Authentication tested: unauthenticated requests to any protected endpoint return HTTP 401; unauthorized role requests return HTTP 403; test for each boundary in permission matrix (AC-005 through AC-011). | QA Lead | ☐ |

_Supporting (non-gating) checks S-11 – S-14: Keycloak OIDC token validation, ABAC scope enforcement, input-validation (SQLi/XSS), and pre-signed file-URL expiry._

---

## Gate 3 — Data

| # | Gate | Owner | Status |
|---|---|---|---|
| D-01 | All EF Core migration scripts for this release applied to staging cleanly (zero errors, no data-loss warnings in migration output log). | Tech Lead | ☐ |
| D-02 | Rollback plan confirmed: rollback migration script (or documented restore-from-backup procedure) applied to staging and prior release confirmed functional; estimated rollback time documented. | Tech Lead | ☐ |
| D-03 | Pre-release backup of production database taken (if production exists); backup verified by partial restore test (database comes up, schema version matches pre-release schema); backup filename and timestamp recorded. | Tech Lead | ☐ |

_Supporting (non-gating) checks D-04 – D-07: seed/reference data verification, DB-level immutability constraints, SQL Server FTS index build (EN/AR), and MinIO bucket configuration._

---

## Gate 4 — Localization

| # | Gate | Owner | Status |
|---|---|---|---|
| L-01 | 100% of i18n keys present in both EN (`en.json`) and AR (`ar.json`) resource files; build-time i18n completeness check passes with zero missing keys. | Frontend Engineer | ☐ |
| L-02 | RTL visual regression test passed on all routes for the AR locale: automated screenshot comparison or QA sign-off by AR-fluent reviewer confirms no LTR artifacts, no cropped Arabic text, no misaligned directional icons; evidence attached. | Frontend Engineer + AR Reviewer | ☐ |
| L-03 | Zero hardcoded English (or Arabic) strings in rendered HTML, verified by i18n linting or automated DOM scan on all routes. | Frontend Engineer | ☐ |

_Supporting (non-gating) checks L-04 – L-07: bilingual glossary consistency, locale-switch data preservation, Gregorian date formatting, and localized error/validation messages in AR._

---

## Gate 5 — Accessibility

| # | Gate | Owner | Status |
|---|---|---|---|
| A-01 | Automated accessibility scan (axe-core or equivalent) run on all page routes in both EN and AR locales: zero Critical violations; High violations documented and either fixed or accepted with explicit rationale; scan report attached. | QA Lead | ☐ |
| A-02 | Keyboard-only navigation test on critical paths completed: login → topic submit → triage → agenda build → vote cast → chairman approve → MoM approve. No keyboard traps; all interactive elements reachable via Tab; focus rings visible throughout (AC-043, AC-044, AC-045). | QA Lead | ☐ |
| A-03 | Drag-and-drop alternatives verified: backlog kanban move-up/move-down controls function via keyboard only; agenda-item reorder keyboard alternative functions (FR-034, FR-047, AC-043, AC-044). | QA Lead | ☐ |

_Supporting (non-gating) checks A-04 – A-07: form-control labels and icon-button aria-labels, color-contrast ratios (light + dark), screen-reader smoke test, and DOM reading order in LTR/RTL._

---

## Gate 6 — Performance

_No [BLOCK] gate in this group. All performance checks (P-01 – P-04: smoke-load test, 30-minute soak, Hangfire queue-at-rest, SQL query-plan review) are required supporting checks per the [Definition of Done](definition-of-done.md) §Performance, but none is a hard release blocker._

---

## Gate 7 — Observability

| # | Gate | Owner | Status |
|---|---|---|---|
| O-01 | Serilog → Seq pipeline verified: a test log entry at Error level generated in staging appears in the self-hosted Seq container within 30 seconds; entry includes correlation ID, masked user ID, module name, and operation (FR-009). | Tech Lead | ☐ |
| O-02 | Health check endpoints verified: GET `/health/live` and GET `/health/ready` both return HTTP 200 with expected body in the staging Docker Compose stack (FR-007). | Tech Lead | ☐ |

_Supporting (non-gating) checks O-03 – O-05: OpenTelemetry trace visibility, Hangfire dashboard access, and Seq error-rate alerting._

---

## Gate 8 — Operability

| # | Gate | Owner | Status |
|---|---|---|---|
| OP-01 | Operational runbook updated and reviewed: covers startup/shutdown sequence, log viewing in Seq, backup/restore procedure, Keycloak claim troubleshooting, known issues/mitigations, and emergency contact list; runbook location documented. | Tech Lead | ☐ |
| OP-02 | Rollback procedure documented and tested (see D-02); rollback confirmed completable within 30 minutes. | Tech Lead | ☐ |
| OP-03 | Docker Compose file (`docker-compose.yml`) validated: all containers (ACMP app, SQL Server, Seq, MinIO) start in the correct order with healthcheck dependencies; no manual intervention required for a clean startup from scratch. | Tech Lead | ☐ |

_Supporting (non-gating) checks OP-04 – OP-08: container resource limits, MinIO bucket config, nightly backup job, OpenAPI document validity, and production environment-configuration checklist._

---

## Gate 9 — Sign-off

All sign-off gates below must be completed before production deployment is authorized.

| # | Sign-off gate | Responsible | Signature / Date |
|---|---|---|---|
| SO-01 | Secretary (Product Owner) UAT sign-off: end-to-end governance loop completed in staging; all secretary-role flows confirmed working. | Secretary | ☐ |
| SO-02 | Chairman sign-off: voting flow, chairman approval, override recording, and decision immutability confirmed in staging. | Chairman | ☐ |
| SO-03 | Security sign-off: all [BLOCK] security gates (S-01 through S-10) checked and confirmed; ASVS L2 review sheet signed. | Security Reviewer | ☐ |
| SO-04 | QA Lead sign-off: all AC-### criteria verified passing; E2E test suite green; permission-matrix tests green; no open Sev-1 defects. | QA Lead | ☐ |

_Supporting (non-gating) sign-offs SO-05 – SO-07: Tech Lead architectural-integrity sign-off, deployment-window scheduling + notification, and the post-deployment verification plan (48h monitoring, rollback-trigger criteria)._

---

## Applying gates at phase vs release boundaries

- **Every production release** (any named phase or patch deployment) must pass all [BLOCK] gates above; this is the Level-3 Release DoD in force. See [execution/definition-of-done.md](definition-of-done.md) §Level 3.
- **Phase exit** (PH-1 → PH-2 → PH-3) is a release boundary: the phase's Must-priority FR-IDs (F-01), all its user stories (F-02), and the full governance-loop E2E (F-03) are the phase-completion gates, evaluated against that phase's scope in [planning/roadmap.md](../planning/roadmap.md).
- **Scope note:** F-02 names the PH-1 story range (US-001 – US-084); at a PH-2/PH-3 boundary, substitute that phase's Must-priority story range. All other gates are phase-agnostic.

---

## Traceability

- Implements **Deliverable 58** (Release-readiness checklist); source: the pre-migration `45-release-readiness-checklist.md` (git history) — [BLOCK] items carried here verbatim, non-gating checks re-homed to the [Definition of Done](definition-of-done.md).
- [BLOCK] security gates enforce: ADR-0009 (audit immutability, S-09), ADR-0004 (Keycloak OIDC, S-10/S-11), FR-013 (secrets externalized, S-07), [docs/domain/security-controls.md](../domain/security-controls.md) (ASVS L2, S-01).
- Localization gates enforce: FR-003 (EN/AR, L-01), FR-004 (RTL, L-02), FR-014 (localized errors, supporting L-07).
- Accessibility gates enforce: FR-034/FR-047 (DnD alternatives, A-03), WCAG 2.2 AA (A-01).
- Data gates enforce: FR-012 (EF Core migrations, D-01), `README.md §A` (backup/restore target, D-03).
- Observability gates enforce: FR-007 (health checks, O-02), FR-009 (Serilog→Seq, O-01).
- Sign-off gates tie to: [planning/roadmap.md](../planning/roadmap.md) §PH-1 Exit Criteria (SO-01, SO-02) and [execution/definition-of-done.md](definition-of-done.md) §Level 3 Release DoD.
