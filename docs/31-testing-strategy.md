# 31 — Testing Strategy (Deliverable 39)

**Purpose:** Define the complete test architecture for ACMP — layers, tools, data strategy, mocking boundaries, CI gates, quality gates (including Keystone gates G-IDS/G-DEC-STATUS/G-REQ-SRC/G-COMPLETE/G-TRACE/G-SET/G-PROGRESS), and per-phase focus — so the execution agent builds a correct, auditable system from day one.

> Settled stack: .NET 8 / ASP.NET Core / MediatR / EF Core / SQL Server; React 18 + TS + Vite; Docker Compose; Keycloak OIDC; Hangfire; Seq; MinIO; ≤20 users. Self-contained (CON-001). Constraints from `../README.md` §A and `15-architecture.md`.

---

## 1. Test Pyramid

```
        ╔══════════════════════╗
        ║   E2E / Playwright   ║  ← few; happy-path + audit-trail + RTL
        ╠══════════════════════╣
        ║  Integration (API)   ║  ← WebApplicationFactory + real DB (Testcontainers)
        ╠══════════════════════╣
        ║  Frontend Component  ║  ← Vitest + React Testing Library
        ╠══════════════════════╣
        ║  App/Handler tests   ║  ← MediatR handlers, validators, in-mem or SQLite
        ╠══════════════════════╣
        ║  Unit / Domain       ║  ← pure domain model, value objects, rules (xUnit)
        ╚══════════════════════╝
```

**Counts guidance (right-sized for ≤20 users / modular monolith):**

| Layer | Target count (per module) | Run time target |
|---|---|---|
| Unit / Domain | 20–50 per module | < 5 s total |
| App / Handler | 10–30 per module | < 15 s total |
| API Integration | 5–15 per major flow | < 3 min total |
| Frontend Component | 10–25 per page/feature | < 60 s total |
| E2E | 15–30 total (critical flows only) | < 10 min total |

---

## 2. Test Layers, Tools, and Scope

### 2.1 Unit / Domain tests

**Tool:** xUnit 2.x + FluentAssertions + Bogus (fake data)
**Scope:** Domain aggregates, entities, value objects, domain rules, status-transition guards, ID generation, `LocalizedString`, business invariants.
**Location:** `tests/Acmp.Domain.Tests/` (per-module mirroring)
**Key rules:**
- Zero infrastructure (no DB, no HTTP, no file I/O).
- Test every domain invariant: vote cannot be cast on a closed ballot; a decision is immutable after issue; action cannot complete without resolution; topic status transitions follow the canonical FSM (README §E).
- Use named factory methods (arrange helpers) for aggregates; avoid `new` clutter in every test.

### 2.2 Application / Handler tests

**Tool:** xUnit + FluentAssertions + NSubstitute (mocking interfaces) + in-process MediatR
**Scope:** Command/query handlers, FluentValidation validators, domain-event handlers, Hangfire job logic.
**Location:** `tests/Acmp.<Module>.Application.Tests/`
**Key rules:**
- Mock all infrastructure boundaries: `IRepository`, `IFileStore`, `INotificationChannel`, `IClock`, `ICurrentUser`, external adapter interfaces (IWebexAdapter, ITarseemAdapter).
- Use real MediatR pipeline (behaviors: validation, logging, auth policy); mock the handler's dependencies.
- Validate that domain events are raised when expected (e.g., VoteClosed event fires when quorum reached).
- Validate that outbox is written when a notification is dispatched.

### 2.3 API Integration tests

**Tool:** `Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>` + xUnit + FluentAssertions + Testcontainers (SQL Server 2022) + custom `TestAuthHandler` (fake Keycloak JWT)
**Scope:** Full HTTP round-trip (endpoint → handler → EF Core → real SQL) for each module's critical paths.
**Location:** `tests/Acmp.Api.IntegrationTests/`
**Key rules:**
- Use Testcontainers SQL Server; each test class gets its own schema via EF migrations applied on startup.
- Replace Keycloak validation with `TestAuthHandler` that synthesizes JWT with arbitrary claims; test all role/claim combinations.
- Assert HTTP status codes, response shapes, and DB state.
- Each test is isolated: use a fresh DB per test collection, or use `IDbContextTransaction` rollback.

