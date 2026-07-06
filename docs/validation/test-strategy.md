---
status: Approved
version: 1.0.0
updated: 2026-07-06
owner: lead-secretary
---

# Test Strategy — ACMP

ACMP follows a right-sized test pyramid for a modular monolith serving at most 20 users. From the base up: pure domain and value-object unit tests (xUnit + FluentAssertions + Bogus); application and handler tests over the real in-process MediatR pipeline with infrastructure boundaries mocked via NSubstitute; API integration tests through `WebApplicationFactory<Program>` against a real SQL Server 2022 via Testcontainers, with a `TestAuthHandler` synthesizing Keycloak JWT claims; frontend component tests in Vitest + React Testing Library with MSW and inline `jest-axe`; and a thin layer of Playwright E2E flows run against a full Docker Compose stack. Counts are deliberately modest per module (roughly 20 to 50 unit, 10 to 30 handler, 5 to 15 integration per major flow) rather than exhaustive.

Domain-specific suites reinforce the governance rules that matter most: a role-by-endpoint permission matrix, workflow and state-machine transitions (valid and invalid), voting integrity (attribution, quorum, immutability, forward-only lifecycle), decision and MoM immutability plus supersession, an audit-trail assertion after every auditable command, file-upload validation, localization key parity, Arabic/RTL layout, and WCAG 2.2 AA accessibility. External adapters (Webex, Tarseem, Keystone) are exercised against mocked HTTP stubs; no real external calls run in CI.

CI gates block on: unit, handler, integration, frontend, and migration tests green; SAST and dependency and secret and container scans clean of High/Critical; zero critical or serious axe-core violations; EN/AR i18n key parity; ArchUnit.NET module-boundary rules; and EF migration idempotency. Coverage is enforced per file to at least 95% (ADR-0016) on top of the docs/validation/test-strategy.md line/branch floors (80% line, 70% branch on domain and application layers). AC-### coverage mapping is a build warning in PH-1 and a hard block from PH-2. E2E, performance (k6), and DAST (ZAP) run on PR-to-main, release, or scheduled cadences rather than every push.

## Test register (TEST-)

Entries map to real test suites named in `docs/validation/acceptance-audit.md` and `docs/validation/test-strategy.md`, or to coherent named areas in the strategy. Status is `Implemented` where the suite exists today and `Planned` where the strategy defines it but it is not yet built.

