---
status: Approved
version: 1.0.0
updated: 2026-07-06
owner: lead-secretary
---

# Definition of Done — ACMP

The completion criteria that must be satisfied before any Story, Feature/Epic, or Release is considered done. Enforced by the QA Lead and Tech Lead; items not meeting DoD are returned to the developer.

**Conventions.**

- Items marked **[HARD]** are non-negotiable; a single unchecked [HARD] item blocks promotion.
- Items marked **[SOFT]** may have a documented exception approved by the Tech Lead + Secretary; the exception is logged in the sprint retrospective.
- DoD applies to all PH-1 work and carries forward unchanged to PH-2/PH-3 unless the team explicitly revises it via a committee topic (see [43-post-release-operating-model.md](../domain/post-release-operating-model.md) §8.3).

---

## Level 1 — Story DoD

Every user story (`US-###`) must satisfy all of the following before the story is moved to "Done" in the sprint board.

### Code Quality

- [ ] **[HARD]** Code compiles with zero errors and zero warnings on the CI pipeline.
- [ ] **[HARD]** All unit tests for the handler(s)/service(s) modified by this story pass; coverage for the new code path is ≥80% branch coverage.
- [ ] **[HARD]** All integration tests for the affected module pass (in-process `WebApplicationFactory` tests; no flaky tests accepted).
- [ ] **[SOFT]** Code has been reviewed and approved by at least one other engineer (PR review); reviewer checklist completed.
- [ ] **[SOFT]** No leftover unfinished-work or fix-later comments introduced without a linked backlog item.
- [ ] **[SOFT]** No code duplication introduced across module boundaries (modular monolith rule: modules communicate via public contracts, not shared tables — `README.md §B`).

### Authorization & Security

- [ ] **[HARD]** Authorization policy is enforced at the API handler layer (ASP.NET Core policy-based authorization); the story's capability appears in the permission matrix ([10-permission-role-matrix.md](../domain/permission-role-matrix.md)) and the policy exists and is named consistently.
- [ ] **[HARD]** Deny-by-default verified: unauthenticated requests return HTTP 401; unauthorized role/ABAC attempts return HTTP 403.
- [ ] **[HARD]** SoD rules (SoD-1 through SoD-5) relevant to this story are enforced in code and verified by a test (e.g., AC-012 for Action.Verify, AC-015 for Vote.Close).
- [ ] **[HARD]** No secrets (connection strings, API keys, tokens) are hardcoded in source code or container images; all secrets are externalized via environment variables/secrets manager (FR-013).
- [ ] **[SOFT]** No new High or Critical OWASP ASVS L2 vulnerabilities introduced (verified by SAST scan result; clean or known-and-tracked exceptions only).

### Audit Events

- [ ] **[HARD]** Every state-mutating operation in this story emits the correct `AuditEvent` (entity type, entity ID, action type, actor, UTC timestamp, before/after state JSON, correlation ID) per FR-150, FR-151.
- [ ] **[HARD]** Immutability guard tested for any story touching votes, issued decisions, ADRs, or published MoMs: a test asserts that a subsequent mutation attempt is rejected (AC-025, AC-027, AC-033).
- [ ] **[HARD]** Audit log rows are verified as insert-only (no UPDATE/DELETE path exists for audit entries in the handler or repository layer).

### Localization (EN/AR)

- [ ] **[HARD]** All user-visible strings (labels, button text, validation messages, error messages, notification text, aria-labels, placeholder text) are externalized to the i18n resource files (EN + AR); no hardcoded strings in component code.
- [ ] **[HARD]** The Arabic translation for every new string is provided (by the coordinating bilingual reviewer) and merged with the PR; EN keys without AR translation are a build-time warning.
- [ ] **[HARD]** RTL layout verified: the new UI component renders correctly in AR/RTL mode (no LTR artifacts, no cropped text, no misaligned icons); visual check performed in Chrome with `dir=rtl` applied.
- [ ] **[SOFT]** EN and AR term consistency verified against the signed-off bilingual glossary (from PH-0 spike).

