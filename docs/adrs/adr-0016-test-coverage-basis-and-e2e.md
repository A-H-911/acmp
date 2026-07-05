# ADR-0016: Test Coverage Basis (≥95% lines, FE + BE), Exclusions, E2E Harness, and DB-Backstop Integration Tests

- Status: Accepted
- Date: 2026-06-29
- Deciders: Architecture Committee (operator-confirmed, 2026-06-29)

## Context and Problem Statement

The operator set a standing mandate (see memory `coverage-and-e2e-mandate`): drive ACMP to **≥95% line coverage on BOTH frontend and backend**, plus **comprehensive, adversarial (failure-first) E2E covering every flow and every screen**. Coverage is the floor; correctness is the bar — shallow tests written only to raise the number are explicitly forbidden.

Measured baseline (2026-06-29, `main` @ `b7ab531`):

- **Frontend:** 82.94% lines (4270 / 5148), measured with `all: true` + `include src/**` (honest denominator). 410… 32 test files, 11 with axe.
- **Backend:** 39.1% raw across 14 assemblies — but this is anchored by the four `*.Infrastructure` projects (9.9–14.4%). The logic layers are healthy: Domain 89–99%, most Application 82–98% (Topics.Application is the outlier at 69.5%), Api 70.9%, Shared 84.1%. Outside Infrastructure, only ~436 lines are uncovered, and ~95 of those are generated/regex/migration noise.

Two facts drive the decisions below, both confirmed by direct inspection (not assumption):

1. **The adversarial "crown jewel" invariants are enforced in code, not the database.** Immutability is a domain guard (`Topic.cs:264` throws "A {Status} topic is immutable; supersede the linked decision instead."; `Agenda.cs:83/133` throw on publish/edit while in the wrong status). IDOR filtering is handler/LINQ (`CurrentActor` + `ICurrentUser`). Audit emission and hash-chain computation are plain C#. **All of these execute under the existing EF Core InMemory test provider** — no SQL Server required to assert them.
2. **Real SQL is needed only for database-enforced backstops:** the `.IsUnique()` indexes (e.g. `(MeetingEntityId, UserId)` = one attendance row per member; `(AgendaEntityId, TopicId)` = no duplicate agenda item), FK cascade behaviour, concurrency tokens, and migrations actually applying. EF InMemory silently accepts violations of all of these, which is a latent false-green risk in the current suite.

## Decision Drivers