### 2.4 DB Integration / Migration tests

**Tool:** xUnit + EF Core Migrations + Testcontainers SQL Server (or LocalDB [unverified availability in CI])
**Scope:** EF migration idempotency; migrate up from empty; migrate up from a prior version; rollback safety; seed data integrity.
**Location:** `tests/Acmp.Migrations.Tests/`
**Key rules:**
- Run `dotnet ef database update` from state=0 in a clean container; assert all tables/indexes exist.
- Run `migrate up` twice (idempotency guard).
- Verify all unique constraints, FKs, and columnstore indexes created correctly.

### 2.5 Frontend Component tests

**Tool:** Vitest + React Testing Library (RTL) + `@testing-library/user-event` + MSW (Mock Service Worker for API mocking) + `jest-axe` (a11y checks inline)
**Scope:** React components, hooks, i18n rendering, RTL layout, form validation, DnD drag-and-drop keyboard paths, permission-gated UI rendering.
**Location:** `src/Acmp.Web/src/__tests__/` (co-located or mirror structure)
**Key rules:**
- Every component test asserts EN and AR label rendering (i18next mock).
- RTL/`dir="rtl"` tests: verify logical CSS flips, text alignment, icon placement (see §2.10).
- Stubs for API via MSW; no real fetch calls.
- `jest-axe` inline accessibility scan on every component (see §2.11).

### 2.6 E2E tests

**Tool:** Playwright (`@playwright/test`); browsers: Chromium (primary) + Firefox (smoke)
**Scope:** Critical happy-path flows that cross frontend + API + DB:
1. Topic intake → backlog prioritization → agenda add → meeting open → voting → decision → ADR.
2. Action create → progress update → completion → sign-off.
3. Audit-trail read by Auditor (read-only gate).
4. Notification creation and display.
5. File upload/download (MinIO integration).
6. Arabic UI: switch locale → RTL layout correct.
7. Accessibility audit (Playwright + `@axe-core/playwright`).

**Location:** `tests/e2e/`
**Key rules:**
- Run against a full `docker compose up` stack (staging env) — not a mocked API.
- Use `page.getByRole` and accessible-name queries; avoid brittle CSS selectors.
- Seed DB before each suite; clean after.
- E2E jobs are gated: only run on PR→main and release branches, not on every feature push.

---

## 3. Domain-Specific Test Categories

### 3.1 Authorization / Permission-matrix tests

**Approach:** For each API endpoint, parameterized xUnit test per canonical role (Chairman, Secretary, Member, Reviewer, Auditor, Administrator, Submitter, Guest). Assert 200/201/403/401 by role × operation.
**Coverage:** Every action in `docs/10-permission-role-matrix.md` has a corresponding test row.
**Tool:** API Integration layer with `TestAuthHandler` claim injection.
**Gate:** All permission-matrix tests must pass before any release — missing coverage = release blocker.

### 3.2 Workflow / State-machine tests

**Scope:** Every status transition in README §E for every entity (Topic, Meeting, Vote, Decision, Action, ADR, Risk, Invariant). Both valid transitions (assert success + event) and invalid transitions (assert domain exception or 422).
**Approach:** Unit tests for domain FSM; handler tests for command → state change; API integration test for full round-trip.
**Data coverage:** All 11 decision outcomes (Approved, ConditionallyApproved, Rejected, MoreInfoRequired, FeedbackProvided, EnhancementsRequired, DesignChangesRequired, ResearchRequired, Deferred, Escalated, Converted) each exercised by at least one integration test.

### 3.3 Voting-integrity tests

Critical given ADR-0010 (always-attributed voting, immutability, chairman authority).

| Test | Assertion |
|---|---|
| Vote cast while ballot Open → recorded with voter identity | Voter name + timestamp in DB |
| Vote cast after ballot Closed → rejected | 422 + domain exception |
| Duplicate vote by same voter → rejected | 422 |
| Quorum reached → ballot auto-closes, VoteClosed event fires | DB state + event published |
| Chairman override recorded with chairman's identity | Immutable record with `ChairmanId` |
| Closed ballot: any mutation attempt → 422 | Full test matrix per field |
| Abstention persisted correctly | `VoteOption.Abstain` in DB |
| Result tally: correct count per option | Aggregate calculation verified |
| Ballot Ratified: no further changes accepted | All write operations → 422 |