### Accessibility

- [ ] **[HARD]** Every interactive element added or modified has a visible focus indicator in both LTR and RTL layouts (WCAG 2.2 AA success criterion 2.4.11).
- [ ] **[HARD]** Every form control has a programmatically associated label (`<label for="...">` or `aria-labelledby`); every icon-only button has an `aria-label`.
- [ ] **[HARD]** If the story introduces drag-and-drop, a keyboard-accessible alternative (move-up/move-down controls or equivalent) is implemented and tested (FR-034, FR-047, AC-043, AC-044).
- [ ] **[SOFT]** Color contrast ratio ≥ 4.5:1 for normal text and ≥ 3:1 for large text verified on new UI elements (WCAG 2.2 AA).
- [ ] **[SOFT]** Tab order matches visual reading order in both EN and AR layouts.

### API Documentation

- [ ] **[SOFT]** New or modified endpoints are documented in the OpenAPI spec (FR-008); request/response schemas, error codes (400/401/403/404/422/500), and example payloads are present.
- [ ] **[SOFT]** OpenAPI spec generates without validation errors (checked in CI).

### Acceptance Criteria

- [ ] **[HARD]** All AC-### criteria linked to this US-### (per [40-acceptance-criteria.md](../validation/acceptance-criteria.md)) are verified passing — either by automated test or documented manual test result.
- [ ] **[HARD]** The QA engineer or Tech Lead has confirmed acceptance; no AC is "assumed passing" without evidence.

---

## Level 2 — Feature/Epic DoD

Every `EPIC-##` must satisfy all Story-level DoD items for its constituent stories plus the following before the epic is closed.

### End-to-End Testing

- [ ] **[HARD]** At least one E2E test (using the agreed E2E framework, e.g., Playwright) covers the primary happy-path flow of the epic from browser login through final state (e.g., for EPIC-08 Voting: configure vote → cast ballots → close → chairman ratify → verify immutability).
- [ ] **[HARD]** All E2E tests for the epic pass on the CI pipeline without flakiness (re-run 3 times; all 3 pass).

### Permission Matrix Tests

- [ ] **[HARD]** A test matrix exists (parametrized integration tests) covering every action in the epic's scope against every relevant role (Allow / AiO / Deny cells from [10-permission-role-matrix.md](../domain/permission-role-matrix.md) §C); all cells verified passing.
- [ ] **[HARD]** ABAC scope tests exist for stream-scoped actions (a Member in Stream-A cannot act on Stream-B-only topics).

### Workflow Coverage

- [ ] **[HARD]** The canonical workflow(s) from [13-workflows.md](../domain/workflows.md) that this epic implements are traceable to tests (each workflow step corresponds to at least one unit/integration/E2E test).
- [ ] **[SOFT]** Exception and alternate paths from each workflow definition are tested (e.g., quorum-not-met path in W11, rejection-without-reason blocked in W2).

### Dashboard Data Validation

- [ ] **[SOFT]** If the epic contributes data to a dashboard (FR-135, FR-136, FR-137), a test verifies that the dashboard count/list updates correctly when the epic's entities are created/modified/completed.

### ADR / Architecture Decision Update

- [ ] **[SOFT]** If any implementation decision in this epic contradicts or refines a settled ADR (ADR-0001–ADR-0012), an ADR update or new ADR has been authored, approved, and linked to the epic's work items.
- [ ] **[SOFT]** If the epic introduced a new architectural pattern not covered by existing ADRs, a new `DEC-` or `ADR-` is raised and its status is at least Proposed.

---

## Level 3 — Release DoD

A release (production deployment of a named phase or patch) must satisfy all Epic/Feature-level DoD items plus the following.

### Phase Acceptance & Exit Criteria

