---
name: coverage-and-e2e-mandate
description: "ACMP standing goal: ≥95% test coverage (frontend AND backend) + comprehensive adversarial E2E across every flow/screen."
metadata: 
  node_type: memory
  type: project
  originSessionId: 687c0d29-6db3-4063-b6e2-b604868200a6
---

Operator mandate (2026-06-29), starting after PR-B: drive ACMP to **≥95% line coverage on BOTH
frontend and backend**, with **comprehensive E2E covering every flow and every screen**. Test the
**bad before the good** — adversarial, failure-first: "what can happen, will happen." Authz denials,
validation failures, IDOR, immutability/audit/hash-chain, status-transition guards, quorum/abstention
edge cases, empty/loading/error/denied states, RTL, a11y, i18n enum completeness, optimistic rollback,
dirty-form/route guards, 401s, notification fan-out (≥2 members).

**Tooling state at kickoff (verify, then build on):**
- Frontend: `@vitest/coverage-v8` is installed but there is **no coverage config or thresholds** in
  `src/Acmp.Web/vitest.config.*` yet — must add provider/include/exclude/thresholds, then baseline
  with `vitest run --coverage`.
- Backend: `coverlet.collector` is in `Acmp.Api.Tests` only (confirm the other 3 test projects:
  Application/Architecture/Domain) — baseline with `dotnet test --collect:"XPlat Code Coverage"` +
  reportgenerator.
- E2E: **no committed Playwright suite exists** (no `@playwright/test`, no `playwright.config`, no
  `e2e/`). The MCP Playwright used for visual verify is ad-hoc, not committed. A real E2E harness must
  be stood up (new dev dep + config + specs + likely a CI job).

**How to apply:** this is a multi-PR program, GO-gated per slice. (1) FIRST establish coverage tooling
+ baseline numbers and agree the basis with the operator (line %, and which files are legitimately
excluded — entrypoints/`main.tsx`, dev-only `DevRoleSwitcher`, generated, config). (2) Plan slices to
close gaps to 95%. (3) Stand up `@playwright/test` for E2E of the core loop
(topic → agenda → meeting → minutes → notify) + auth round-trip, per screen. Keep the Standard Footer.
See [[phase-prompt-standard-footer]], [[i18n-parity-not-completeness]], [[web-visual-verify-cache-busting]].

**SETTLED (2026-06-29, ADR-0016 + operator GO).** Measured baseline @ `b7ab531`: **FE 82.94%**
(4270/5148 lines, honest denominator), **BE 39.1% raw** but only ~436 uncovered lines outside the
`*.Infrastructure` assemblies (Domain 89–99%, most Application 82–98%, Topics.Application 69.5% is the
outlier, Api 71%). KEY FACT (verified, not assumed): the adversarial invariants are CODE-enforced, so
they run on the existing **EF InMemory** `AcmpWebApplicationFactory` — immutability is a domain guard
(`Topic.cs:264`, `Agenda.cs:83/133` throw), IDOR is handler/LINQ (`CurrentActor`+`ICurrentUser`), audit
+ hash-chain are C#. Only DB-enforced backstops (`.IsUnique()` indexes like `(MeetingEntityId,UserId)`
and `(AgendaEntityId,TopicId)`, FK cascade, concurrency, migrations) need real SQL → that is the
Testcontainers side-suite, NOT the path to 95%.