### 3.4 Decision-history / Immutability tests

| Test | Assertion |
|---|---|
| Issued `Decision` row: PUT/PATCH → 422 | HTTP + DB state unchanged |
| Decision superseded → old record preserved, `SupersededBy` set | Both records in DB |
| AuditEvent created on issue | `audit` schema has event |
| `Vote` rows: no UPDATE/DELETE path exists | No API route + DB trigger not needed (EF no-track) |
| ADR lifecycle: Approved→Superseded creates new ADR; original status unchanged | Both rows exist |
| Minutes: once published, content hash stored; any re-publish invalidates if content differs | Hash check in test |

### 3.5 Audit-trail tests

**Scope:** Every create/update/delete command on every aggregate must produce an `AuditEvent` row with `(SubjectType, SubjectId, ActionType, ActorId, Timestamp, Before, After)`.
**Approach:** API Integration tests assert audit table rows after each command. Cover all domain actions listed in `docs/26-audit-and-records-management.md`.
**Guard:** Audit-trail tests run as a dedicated CI stage before release; zero gaps = release gate.

### 3.6 File-upload tests

| Test | Assertion |
|---|---|
| Valid file (PDF/DOCX/PNG ≤ size limit) → stored in MinIO | `IFileStore` returns key; object exists in MinIO bucket |
| Oversized file → 413 | Error shape matches API contract |
| Disallowed MIME type → 415 | MIME check before MinIO write |
| Pre-signed URL: returned URL is time-limited | URL expires ≤ configured TTL |
| MinIO unreachable → graceful error (not 500 with stack trace) | Circuit-breaker fires; user-facing message |
| Duplicate attachment → de-duplicated by hash | Only one object stored |

**Note:** Integration tests use a real MinIO container via Testcontainers or a docker compose test profile.

### 3.7 Localization tests

**Scope:** Every user-visible string has translations for both `en` and `ar`. Missing key = test failure.
**Approach:**
- Script: enumerate all i18n keys in `en.json`; verify same key set exists in `ar.json` — fails CI if sets differ.
- RTL: Vitest + React Testing Library assert `dir="rtl"` on root element when locale is `ar`.
- Date format: locale `ar` uses Gregorian date formatting with Arabic locale (not Hijri; README §A).
- Backend: `LocalizedString` entity has both `En` and `Ar` fields populated; validator rejects null Ar on create.

### 3.8 Arabic / RTL tests

**Scope:** Full RTL layout correctness for Arabic locale.

| Test | Tool | Assertion |
|---|---|---|
| Root `<html dir="rtl">` when locale=ar | Vitest / RTL | DOM attribute |
| Sidebar renders on right in RTL | Playwright screenshot diff | Visual regression |
| Logical CSS (`margin-inline-start`) mirrors correctly | Playwright + axe | Layout direction |
| Arabic text in diagram JSON spec → Tarseem renders Arabic RTL | Tarseem mock + spec check | Spec field populated |
| DnD handles keyboard navigation in RTL (Arrow keys mirrored) | Vitest / user-event | Event handler |
| Form error messages in Arabic | Vitest | `getByRole('alert')` shows AR text |
| Table column order reversal in RTL | RTL snapshot | Column sequence |

### 3.9 Accessibility tests

**Target:** WCAG 2.2 AA (NFR-035 area, `docs/08-non-functional-requirements.md`).

| Test | Tool | Trigger |
|---|---|---|
| Automated axe-core scan on every component | `jest-axe` in Vitest | Per component test |
| Automated axe-core scan on every rendered page | Playwright + `@axe-core/playwright` | E2E suite |
| Keyboard-only navigation: tab order, focus visible, no keyboard trap | Playwright manual flow | E2E |
| DnD: keyboard-only drag (`@dnd-kit` keyboard sensors) | Playwright user-event | E2E |
| Screen reader roles: interactive elements have accessible names | Vitest `getByRole` | Component |
| Color contrast: no violation at AA | axe-core | Component + E2E |
| Language attribute set correctly per locale | Vitest | Component |

**Gate:** Zero axe-core violations in `critical` / `serious` category is a merge gate.

### 3.10 Security tests