| ID | Test suite / area | Type | Covers | Status |
|---|---|---|---|---|
| TEST-001 | KeycloakRoleClaimMapperTests | Unit | FR-018 claim-to-role mapping (AC-002, AC-003) | Implemented |
| TEST-002 | MembershipFeatureTests | Integration | FR-002, FR-020 JIT profile and role assignment (AC-002, AC-058) | Implemented |
| TEST-003 | MembershipApiTests | Integration | FR-021, FR-024 directory read all roles, 401 on no token (AC-008, AC-059) | Implemented |
| TEST-004 | PermissionMatrixTests | Integration | FR-018, FR-024 role-by-endpoint RBAC and SoD-5 (AC-005, AC-006, AC-007) | Implemented |
| TEST-005 | AbacHandlerTests | Unit | FR-019, FR-022 stream-scope and ownership ABAC (AC-009, AC-010, AC-011) | Implemented |
| TEST-006 | MembershipResolverTests | Unit | FR-019 stream membership resolution (AC-010) | Implemented |
| TEST-007 | SegregationOfDutiesTests | Unit | FR-074, FR-075, FR-085 SoD-1 and SoD-3 predicates (AC-012, AC-013, AC-015, AC-016) | Implemented |
| TEST-008 | ActionHandlerTests | Unit | FR-085 verifier-not-owner gate and audited denial (AC-012, AC-013) | Implemented |
| TEST-009 | ActionsApiTests | Integration | FR-085 owner-verify 403, independent verify 204 (AC-012, AC-013) | Implemented |
| TEST-010 | ActionActions.test | Unit | FR-085 Verify UI hidden from owner, surfaces API 403 (AC-012, AC-013) | Implemented |
| TEST-011 | VoteTests | Unit | FR-070 to FR-077 vote domain rules, forward-only lifecycle (AC-021 to AC-026) | Implemented |
| TEST-012 | VoteHandlerTests | Unit | FR-070 to FR-075 config lock, quorum, attribution, SoD-3 (AC-015, AC-016, AC-021 to AC-024) | Implemented |
| TEST-013 | VotesApiTests | Integration | FR-071, FR-072, FR-075 duplicate/close/quorum HTTP behavior (AC-022 to AC-025) | Implemented |
| TEST-014 | DecisionTests | Unit | FR-065, FR-066 decision immutability and supersede back-link (AC-027, AC-028) | Implemented |
| TEST-015 | DecisionHandlerTests | Unit | FR-065, FR-066, FR-074 issue, supersede, override with justification (AC-016, AC-027, AC-028) | Implemented |
| TEST-016 | DecisionsApiTests | Integration | FR-065, FR-067 supersede 201, downstream-link issue gate 409 (AC-028, AC-029) | Implemented |
| TEST-017 | DecisionPage.test | Unit | FR-066 read-only detail, superseded state, supersede dialog (AC-027, AC-028) | Implemented |
| TEST-018 | TopicTests | Unit | FR-040, FR-042, FR-044 rejection immutability, prepare, content lock (AC-032 to AC-035) | Implemented |
| TEST-019 | TopicHandlerTests | Unit | FR-029, FR-040, FR-042 reject/accept/prepare guards and authz-deny (AC-031 to AC-035) | Implemented |
| TEST-020 | TopicApplicationTests | Unit | FR-025, FR-029 submit/reject/prepare validators (AC-030, AC-031, AC-035) | Implemented |
| TEST-021 | TopicApiTests | Integration | FR-022, FR-025, FR-029 topic edit scope and validation HTTP (AC-009, AC-030, AC-031) | Implemented |
| TEST-022 | SubmitTopic.test | Unit | FR-003, FR-015, FR-025 client validation, dirty-nav guard, locale preserve (AC-030, AC-039, AC-047) | Implemented |
| TEST-023 | TopicAttachmentTests | Unit | FR-006, FR-027 size/MIME validation and IFileStore handler (AC-049, AC-050) | Implemented |
| TEST-024 | MinutesOfMeetingTests | Unit | FR-054, FR-055 MoM supersede and state transitions (AC-036 to AC-038) | Implemented |
| TEST-025 | MinutesHandlerTests | Unit | FR-054, FR-055 sole-author flag, change-request, publish fan-out (AC-014, AC-036 to AC-038) | Implemented |
| TEST-026 | MinutesApiTests | Integration | FR-054, FR-055 supersede/request-changes/publish HTTP (AC-036 to AC-038) | Implemented |
| TEST-027 | MeetingMinutes.test | Unit | FR-054, FR-055 minutes UI approve, supersede, request-changes (AC-014, AC-036 to AC-038) | Implemented |
| TEST-028 | AgendaBuilder.test + MeetingHandlerTests | Unit | FR-047, FR-130 keyboard reorder, agenda publish fan-out (AC-044, AC-051) | Implemented |
| TEST-029 | NotificationHandlerTests + NotificationCenter.test | Unit | FR-129, FR-130, FR-131 in-app-only delivery, deep-link nav (AC-051 to AC-053) | Implemented |
| TEST-030 | ActionReminderSweepTests + CommitteeDirectoryTests | Unit | FR-083, FR-084 due-soon reminder and overdue escalation (AC-054, AC-055) | Implemented |
| TEST-031 | JobsMonitorMapperTests + AdminJobsEndpointTests | Integration | FR-011 Hangfire job-monitor projection and admin endpoint (AC-056) | Implemented |
| TEST-032 | Backlog.test | Unit | FR-038 aging badge rendering (AC-057) | Implemented |
| TEST-033 | Kanban.test + topicMeta.test | Unit | FR-034 backlog keyboard move alternative (AC-043) | Implemented |
| TEST-034 | i18n direction.test.ts + theme.test.ts | Unit | FR-004, FR-005 dir=rtl mirroring and theme persistence (AC-040, AC-042) | Implemented |
| TEST-035 | Accessibility axe render (jest-axe WCAG 2.2 AA) | Unit | FR-004, FR-034 focus, labels, contrast, reading order (AC-045, AC-046) | Implemented |
| TEST-036 | TraceabilityTests + TraceabilityApiTests | Integration | FR-146, FR-147 typed edge create and relationship panel API (AC-062, AC-063) | Implemented |
| TEST-037 | dashboardAgg.test + RoleDashboard.test | Unit | FR-135, FR-136, FR-137 role dashboard aggregation and variants (AC-064 to AC-066) | Implemented |
| TEST-038 | ArchUnit.NET module-boundary tests | Architecture | Cross-module isolation (no internal references between bounded contexts) | Implemented |
| TEST-039 | Acmp.Migrations.Tests EF migration idempotency | Integration | Schema provisioning, migrate-up twice, constraints and indexes | Implemented |
| TEST-040 | i18n key-parity script (EN vs AR) | Unit | FR-003, FR-004 EN/AR key set parity gate | Implemented |
| TEST-041 | core-loop.spec E2E (Playwright) | E2E | FR-042, FR-130 intake to agenda to publish, cross-context fan-out (AC-035, AC-051) | Implemented |
| TEST-042 | rtl-a11y.spec + dnd-and-failures.spec E2E | E2E | FR-004, FR-047 live axe AA EN/AR, native agenda reorder (AC-044) | Implemented |
| TEST-043 | p12-dashboard-vr.spec E2E visual regression | E2E | FR-135 to FR-137 role dashboard pixel fidelity EN-light and AR-dark (AC-064 to AC-066) | Implemented |
| TEST-044 | Audit-trail hash-chain suite (AuditEvent immutability and integrity) | Integration | FR-150 to FR-153 audit entry, immutability, hash-chain, auditor search (AC-017 to AC-020) | Planned |
| TEST-045 | Global search integration suite (grouped results, Arabic word-breaker) | Integration | FR-143 to FR-145 grouped search and Arabic full-text (AC-060, AC-061) | Planned |
| TEST-046 | Traceability panel component tests (FE display) | Unit | FR-146, FR-147 upstream/downstream panel display (AC-062, AC-063) | Planned |
| TEST-047 | k6 performance scenarios (dashboard, search, vote, upload) | E2E | NFR-001, NFR-002, NFR-005 P95 latency targets under 15 VU | Planned |
| TEST-048 | OWASP ZAP baseline DAST | E2E | Security baseline scan (advisory in PH-1, blocking from PH-2) | Planned |
| TEST-049 | External adapter contract tests (Webex, Tarseem, Keystone via WireMock) | Integration | Adapter request/response contracts, retry and circuit-breaker | Planned |
| TEST-050 | Backup and restore runbook verification | Integration | SQL and MinIO restore, row-count parity, smoke on restored instance | Planned |