Decisions: (1) **Basis = ≥95% LINES** on assertable code, **global + per-file** thresholds. Hard-exclude:
BE = `**/Migrations/*.cs` + `[GeneratedCode]`/`[CompilerGenerated]`/`[ExcludeFromCodeCoverage]`/
`[DebuggerNonUserCode]` + `[Acmp.Api]Program` (via repo-root `coverlet.runsettings`); FE = `src/main.tsx`,
`DevRoleSwitcher.tsx`, `src/test/**`, `*.d.ts` (in `vitest.config.ts` `coverage`). **`App.tsx` is NOT
excluded** (route/dirty-form guards must be tested). DI extensions NOT excluded (covered at boot).
(2) **E2E = `@playwright/test`** against the real compose stack incl. genuine Keycloak PKCE, run on
**`pull_request`→`main` + `workflow_dispatch`** (gates merge to main + on demand), NOT per branch push.
(3) **Testcontainers DB-backstop suite = INCLUDED (full).** Tooling: `npm run test:cov` (FE), `dotnet test
--settings coverlet.runsettings --collect:"XPlat Code Coverage"` + reportgenerator (BE). CI gate flips to
fail-under-95 only in the LAST slice (S7) so main never goes red mid-climb. Slice order S0→S7 in ADR-0016.
After exclusions baseline: FE **83.74%**. S0 branch: `chore/coverage-tooling-basis`.