**Tool:** OWASP ZAP (baseline scan) [unverified: ZAP version compatibility with .NET 8 minimal APIs]; Snyk / `dotnet list package --vulnerable`; `npm audit`; Gitleaks; Trivy.
**Scope and schedule:**

| Test | Tool | Frequency | Gate |
|---|---|---|---|
| SAST | CodeQL (or SonarQube) | Every PR | Block on High/Critical |
| Dependency vuln scan (.NET) | `dotnet list package --vulnerable` | Every PR | Block on High/Critical |
| Dependency vuln scan (npm) | `npm audit --audit-level=high` | Every PR | Block on High/Critical |
| Secret scan | Gitleaks | Every commit (pre-commit hook + CI) | Block on any finding |
| Container image scan | Trivy / Grype | On image build | Block on High/Critical |
| ZAP baseline DAST | OWASP ZAP | Weekly + pre-release | Advisory (report only in MVP; block post-Phase 1) |
| Auth: all endpoints reject unauthenticated | API Integration | Every PR | Block |
| OIDC token validation | API Integration + unit | Every PR | Block |

### 3.11 Performance tests

**Tool:** k6 (lightweight; right-sized for ≤20 users)
**Rationale:** No heavy performance engineering needed (C-SCALE: ≤20 users, ~15 concurrent). A small k6 script validates that NFR targets are met.

| Scenario | VUs | Duration | Pass criteria |
|---|---|---|---|
| Homepage / dashboard load | 15 | 60 s | P95 ≤ 2 s (NFR-001) |
| Topic list (100 topics) | 10 | 60 s | P95 ≤ 2 s |
| Vote cast (concurrent) | 5 | 30 s | P95 ≤ 1 s; no 5xx |
| File upload (10 MB PDF) | 3 | 60 s | P95 ≤ 10 s (NFR-005) |
| Search (full-text) | 5 | 60 s | P95 ≤ 3 s (NFR-002) |
| Notification delivery (in-app poll) | 10 | 60 s | P95 ≤ 5 s |

**Schedule:** Run in staging after integration suite; not on every PR (too slow for CI). Run pre-release.

### 3.12 Container tests

- `docker compose up` smoke test: all services reach `healthy` state within 120 s.
- Health endpoint check: `GET /healthz` → 200 after compose up.
- Readiness endpoint check: `GET /readyz` → 200 (SQL + MinIO + Seq reachable).
- MinIO bucket creation: bucket exists after compose up.
- Seq ingestion: a log entry appears in Seq within 10 s of API startup.

### 3.13 Backup / Restore tests

**Frequency:** Monthly + pre-release.
**Steps (manual runbook, partially scripted):**
1. Execute nightly backup script on staging VM.
2. Transfer backup to standby VM.
3. Restore SQL backup: `RESTORE DATABASE` [unverified: exact SQL Server restore syntax in Docker].
4. Restore MinIO data (rsync or `mc mirror`).
5. Start ACMP API; run smoke-test suite against restored instance.
6. Assert row counts match source; spot-check three known decisions/votes.

### 3.14 Integration contract tests (external adapters)

**Approach:** All external adapters (Webex, Tarseem, Keystone) are tested via **mocked HTTP server** (WireMock.Net [unverified] or local stub) asserting the request/response contract. No real external calls in CI.

| Adapter | Contract tests |
|---|---|
| `IWebexAdapter` | Meeting create, message send, webhook receive, 429 retry, circuit-breaker open |
| `ITarseemAdapter` | Render request (JSON spec in → SVG out); unhealthy response; timeout |
| Keystone import | Manifest parse, FR import, decision import, risk import, unknown-field tolerant |

---

## 4. Test Data Strategy

### 4.1 Test Builders (Fluent Builders)

Each domain aggregate has a corresponding **builder** in `tests/TestBuilders/`:
```
TopicBuilder.Create().WithType(TopicType.ArchitectureDecision).WithStatus(TopicStatus.Submitted).Build()
VoteBuilder.Create().WithEligibleVoters(3).WithQuorum(2).Build()
DecisionBuilder.Create().AsIssued().Build()
```
Builders default to valid, complete objects; chain methods override specific fields.

### 4.2 Seed Data