- The 95% number must measure **real, assertable product code** — not generated scaffolding or composition roots that carry no branch to assert (otherwise the metric lies and invites gaming).
- Right-sizing (guardrail #12): ACMP is an on-prem, ≤20-user tool. Coverage tooling and E2E infrastructure must not become heavier than the problem warrants.
- The mandate explicitly requires the **real Keycloak PKCE round-trip** as an E2E flow — it cannot be faked away.
- CI must never be pushed red while the program is still climbing toward 95% (guardrail #13: `main` stays green); the hard fail-under gate is wired only at the end.

## Decision Outcome

### 1. Coverage basis: ≥95% **line** coverage, FE + BE, on assertable code

Hard-excluded from the denominator (genuinely un-assertable plumbing only):

- **Backend** (`coverlet.runsettings`): EF Core migrations + their `*.Designer.cs` (`**/Migrations/*.cs`); source-generated/compiler code (`[GeneratedCode]`, `[CompilerGenerated]`, `[ExcludeFromCodeCoverage]`, `[DebuggerNonUserCode]`); `Program.cs` (composition root, no branching); **design-time `IDesignTimeDbContextFactory` classes (`**/*DbContextFactory.cs`)** — run only by `dotnet ef migrations` at design time, never at runtime, so they carry no runtime branch to assert (same class as `Program.cs`); **`MinioFileStore` (`**/MinioFileStore.cs`)** — the Phase-2 S3 adapter, not wired into any v1 runtime flow (see the borderline note below; excluded with this Phase-2 note since no v1 slice touches it). DI-registration extension methods are **not** excluded — they run during the `WebApplicationFactory` boot and are covered incidentally.

  > **Amendment (S1, 2026-06-29, operator-confirmed).** The `*DbContextFactory.cs` and `MinioFileStore.cs` exclusions were added during S1. Rationale: the design-time factories are pure `dotnet ef` plumbing (the same un-assertable category as the already-excluded `Program.cs`), and `MinioFileStore` is the Phase-2 adapter the original §1 already earmarked for exclusion "at the slice that touches it." Crucially, S7 wires **per-file** thresholds — a 0%-covered design-time factory would fail a per-file gate regardless of the global number, and the only way to "cover" it is a theatre test that news-up the factory and asserts it returns a context. Excluding is the honest call. Effect: BE baseline rises from 94.5% to ~97% on the same test suite (60 design-time + 6 Phase-2 lines leave the denominator).
- **Frontend** (`vitest.config.ts`): `src/main.tsx` (ReactDOM bootstrap); `src/components/shell/DevRoleSwitcher.tsx` (dev-only, not shipped behaviour); `src/test/**` (test harness); `*.d.ts`. **`App.tsx` is NOT excluded** — it wires routing and route/dirty-form guards, which the adversarial matrix explicitly requires testing.

Everything else — repositories, handlers, validators, endpoints, components, and logic-bearing DTOs — is **covered by tests, not excused by exclusion**. Thresholds are enforced **global + per-file** (a 0% file cannot hide behind the average), wired into CI only in the final slice once both stacks are already ≥95%.

`MinioFileStore` (Phase-2 object-storage adapter, not wired into any v1 runtime flow) is a borderline case: covered with a mocked S3 client if cheap, otherwise excluded with a Phase-2 note at the slice that touches it.

### 2. E2E harness: `@playwright/test`, run at PR-to-`main` and on demand

`@playwright/test` (the project's stated E2E tool) drives the **real** application stack (web :8088 / api :8080 / Keycloak :8085) brought up via the self-contained Docker Compose stack, including the genuine Keycloak PKCE auth round-trip. A dedicated CI workflow runs it **on `pull_request` → `main`** (gating merges to main) and **on demand** (`workflow_dispatch`), not on every branch push. Specs are failure-first and cover the core loop (topic → agenda → meeting → minutes → notify), auth round-trip, schedule/build-publish-agenda/conduct-meeting, and notifications. The exact SPA auth-seed mechanism is verified as the first task of the E2E slice before specs are written.

### 3. DB-backstop integration tests: included (full), via Testcontainers SQL Server

A SQL-Server-backed integration suite (Testcontainers) proves the database-enforced invariants that InMemory cannot: the `.IsUnique()` backstops, FK cascade behaviour, concurrency/rowversion, and that migrations apply cleanly. This closes the genuine false-green gap in the current InMemory-only suite. CI already has Docker available (the `compose` job), so this is feasible; the cost is image pull + a slower integration leg, accepted as worthwhile for governance-data integrity.

### Consequences

- Good: the 95% number reflects real behaviour; the crown-jewel invariants are proven on the fast InMemory stack while the DB backstops are proven for real on SQL Server; merges to `main` are gated on a true end-to-end auth round-trip.
- Bad / trade-off: the E2E and Testcontainers legs make PR CI to `main` slower and introduce more moving parts (Keycloak realm import, stack health-wait, SQL image pull) — mitigated with deterministic health checks, `expect`-based waits, traces/screenshots on failure, and keeping these legs off the per-branch hot path. Per-file thresholds add maintenance when files are added (acceptable; it is the point).

## Validation

- `npx vitest run --coverage` produces a coverage report with the exclusions applied; baseline FE % after exclusions is recorded in the progress log.
- `dotnet test acmp.sln --collect:"XPlat Code Coverage" --settings coverlet.runsettings` + ReportGenerator produces a merged backend report with migrations/generated/`Program` removed; baseline BE % after exclusions is recorded.
- Final slice (S7): a deliberately removed test turns CI red (proves the gate works), then is reverted.
- E2E: specs pass against the live compose stack; a forced failure produces a Playwright trace artifact.
- Testcontainers: a duplicate `(MeetingEntityId, UserId)` insert is rejected by SQL Server but (demonstrably) accepted by InMemory — proving the backstop suite catches what the unit suite cannot.

## Links / Notes

- Memory: `coverage-and-e2e-mandate`, `i18n-parity-not-completeness`, `web-visual-verify-cache-busting`.
- Slice program: S0 tooling+basis (this ADR) → S1 BE adversarial invariants → S2 FE auth+data layer → S3 BE Api endpoints → S4 FE screen-state cleanup → S5 Testcontainers DB backstops → S6 E2E harness+core loop → S7 flip CI coverage gate to fail-under-95.
- Guardrails: #8 (no feature without tests/AC), #12 (do not overengineer), #13 (main stays green, gated merges).
- Note: a pre-existing ADR number collision exists at `0015` (two files); not addressed here.