**S1 SHIPPED (PR #39, squash-merged into main @ `6aea73c`, 2026-06-29).** Backend adversarial invariants:
40 failure-first tests over the 0%-covered Application handlers/validators (Topics Update/Defer/Prepare/
Prioritize/Reject, Meetings End/Cancel/RemoveAgendaItem + validators, Membership CreateDelegation) —
asserting authz-deny, 404, domain status/immutability guards, audit emission on the InMemory stack. **BE
89.1% → 97.6%** (1976/2024). KEY: Topics handlers authorize per-resource via `IResourceAuthorizer` **inside**
the handler (authz-deny IS asserted there); Meetings/Membership commands role-gate via the MediatR
`AuthorizationBehavior` (`IAuthorizedRequest`) — that's a PIPELINE test, do NOT fake it at the handler layer
(assert 404 + domain guards + audit instead). **ADR-0016 basis amended** (operator-confirmed): coverlet now
also excludes `**/*DbContextFactory.cs` (design-time `IDesignTimeDbContextFactory`, never runtime — same class
as `Program.cs`) + `**/MinioFileStore.cs` (Phase-2). No AC verdicts flipped (G-TRACE: `Met` needs the live
HTTP/UI leg → P17); acceptance-audit AC→test mapping began (AC-031/032/034/035/043).

**S2 SHIPPED (PR #40, squash-merged into main @ `6176a52`, 2026-06-29).** Frontend auth + data layer: 88
failure-first tests over the ~0–60%-covered surface (12 files + coverage-excluded harness `src/test/
queryHarness.tsx`). **FE 83.74% → 94.83% lines**; the whole S2 surface (`App.tsx`, all `api/*`, all `auth/*`,
`LoginPage`, `AuthCallbackPage`) is at **100% lines**. KEY APPROACH: screen tests `vi.mock` the api hooks away,
so the real hooks/providers never ran — S2 flips the boundary, running the REAL hooks/providers against a
stubbed `global.fetch` (helper `stubFetch` + `makeQueryWrapper`). Covered: apiClient (token/Accept-Language/
204/RFC-7807→ApiError/non-JSON fallback), queryClient retry (4xx no-retry), topics+meetings+notifications+
members hooks (URL/body/invalidation), AuthProvider (fail-closed/DEV-stub/OidcBridge claims→roles/provision
POST `/members/me` carrying bearer — "post-login never 401"/expiry→authStatus), authConfig (PKCE/scope),
authStatus, ProtectedRoute, App route tree, AuthCallbackPage, LoginPage banners. GOTCHAS (carry forward):
(1) NO optimistic-update logic exists in `api/` (zero `onMutate`) → rollback tests are N/A, don't invent them.
(2) `<Navigate>` redirects NO-OP in the `createMemoryRouter` data-router test harness (jsdom+undici AbortSignal
brand-check bug) — assert redirect behaviour in declarative `<Routes>` tests instead (ProtectedRoute.test.tsx),
or assert observable outcomes. (3) Storage-throw tests must SWAP the global `sessionStorage` object — spying
`Storage.prototype` does NOT intercept the test env's storage (false-green trap). No AC verdicts flipped.
Global 94.83% (<95%) is BY DESIGN — screen-state remainder (`components/ui`, `PlaceholderPage`, `ErrorBoundary`,
`AppShell`, `Card`, etc.) is S4.

**S3 SHIPPED (PR #41, squash-merged into main @ `3e6f8bc`, 2026-06-29).** Scope widened (operator: "B, one PR")
from "Api endpoints" to **finish the WHOLE backend to per-file ≥95%** — Api endpoints + ~14 domain/app/shared
stragglers. 101 failure-first tests (458 → 559). **BE 97.6% → 99.6% lines; EVERY backend file now ≥95%**
(per-file-gate ready); Api assembly 94.7% → **100%**. No new exclusions (every gap was reachable code). Built
largely by 4 parallel sub-agents into new non-overlapping files. Covered: Topic/Meetings HTTP endpoints
(defer/priority/update; agenda move/timebox/presenter; conduct attendance/discussion/actual-time; cancel +
403/400), Topic Close/Convert + events, TopicComment/TopicAttachment, MemberStreamAssignment/Delegation/
CommitteeMember, AssignStreamsValidator, GetBacklog filter/sort branches, Notification, CurrentUserService,
BaseEntity, LocalizedString, ITopicScheduler. GOTCHAS (carry forward): (1) BE coverage = `dotnet test
--collect:"XPlat Code Coverage" --settings coverlet.runsettings` → `reportgenerator` (TextSummary); the
per-file basis is reportgenerator's MERGED report — parse single cobertura files with care (merge all 4 test
projects). (2) Private EF constructors (`TopicAttachment`, `MemberStreamAssignment`) are only covered by a
real save-then-reload in a FRESH `DbContext` (Include the nav) — NOT by unit construction; don't exclude them.
(3) The `ITopicScheduler` success path needs a DIRECT unit test — HTTP agenda tests pass synthetic topicIds
with no matching Topic so the seam short-circuits. (4) `dotnet format --verify-no-changes` is a gate; new .cs
files need UTF-8 BOM + no trailing whitespace — run `dotnet format` to auto-fix before verify. (5) Sub-agents
can lose in-process state if a process exits mid-run; their files may already be on disk — reconcile via
`git status` before re-doing. No AC verdicts flipped.

**S4 SHIPPED (PR #42, squash-merged into main @ `1d862e8`, 2026-06-29).** FE screen-state cleanup: 121 tests (225 → 346). **FE 94.83% → 98.46% lines; EVERY FE file now ≥95%** (verified via vitest v8 `coverage/coverage-summary.json`). Covered the UI primitives (Pagination/Select/Dialog/Field/DateField/MultiSelect keyboard+edge paths via `components/ui/coverage.test.tsx`), ErrorBoundary, PlaceholderPage, meetingStatus, NotificationCenter states, and feature gaps (SubmitTopic 84→95.5%, AgendaBuilder 91.6→95.2%). **DEVIATION (operator-approved): 4 documented `/* v8 ignore start/stop */` comments** (comment-only, no behaviour change) on genuinely browser-only paths jsdom can't run — `SortableList.onDragEnd` (@dnd-kit pointer drag), `AgendaBuilder` native HTML5 drag handlers (item + pool card), `AgendaBuilder.AgendaPreview` empty branch (defensive/unreachable), `SubmitTopic.saveDraftAndLeave` storage catch (defensive). The accessible Move-up/down + click-to-add fallbacks ARE unit-tested; the **drag paths are deferred to S6 E2E**. GOTCHAS (carry forward): (a) FE per-file gate check = `node -e` over `coverage/coverage-summary.json` (the text reporter truncates uncovered line lists; parse the v8 JSON for exact lines via `coverage-final.json` statementMap). (b) jsdom has NO IntersectionObserver and native/ dnd-kit drag — those paths are E2E-only. (c) axe in this repo = `import axe from 'axe-core'` + `axe.run(container,{runOnly:{type:'tag',values:[...wcag]}, rules:{'color-contrast':{enabled:false}}})`, NOT vitest-axe. (d) feature/screen tests `vi.mock` the api hooks — extend those mocks rather than hitting fetch.

**S5 SHIPPED (PR #43, squash-merged into main @ `2d1a91b`, 2026-06-30; CI green incl. the backend job running the real SQL container in 2m30s).** Testcontainers SQL-Server DB-backstop suite: new proj `tests/Acmp.Integration.Tests` in `acmp.sln`, `Testcontainers.MsSql` 3.10.0, one shared `MsSqlContainer` collection-fixture that MigrateAsync's all 4 module contexts (= migrations-apply proof). 11 failure-first tests, full suite 570 pass. Backstops proven (SQL rejects, with InMemory-accepts twin where it's the contrast): `Meeting.Key` unique (twin), Delegation→Member FK `OnDelete(Restrict)` (twin), `Agenda.MeetingId` unique, `CommitteeMember.KeycloakUserId` unique, `Email` FILTERED unique (2 real dups reject / 2 empty allowed), and the two composite owned-collection indexes `(MeetingEntityId,UserId)` + `(AgendaEntityId,TopicId)`. GOTCHAS (carry forward): (1) EF EAGERLY loads `OwnsMany` owned collections with the owner — there is NO "load-without-Include" trick; the aggregate dedupe (`Meeting.SeedAttendee`, `Agenda.AddItem`) always fires, so a duplicate owned-child row can ONLY be made via a raw non-aggregate `INSERT … SELECT` (copy seeded row, fresh `PublicId`) — that's the realistic backstop path anyway. (2) raw-insert violation surfaces as `System.Data.Common.DbException` (SqlException), NOT `DbUpdateException` (that's only for SaveChanges). (3) `[Order]` is a SQL reserved word — bracket it in raw SQL. (4) needs Docker locally now (`dotnet test acmp.sln`); operator's Docker Desktop was stopped — launch `"C:\Program Files\Docker\Docker\Docker Desktop.exe"` and poll `docker info`. **NEW DRIFT FLAG OQ-043 (docs/42):** docs/16 §1.5 mandates `RowVersion ROWVERSION` optimistic concurrency on mutable roots → 409, but ZERO concurrency tokens exist in code — can't test a non-existent backstop; adding it = feature+migration slice, NOT S5 (guardrail #11). So ADR-0016 §3's "concurrency/rowversion" item is DEFERRED to that future slice, not done in S5.
**S6a SHIPPED (PR #44, squash-merged into main @ `18090eb`, 2026-06-30; ALL 4 CI checks green incl. the new `e2e` job passing in 3m31s in CI — builds images + boots the 8-service stack + seeds + runs real PKCE specs).** E2E harness (ADR-0016 §2): `@playwright/test` under `src/Acmp.Web/` — `playwright.config.ts`, `e2e/global-setup.ts` (waits realm+SPA health, then seeds per-role test users `e2e-secretary/-chairman/-member` via Keycloak ADMIN REST API, fixed pw `E2e!Passw0rd`, idempotent), `e2e/users.ts`, `e2e/login.ts` (drives the REAL Keycloak form `#username`/`#password`/`#kc-login`), `e2e/auth.spec.ts` (2 tests), gated `.github/workflows/e2e.yml` (`pull_request→main` + `workflow_dispatch`, brings up stack→seeds→runs→traces→`down -v`), `package.json` (`@playwright/test`^1.49.1 + `e2e`/`e2e:up`/`e2e:down` scripts), and `vitest.config.ts` `include:['src/**/*.{test,spec}.{ts,tsx}']` so vitest skips `e2e/`. **Both auth specs PASS locally against the live 8-service stack** (deep-link→/login; real PKCE→authenticated dashboard); build + 346 unit tests still green. **AUTH-SEED FINDING (§2 first task):** shipped realm ships only `acmp-admin` with `UPDATE_PASSWORD` required + client `directAccessGrantsEnabled:false` → CANNOT drive unattended E2E; hence admin-API seeding, prod realm export UNTOUCHED. **GOTCHAS (carry forward):** (1) compose `kcdata`/`mssql-data` volumes pin Postgres/SQL passwords at first-init — a STALE volume from a prior run makes keycloak (`password authentication failed for user "keycloak"`) + SA login fail; ALWAYS `npm run e2e:down` (down -v) before `e2e:up` to re-init with current `.env.example` creds. (2) `keycloak.localhost` resolves to loopback in Chromium (incl. headless) — SPA at :8088 redirects to KC :8085 fine. (3) `e2e:up`/`e2e:down` run from `src/Acmp.Web` using `../../deploy/docker-compose.yml --env-file ../../deploy/.env.example`; must `set -a; source ../../deploy/.env.example` before `npm run e2e` so global-setup gets KC admin creds (`admin`/`ChangeMe_KC#2026`). **S6b (next) = core loop (topic→agenda→meeting→minutes→notify) + S4-deferred drag paths (@dnd-kit + native HTML5) + RTL/axe pass on the same harness — each needs reading + live-iterating its feature UI. Then S7 = flip CI coverage gate to fail-under-95 per-file, both stacks (last slice).**

**S6b-1 SHIPPED (PR #45, squash-merged into main @ `adcb613`, 2026-06-30; ALL 4 CI checks green incl. the
`e2e` job at 4m11s — full stack boot + the real PKCE core-loop run in CI).** Core-loop E2E (ADR-0016 §2):
new `e2e/core-loop.spec.ts` (one failure-first spine) + `e2e/apiHelpers.ts`. Flow (all REAL UI except 2
API-assisted steps): secretary submits topic → accepts in Kanban (keyboard "M" move popover → AcceptDialog,
owner=member) → **[API] prepare** → schedule meeting (self-served, chair defaults to chairman) → build agenda
(add + **assign presenter** — REQUIRED) → **publish (= notify fan-out)** → start → conduct (mark attendance +
capture discussion note) → end → **minutes placeholder gate** asserted → notification bell verified for BOTH
recipients (member + chairman). Decisions settled this slice: A=map "sortable reorder" onto the native drags
(SortableList is mounted by NO screen → not E2E-able, skipped); B=verify BOTH recipients (3 logins:
secretary actor + member + chairman); 3 small per-flow PRs. **KEY FACTS (verified live):** (1) stack boots
EMPTY — no DB seeder; members exist only after login self-provisions an *active* `CommitteeMember`
(`CommitteeMember.Provision` sets Status=Active), so recipients log in once BEFORE publish. (2) Topic→Prepared
has NO v1 UI (Kanban only does triage→accepted/→returned); `POST /topics/{id}/prepare` is API-only, ABAC
`Policies.TopicEdit` = Secretary's own bearer works. (3) Bearer captured by sniffing the `Authorization`
header off a live `/api` request (KC direct-grant off). (4) **Minutes = placeholder** (MoM=P7) → assert the
gate, never fake it; "notify"=publish fan-out. (5) The live leg caught a real FALSE-GREEN: `Agenda.Publish`
throws "Every agenda item needs a presenter before publishing" — unit suite asserts the guard in isolation,
only the UI proves the builder lets you satisfy it; spec assigns a presenter + asserts publish→200.
**GOTCHAS (carry forward):** (a) `getByLabel('X',{exact})` FAILS on `Field`-wrapped inputs — `<label>` text
is `"X*"` (required `*` is aria-hidden from the NAME but present in label TEXT); use
`getByRole('textbox',{name,exact})` (accessible name excludes `*`). (b) Modals leave sibling chrome in DOM —
scope dialog actions via `page.getByRole('dialog').getBy…` (Backlog "Owner" filter chip collided with
AcceptDialog owner Select); assert close via `getByRole('dialog').toHaveCount(0)`, NOT a chrome element that
persists. (c) Local stack is FAST — full 3-login loop runs ~4s, so genuine passes look "too fast"; confirm
via `docker logs acmp-api-1` (Publish/Start/MarkAttendance/CaptureDiscussion/End commands), not wall-clock.
(d) `getByRole('button',{name:'Date',exact})` for the DateField trigger; pick a day via `.datepicker-day.is-today`.
(e) Segmented = plain buttons (`getByRole('button',{name:'Kanban'})`); Tabs = `role="tab"`. (f) Routes:
schedule = `/meetings/new`; submit = `/topics/new`. No AC verdicts flipped (deferred to end of S6b).
**NEXT = S6b-2** (native HTML5 drag — AgendaBuilder pool→agenda + within-agenda reorder, Kanban card→column;
handlers read React refs not `dataTransfer` so Playwright `dragTo` should fire them, spike once first — +
failure-first authz/validation), then S6b-3 (RTL/Arabic + axe), then S7 (flip CI coverage gate to
fail-under-95). Branch was `chore/s6b-core-loop`.

**S6b-2 SHIPPED (PR #46, squash-merged into main @ `2253e08`, 2026-06-30; all 4 CI checks green, `e2e`
3m54s on fresh CI stack).** Native drag paths (the S4 `/* v8 ignore */`-deferred pointer drags) + failure-first:
new `e2e/dnd-and-failures.spec.ts` (7 tests) + `e2e/scenario.ts` (API setup helpers + `dragHtml5`). DRAG (all
GREEN first try): Kanban card→Accepted col (opens AcceptDialog), Kanban card→Scheduled col (illegal →
announced in `[aria-live="assertive"]`, no dialog), AgendaBuilder pool card→agenda region (adds item),
AgendaBuilder item2→item1 (single ±1 reorder). FAILURE-FIRST: member denied schedule (`Policies.MeetingSchedule`
→403 + UI error), schedule form blocks empty (required errors, no request) + inverted window, publish DISABLED
on empty agenda + Start GATED until published. **DRAG MECHANISM (settled):** handlers store state in React
refs/state on `dragstart`, read on `drop` (NO `dataTransfer` payload) → `dragHtml5(src,tgt)` dispatches the
DnD events directly (`source.dispatchEvent('dragstart')`/`target.dispatchEvent('dragover'|'drop')`/
`source.dispatchEvent('dragend')`) — MORE deterministic than `locator.dragTo()` geometry sim; centralized so
it's a 1-line swap if ever needed. **Decision A realised:** `@dnd-kit SortableList` is mounted by NO screen
(only its own unit test) → NOT E2E-able, intentionally skipped; the native handlers cover the "sortable
reorder" target. **NEW GOTCHA (carry forward):** JIT provisioning (`POST /members/me`) is ASYNC on login, so
the FIRST `/api/members` read after a login races it (first secretary login in a fresh-stack run failed while
later ones passed) — force it: `await page.request.post('/api/members/me',{headers:{Authorization:bearer}})`
(idempotent) BEFORE querying the directory. **Setup pattern:** drive non-under-test setup (create/accept/
prepare topic, schedule meeting, add agenda item) via API with the captured bearer; reserve UI for the
action asserted. Validated 10/10 on a freshly RESET stack (down -v → up → full suite) to mirror CI exactly
before push. No AC verdicts flipped (deferred to end of S6b). **NEXT = S6b-3** (RTL/Arabic dir=rtl + axe-clean
on a key authenticated screen — last E2E slice), then S7 (flip CI coverage gate to fail-under-95 per-file).
Branch was `chore/s6b-2-dnd-failures`.

**S6b-3 SHIPPED (PR #47, squash-merged into main @ `deeb6a0`, 2026-06-30; all 4 CI checks green, `e2e`
4m7s on fresh CI stack). E2E MANDATE (ADR-0016 §2) COMPLETE.** RTL/Arabic + a11y, last E2E slice: new
`e2e/rtl-a11y.spec.ts` (3 tests): (1) app flips to RTL Arabic from the TopBar toggle — `<html>` `dir` ltr→rtl
+ `lang=ar`, toggle then offers English (i18n really switched); (2) Backlog axe-clean (0 WCAG 2a/2aa) in
BOTH EN + AR/RTL; (3) Submit-Topic axe-clean in BOTH EN + AR/RTL. **KEY GOTCHA (carry forward):** the app
ships a STRICT CSP (`script-src 'self'`) → `page.addScriptTag` (inline injection) is BLOCKED; inject axe by
reading the source (`readFileSync(require.resolve('axe-core/axe.min.js'))`) and running it via
`page.evaluate(AXE_SOURCE)` — CDP eval BYPASSES the page CSP. NO new dep (reuse installed `axe-core`).
`color-contrast` disabled in the axe run (match S4 unit convention; contrast = design-token/fidelity, out of
slice scope). RTL toggle button: `getByRole('button',{name:/Switch to/})`; `i18n/index.ts` sets
`document.documentElement` dir/lang on `changeLanguage`. App is genuinely a11y-clean (0 violations both
screens both locales). Validated 13/13 full suite on a freshly reset stack before push.

**E2E SUITE NOW = 13 tests across 4 spec files** (all merged to main, all green in CI): `auth.spec.ts` (2),
`core-loop.spec.ts` (1), `dnd-and-failures.spec.ts` (7), `rtl-a11y.spec.ts` (3). Shared helpers:
`login.ts`/`users.ts`/`global-setup.ts` (S6a), `apiHelpers.ts` (captureBearer, prepareTopic),
`scenario.ts` (API setup builders + `dragHtml5`). The whole suite runs ~15s locally on a warm stack; CI
`e2e` job ~4m (image build + 8-service boot + seed + run).

**PROGRAM STATUS: S0–S6b ALL MERGED to main @ `deeb6a0`. ONLY S7 REMAINS = flip the CI coverage gate to
fail-under-95 (per-file, both FE+BE), the LAST slice (main stayed green the whole climb; the hard gate is
wired only now that both stacks are already ≥95%).** ADR-0016 Validation step: a deliberately removed test
must turn CI red (prove the gate works), then revert. S7 changes CI gating → needs operator GO on the
approach (4-line shape). Deferred/optional: end-of-S6b **AC-verdict reconciliation** (the live HTTP/UI legs
now exist, so several `Met`-pending-P17 ACs in `docs/_progress/acceptance-audit.md` can be revisited), and
**OQ-043** (docs/42 — RowVersion optimistic concurrency: docs/16 mandates it but NO code implements it; its
own feature+migration slice, guardrail #11, NOT blocking S7).

**S7 SHIPPED (PR #48, squash-merged into main @ `076d65c`, 2026-06-30; all 4 CI checks green WITH the gate
now live). ADR-0016 TEST-HARDENING PROGRAM COMPLETE (S0–S7).** Flipped the CI coverage gate to fail-under-95
(global + per-file LINES, FE+BE). **FE:** `vitest.config.ts` → `coverage.thresholds: { lines: 95, perFile:
true }` (LINES only — the basis, not functions/branches, which would false-fail); CI `frontend` job test step
→ `npm run test:cov` (evaluates thresholds). **BE:** coverlet's own threshold is PER-ASSEMBLY only, so
per-file needs a custom check → new `scripts/check-coverage.mjs` unions line-hits across every per-project
cobertura report (a line covered if ANY test project hit it = true merged coverage, NO ReportGenerator dep),
fails if any file OR global <95%; coverlet.runsettings exclusions already applied at collection time so every
file it sees is in-scope. CI `backend` job → `dotnet test … --collect:"XPlat Code Coverage" --settings
coverlet.runsettings` + `node scripts/check-coverage.mjs .` (+ `actions/setup-node@v4`). Threshold is a CLI
arg (default 95) → the ADR's deliberate-red Validation is a deterministic repeatable command
(`node scripts/check-coverage.mjs . 100` trips, listing Topic.cs 96.47% + Agenda.cs 98.80%, exit 1) instead
of a throwaway test deletion — operator accepted the LOCAL fail-path proof (no extra CI-red cycle). Verified
locally: gate@95 passes (FE all files ≥95% lines; BE 113 files, global 99.65%, zero sub-95). `.gitignore`
already covers `TestResults/` + `coverage/`. GOTCHA: don't gate FE on functions/branches (only lines is the
basis); BE per-file is NOT native to coverlet — the union-merge script is the mechanism.

**PROGRAM DONE — ALL SLICES S0–S7 MERGED to main @ `076d65c`.** Final state: BE 99.6% lines (every file
≥95%), FE 98.46% lines (every file ≥95%), 13-test live E2E suite (auth + core-loop + drag/failures + RTL/axe)
gating PR→main, Testcontainers SQL DB-backstop suite, and a HARD per-file ≥95% line gate now enforced in CI
for both stacks. **OPTIONAL FOLLOW-UPS (not started, each its own slice if the operator wants them):**
(1) end-of-S6b **AC-verdict reconciliation** — the live HTTP/UI legs now exist, so `Met`-pending-P17 ACs in
`docs/_progress/acceptance-audit.md` can be revisited/flipped (no verdicts were flipped during S1–S7 by
design). (2) **OQ-043** (docs/42) — docs/16 mandates RowVersion optimistic concurrency (→409) but NO code
implements it; adding it = a feature+migration+test slice (guardrail #11), NOT part of the hardening program.

**PRIOR STATUS: both stacks per-file-gate-ready & MERGED to main @ `1d862e8` — BE 99.6% (PR #41), FE 98.46% (PR #42), every file ≥95%.** **NEXT = S5: Testcontainers SQL-Server DB-backstop suite (ADR-0016 §3), branch `chore/s5-testcontainers`. Orientation done (this session): backstops to prove = `(MeetingEntityId,UserId)` + `(AgendaEntityId,TopicId)` + `Agenda.MeetingId` + `Topic/Meeting.Key` + `Stream.Code` + `CommitteeMember.KeycloakUserId` unique, `CommitteeMember.Email` FILTERED unique (`WHERE [Email]<>''` — two emailless OK, two real-email reject), Delegation→Member FK `OnDelete(Restrict)`, + all 4 module migrations apply on real SQL. NEW proj `tests/Acmp.Integration.Tests` IN `acmp.sln` (operator GO), pkg `Testcontainers.MsSql`, one shared `MsSqlContainer` collection-fixture, each test paired with an InMemory-accepts twin (the contrast IS the proof). Runs in existing `backend` CI job (ubuntu has Docker), no new workflow. DRIFT FLAG → `OQ-`: docs/16 says mutable roots carry `RowVersion ROWVERSION` but NO code has any `IsRowVersion`/`[Timestamp]`/concurrency token — can't prove a backstop that doesn't exist; adding it is a feature+migration slice, not S5.** — prove the DB-enforced invariants InMemory can't: `.IsUnique()` backstops (e.g. `(MeetingEntityId,UserId)` one attendance row/member; `(AgendaEntityId,TopicId)` no duplicate agenda item), FK cascade, concurrency/rowversion, and that migrations apply on real SQL Server. CI already has Docker (the `compose` job). Then S6 E2E @playwright/test (covers the S4-deferred drag paths + Keycloak PKCE round-trip), S7 flip CI to fail-under-95 per-file.