- **Unit/Handler tests:** builders only; no DB.
- **API Integration tests:** each test collection seeds its own minimal data set via a `SeedHelper` class that calls EF Core directly (not the API).
- **E2E tests:** dedicated `seed.sql` and `seed-minio.sh` scripts run before each E2E suite. Seed includes: 3 users (Chairman, Secretary, Member), 5 topics across all types, 2 completed votes, 1 issued decision, 2 open actions.
- **Staging:** richer seed file (`deploy/seed/staging-seed.sql`) with realistic bilingual (EN/AR) data including Arabic-named topics and Arabic decisions.

### 4.3 Mocking Boundaries

| Boundary | Test layer | Mock approach |
|---|---|---|
| Keycloak JWT validation | All layers above unit | `TestAuthHandler` injects claims; no real KC call |
| SQL Server | Unit, Handler | In-memory EF Core (for pure logic only) |
| SQL Server | Integration | Real Testcontainers SQL Server 2022 |
| MinIO `IFileStore` | Unit, Handler | `NSubstitute` mock |
| MinIO | File-upload integration | Real MinIO container (Testcontainers) |
| `INotificationChannel` | Handler | `NSubstitute` mock; assert `Publish()` called |
| Hangfire jobs | Handler | Mock `IBackgroundJobClient`; assert enqueue call |
| Seq | All | No mock; Seq container optional; logs buffered in tests |
| `ITarseemAdapter` | Handler, API Integration | WireMock stub |
| `IWebexAdapter` | Handler, API Integration | WireMock stub |
| `IClock` | All | `NSubstitute` mock; inject fixed `DateTimeOffset` |

**Rule:** Real DB and real MinIO only in Integration tests and above. All other tests mock at the interface boundary.

---

## 5. Acceptance-Test Structure (Gherkin-ish mapping to AC-###)

Acceptance criteria are defined in `docs/40-acceptance-criteria.md`. Each `AC-###` maps to at least one test:

```
Feature: Vote on a decision [AC-042..AC-048]
  Scenario: Chairman casts deciding vote — immutable record
    Given a topic in InCommittee status
    And a Vote ballot is Open with 3 eligible voters
    When Chairman submits a vote of "Approved"
    Then the vote is recorded with ChairmanId and timestamp
    And the ballot moves to Closed
    And a VoteClosed domain event is raised
    And an AuditEvent is appended [AC-044]
    And any subsequent mutation attempt returns 422 [AC-047]
```

These are expressed as **xUnit integration tests with descriptive names** (not Gherkin files unless the team adopts SpecFlow — not currently planned). Each test method name includes the `AC-###` reference:
```csharp
[Fact(DisplayName = "AC-044: VoteClosed event creates AuditEvent")]
public async Task Vote_Cast_Creates_AuditEvent() { … }
```

**Traceability matrix:** CI generates a test-result XML; a post-build step maps test DisplayName AC references to the acceptance criteria list; any AC-### with zero passing tests = build warning (Phase 2: block).

---

## 6. Quality Gates

### 6.1 Keystone gates (applied to the planning package itself)

| Gate | ID | How satisfied |
|---|---|---|
| All IDs resolve | G-IDS | README §F IDs used consistently; `docs/42-open-decisions.md` lists unresolved `OQ-` |
| Every decision has a status | G-DEC-STATUS | ADR table in README §A; all 12 ADRs have status in `adr/` |
| Every FR/NFR has provenance | G-REQ-SRC | `docs/07-functional-requirements.md` has source column |
| No TODO/placeholder | G-COMPLETE | Review pass before package release |
| Every MVP req → ≥1 decision, ≥1 work item, ≥1 test | G-TRACE | `docs/30-search-and-traceability.md` traceability matrix |
| All "Always" artifacts present | G-SET | Deliverable index in README checked |
| Acceptance audit verdicts | G-PROGRESS | `docs/45-release-readiness-checklist.md` |

### 6.2 Code quality gates (enforced in CI — see `docs/32-devsecops-plan.md` for pipeline)