- [ ] **[HARD]** All Must-priority (`M`) FR-IDs in scope for this release have at least one passing AC-### (Keystone gate G-TRACE verified).
- [ ] **[HARD]** All Must-priority user stories (US-###) are in "Done" status with verified ACs.
- [ ] **[HARD]** Zero Sev-1 open defects at release time.
- [ ] **[SOFT]** Sev-2 open defects have a documented mitigation plan and owner; no unmitigated Sev-2 defects.
- [ ] **[HARD]** Secretary (Product Owner) has signed off UAT in the staging environment.
- [ ] **[HARD]** Chairman has signed off on the governance-critical flows (voting, decision, MoM approval) in UAT.

### Security

- [ ] **[HARD]** OWASP ASVS L2 checklist reviewed; no unmitigated High or Critical items ([25-security-controls.md](../domain/security-controls.md)).
- [ ] **[HARD]** SAST scan (e.g., Semgrep, .NET Roslyn analyzers) clean or all findings triaged with accepted exceptions documented.
- [ ] **[HARD]** DAST scan (e.g., OWASP ZAP against staging) run; High/Critical findings resolved or accepted with documented risk.
- [ ] **[HARD]** Dependency vulnerability scan (e.g., `dotnet list package --vulnerable`, npm audit) clean or findings triaged; no High/Critical unmitigated CVEs in production dependencies.
- [ ] **[HARD]** Secret scan (e.g., truffleHog, gitleaks) clean on repository history; no secrets in source code or Docker images.
- [ ] **[HARD]** Container image vulnerability scan clean for High/Critical CVEs in base images.
- [ ] **[HARD]** All secrets externalized; no secrets in `docker-compose.yml`, Dockerfiles, or committed config files (FR-013).
- [ ] **[HARD]** TLS enforced on all external-facing endpoints; certificates valid.
- [ ] **[HARD]** Audit immutability verified: a test asserts that attempting to UPDATE or DELETE any audit log row (even as a DBA-level test user) fails; hash-chain integrity check passes on the full audit log in staging.

### Data

- [ ] **[HARD]** All EF Core migration scripts applied to staging cleanly (zero errors, no data loss); migrations are idempotent.
- [ ] **[HARD]** Rollback plan tested: rollback migration script (or backup-restore) applied to staging and confirmed that the prior release is functional.
- [ ] **[HARD]** Pre-release backup taken of production database (if production exists) and restore tested to verify the backup is valid.
- [ ] **[SOFT]** Seed data (canonical roles, default templates, bilingual glossary terms) verified correct in staging.
- [ ] **[SOFT]** Any reference/lookup data required for the release (e.g., new topic types, new notification event codes) is present in the deployed seed migration.

### Localization

- [ ] **[HARD]** 100% of i18n keys present in both EN and AR resource files (build-time check passes; zero missing keys).
- [ ] **[HARD]** RTL visual regression test passed on all page routes for the AR locale (automated screenshot diff or manual sign-off by AR-fluent reviewer).
- [ ] **[HARD]** Zero hardcoded English strings in rendered HTML (automated check via i18n linting or manual spot-check).
- [ ] **[SOFT]** Bilingual glossary terms used consistently across the release (Secretary confirms with AR-fluent committee member).

### Accessibility

- [ ] **[HARD]** Automated accessibility scan (e.g., axe-core) run on all pages; zero Critical violations; High violations documented and addressed or accepted with rationale.
- [ ] **[HARD]** Keyboard-only navigation test on critical paths (login → topic submit → vote cast → decision view) passes; no keyboard traps; all interactive elements reachable via Tab.
- [ ] **[HARD]** Drag-and-drop alternatives verified on kanban view (EPIC-04) and agenda builder (EPIC-05) via keyboard-only test.
- [ ] **[SOFT]** Screen reader smoke test (NVDA or VoiceOver) on the committee dashboard and topic detail page.

### Performance

- [ ] **[SOFT]** Smoke-load test run against staging with a representative dataset (50 topics, 10 meetings, 200 audit log entries): all pages load within 3 seconds at single-user load; no memory leak detected over 30 minutes.
- [ ] **[SOFT]** Hangfire job queue checked: no jobs stalled or queued for >5 minutes under normal load.
- [ ] **[SOFT]** SQL Server query plans reviewed for any new queries added in this release; no table scans on large tables without an appropriate index.

### Observability

- [ ] **[HARD]** Serilog → Seq pipeline verified: a test error event is generated in staging and appears in the self-hosted Seq instance with correct correlation ID, user ID (masked), module, and operation fields (FR-009).
- [ ] **[HARD]** Health check endpoints (`/health/live`, `/health/ready`) return HTTP 200 in staging Docker Compose stack (FR-007).
- [ ] **[SOFT]** OpenTelemetry traces are visible in Seq (or configured trace backend) for at least one inbound HTTP request and one Hangfire background job (FR-010).
- [ ] **[SOFT]** Hangfire dashboard accessible to Administrator in staging; no failed jobs without acknowledged resolution.

### Operability

- [ ] **[HARD]** Operational runbook updated to cover: startup/shutdown, log viewing in Seq, backup/restore procedure, Keycloak claim mapping confirmation, known issues and mitigations.
- [ ] **[HARD]** Rollback procedure documented and tested (see Data section); rollback time ≤ 30 minutes confirmed.
- [ ] **[HARD]** Docker Compose file (`docker-compose.yml`) validated: all containers (ACMP app, SQL Server, Seq, MinIO) start up in order with correct healthcheck dependencies; no manual intervention required.
- [ ] **[SOFT]** MinIO buckets and lifecycle policies confirmed correct in staging.
- [ ] **[SOFT]** Container resource limits (CPU, memory) set appropriately for the target VM size; verified they do not cause OOM under normal load.

### Sign-off

- [ ] **[HARD]** Secretary (PO) UAT sign-off: end-to-end governance loop completed in staging (topic → triage → agenda → meeting → vote → decision → action); Secretary confirms the workflow matches the operating model ([43-post-release-operating-model.md](../domain/post-release-operating-model.md) §4).
- [ ] **[HARD]** Chairman UAT sign-off: voting, chairman approval, and decision record verified in staging.
- [ ] **[HARD]** Security sign-off from designated security reviewer: all [HARD] security items above checked and confirmed.
- [ ] **[SOFT]** Tech Lead sign-off: architectural integrity confirmed; no module boundary violations introduced.
- [ ] **[SOFT]** QA lead sign-off: all AC-### criteria verified; E2E test suite green.

---

## Traceability

- Implements **Deliverable 57** (Definition of Done); source: [44-definition-of-done.md](definition-of-done.md).
- Story DoD enforces: FR-009 (Serilog/Seq), FR-013 (externalized config), FR-150/FR-151 (audit events), FR-003/FR-004 (EN/AR + RTL), FR-034/FR-047 (DnD accessibility), FR-024 (role-based UI).
- Feature/Epic DoD enforces: [10-permission-role-matrix.md](../domain/permission-role-matrix.md) (permission matrix tests), [13-workflows.md](../domain/workflows.md) (workflow coverage), ADR-0009 (immutability).
- Release DoD enforces: [planning/roadmap.md](../planning/roadmap.md) §PH-1 Exit Criteria, [25-security-controls.md](../domain/security-controls.md) (ASVS L2), [40-acceptance-criteria.md](../validation/acceptance-criteria.md) (AC-### coverage per G-TRACE).
- SoD rules (SoD-1 through SoD-5) per [10-permission-role-matrix.md](../domain/permission-role-matrix.md) §E.4 are explicitly required in the Story-level Authorization checklist.
- Release-boundary [BLOCK] gates that apply this DoD → [execution/checkpoints.md](checkpoints.md).
- Revision authority: changes to this DoD are governed per [43-post-release-operating-model.md](../domain/post-release-operating-model.md) §8.3 (submit as a committee topic).