| Gate | Tool | Threshold | Block? |
|---|---|---|---|
| Unit + handler tests green | xUnit | 0 failures | Yes |
| API integration tests green | xUnit | 0 failures | Yes |
| Frontend tests green | Vitest | 0 failures | Yes |
| Line coverage ≥ 80% (domain + application layers) | Coverlet + ReportGenerator | 80% | Yes |
| Branch coverage ≥ 70% (domain + application layers) | Coverlet | 70% | Yes |
| SAST: zero High/Critical findings | CodeQL | 0 | Yes |
| Dependency vuln: zero High/Critical | dotnet + npm audit | 0 | Yes |
| Secret scan: zero findings | Gitleaks | 0 | Yes |
| Container image: zero Critical | Trivy | 0 | Yes |
| axe-core: zero critical/serious violations | jest-axe + Playwright | 0 | Yes |
| i18n key parity EN↔AR | Custom script | 0 missing | Yes |
| Architecture rule (no cross-module internal references) | ArchUnit.NET | 0 violations | Yes |
| EF migrations idempotent | Migration test | Pass | Yes |
| AC-### coverage > 0 for all MVP ACs | Test-map script | 100% | Phase 2 |

---

## 7. CI Stages

See `docs/32-devsecops-plan.md` §3 for the full pipeline. Test-relevant stages:

| Stage | Tests | Trigger | Time budget |
|---|---|---|---|
| 1. Format | None | Every push | < 30 s |
| 2. Build | None | Every push | < 2 min |
| 3. Unit | Unit + handler | Every push | < 2 min |
| 4. Frontend | Vitest + RTL | Every push | < 2 min |
| 5. Integration | API + DB + MinIO | PR→main, main | < 8 min |
| 6. SAST + Scan | CodeQL, audits, secrets | PR→main | < 5 min |
| 7. Container build + Trivy | Image scan | PR→main | < 5 min |
| 8. E2E | Playwright (staging stack) | PR→main, release | < 10 min |
| 9. Performance | k6 (staging) | Release only | < 5 min |
| 10. Security DAST | ZAP baseline | Weekly + release | < 10 min |

---

## 8. Minimum Release Criteria

Before any phase-gate release, **all** of the following must be true:

- [ ] All unit, handler, API integration, frontend, and migration tests pass.
- [ ] E2E happy-path flows pass on staging docker-compose stack.
- [ ] All quality gates in §6.2 green.
- [ ] Permission-matrix test suite passes for all 8 canonical roles.
- [ ] Voting-integrity test suite passes (all rows in §3.3).
- [ ] Decision-immutability tests pass (all rows in §3.4).
- [ ] Audit-trail tests pass (100% coverage of auditable events).
- [ ] i18n parity: EN and AR key sets match; Arabic renders correctly in RTL mode.
- [ ] axe-core zero critical/serious violations on all page-level E2E tests.
- [ ] ZAP baseline: no High findings (or documented risk-accepted exceptions).
- [ ] Performance: P95 ≤ 2 s for core pages under 15 VU load (k6).
- [ ] Backup/restore test executed and passed in staging within 30 days.
- [ ] `docs/45-release-readiness-checklist.md` signed off by lead.

---

## 9. Per-Phase Test Focus

| Phase | Focus | New test categories added |
|---|---|---|
| **PH-1** (core loop: intake→vote→decision) | Core workflow, domain rules, auth/authz, audit trail, voting integrity | Unit, handler, API integration, migration, permission matrix, voting, immutability |
| **PH-2** (Webex, Tarseem, notifications, diagrams) | Adapter contracts, file upload, MinIO, render pipeline | Contract tests (Webex, Tarseem), file-upload, container smoke |
| **PH-3** (AI extraction, research, Keystone import) | AI candidate output validation, human-review gate, Keystone manifest import | Keystone contract, AI output schema tests |
| **PH-4** (polish, reporting, search enhancement) | Performance, accessibility full audit, reporting correctness | k6 extended, axe-core full coverage, ZAP block mode |

---

## Traceability

Links: `docs/40-acceptance-criteria.md` (AC-### ↔ test names) · `docs/32-devsecops-plan.md` (CI stages) · `docs/10-permission-role-matrix.md` (permission-matrix test rows) · `docs/08-non-functional-requirements.md` (NFR targets for perf/a11y gates) · `docs/26-audit-and-records-management.md` (auditable-event list) · `../README.md` §A (ADR-0009 immutability, ADR-0010 voting) · `docs/20-keystone-analysis-and-integration.md` (Keystone quality gates G-IDS…G-PROGRESS).
