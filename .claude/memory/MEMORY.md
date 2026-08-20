# Memory Index — ACMP

> Compacted 2026-08-20 (9th). One line per memory; detail lives in topic files and the package.
> Read the linked file before acting. ⚠ Keep this file under ~17KB — past its read limit
> the tail is SILENTLY dropped on load, so an over-long index is worse than a short one.

## ★★ 2026-08-20 (later) · **THE DISPOSITION SESSION — v1 GOT BIGGER**

- ⚠⚠⚠ [**AN ID IS A POINTER, NOT A REFERENCE**](an-id-is-a-pointer-not-a-reference.md) — the operator
  **refused an interview** because the slate cited ~40 records by id alone. `LL-011`, **Approved+pinned**
  (11 lessons bind). Anything they read to DECIDE carries each record's full text inline, generated from
  the JSONL. ⭐ The fix found **`DEF-082` does not exist** though 3 records cite it as real and fixed —
  `G-IDS` checks FKs, **not ids in prose**. Operator chose to carry the gap (`DEF-101`), not reconstruct.
- ⚠⚠ **ALL TWELVE demand-triggered `DW-` rows `Activated`** (`DEC-067`/`SC-029`) — **against my
  recommendation**, recorded as an override. Nine remain unscheduled by intent.
- ⚠⚠ **A REQUIREMENT'S STATUS AND ITS `DW-` ROW'S STATUS ARE UNRELATED COLUMNS AND NOTHING COMPARES
  THEM.** Found via `DEC-064` d2 (*"DW-037 is ACTIVATED"*) that **never reached the row** while `SC-020`
  had marked `FR-035` `Deferred`. Fixed by `SC-028`; three more caught **before** writing.
  **Activating a `DW-` row → check its requirement in the same breath.**
- ✅ **`assumptions-current` FIXED — it FAILS now** (`ASM-011`, overdue on purpose). ⚠ the field is a
  **FUTURE due date**; more will redden — **that is the control working, don't clear them.**
  ⚠ **`deferred-work-reviewed` CANNOT go green from reviewing** — it selects `Open`+`Activated`+`Scheduled`.
- ⚠ **`DEF-102`: `NFR-013` mandates a columnstore `ADR-0022` (Approved) removed**; `DEC-020`/`ADR-0003`/
  `OQ-040` still assume it. Operator: *record it, change nothing.* Keyword sweep only (`LL-008`, 4th).
- ⚠ **`DW-052`'s premise is wrong** — caps are **50 MB / 2 GB**, not `NFR-011`'s 100 MB.
  ⚠ **`DW-037`'s data claim was half wrong** — `Topic.Schedule` does NOT persist the meeting id
  (`TopicScheduledEvent` has **zero consumers**); the calendar must read from the **Meetings** API.
- ⚠ **I reported "four" truncated assumption titles; it was EIGHT.** I measured inside the twelve rows I
  was already editing. **Measuring inside the set you are holding is not measuring the register.** All 8
  repaired; `DEF-092` widened. `ASM-001` → `Superseded`; `DW-029` → `Done`.

## ★ 2026-08-20 · **THE DW-029 PROGRAMME IS CLOSED** · durable bits only

- ✅ **EVERY PHASE IS `Implemented` EXCEPT `PH-3`**, which stays `Approved` on purpose (`WBS-20.4` is the
  email adapter vs a hard constraint, `DEC-055`; "repairing" it is the manufactured-status move `DEF-010`
  records). `SL-014` is `Deferred` (`P14`, `DEC-028`). **`PH-7`/`SL-032` now hold the live build work.**
- ⚠⚠ **A MENTION IS NOT A COVERAGE.** "named ANYWHERE in a DW row" made `NFR-018` vanish from a worklist
  because another row's prose mentioned it. **A well-cross-referenced register would report itself
  finished.** Match on TITLE, not prose.
- ⚠⚠ **`DEF-099` — OTLP traces had NEVER reached Seq in ANY environment.** ⭐ **The discriminator is the
  reusable part:** events grew 679→739 while DB spans sat at **exactly 72** — same host, port, container;
  log path delivering, trace path not. **Verify observability by sending traffic and asserting spans
  MOVE, never by a clean boot.**
- ⭐ **`LL-008`** sweep by KEYWORD not just id (an id-only sweep returned **0 hits for 6 of 7**).
  **`LL-009`** two instruments agreeing is ONE when they share a mechanism. **`LL-010`** if a requirement
  says X is the single source for Y, **check X exists before measuring Y**.
- ⚠ **COUNT THE REQUIREMENT'S CLAUSES BEFORE YOU COUNT YOUR FINDINGS** — twice, strong evidence covered
  two-thirds of a three-part requirement and nearly carried a `Met`.
- ⚠ **The operator declined to relax a requirement TWICE.** The register states the **TARGET**, not the
  status quo — never offer a narrowing as the easy path.
- ⚠⚠ **A number entering a durable artifact must come from output visible in the same breath** — an
  unmeasured count reached a commit message, which cannot be amended.
- ⚠ **`entity_query("requirement", ...)` OVERFLOWS the tool's token limit**; `columns` does not narrow it.
  Count from `data/requirements.jsonl`.
- ⚠ **Running a stack here:** `docker ps` empty does NOT mean safe — 5 populated volumes exist and
  `dev-up.sh` is `up -d --build`, the documented breaker. Isolated project, FRESH volumes, tag an existing
  CI image to skip the 3.62 GB FTS build, `sqlserver`+`seq` healthy first, then `down -v` and remove the tag.
- ⚠ **`entity_upsert` NOT NULL on UPDATE:** `defects.title/severity`, `scope_changes.decision_ref/
  description/iteration`, `slices.title/objective/phase_id`, `phases.title`. Nullable fields preserved by
  omission. ⚠ **CHECK constraints:** `verified_by IN (human|agent|ci)`, `verification_method IN
  (auto-test|manual|inspection)`, `progress.event_type` is a fixed set (no `decision-made`).

## ★ batches 14+15 — only what the head above does not repeat

- ⚠ **THE READER-CLOSABLE PHASE OF `DW-029` IS EXHAUSTED** — what remains needs a stack, a browser, a
  scanner, an unconfirmed org policy or an undeferred `P14`. **Re-run the rule rather than trusting any
  written list.** Remaining reader-closable: `NFR-018` (external ASVS L2) and `NFR-038` (rides `P14`).
- **Still open, needing a stack or a scanner:** `NFR-018` DAST + pentest, `NFR-019` TLS scan, `NFR-052`
  single-command startup, the ops group (`NFR-015 017 044 052 062`, `PE-485`), and much of `DW-043`…`DW-060`
  measured from trace data — **`DEF-099` is fixed so traces now arrive.** `DW-066` (alpine/distroless) is
  operator-only and its **risk is musl, not the two `FROM` lines**.

## ★ batch 13 — compacted; fuller record in `prm-next.md`

- ✅⚠ **`DEF-093` fixed upstream (4.4.2) — the old "never report gates green" rule is RETIRED.** ⚠⚠ The token
  rule changed in ONE direction: **journal text is exempt; every live ENTITY row is still screened**
  (`title`/`statement`/`description`) — there, name the concept or backtick the token.
- ⭐⭐ **Classifying rule, used every batch since: surface that DOES NOT EXIST can be excluded BY NAME in a
  `Met` verdict; surface that EXISTS but is unproven CANNOT.**
- ⚠⚠ **A COUNT OF THE ENFORCING MECHANISM IS NOT A MEASURE OF THE PROPERTY** (`LL-006`) — the `NFR-021`
  census read as a 38-command validation hole; all four commands carrying scalar input are guarded **in the
  domain**. ⚠ A defect row's predicted cause is a HYPOTHESIS (`DEF-067`).
- ⚠ **Never leave a Pending/Partial AC** — `acs-met` counts by `retired_in` and ignores `lifecycle_status`,
  so an AC ahead of its evidence holds readiness false **forever**. **A part-verified requirement gets a
  `DW-` row, not an AC.**
- ⚠ **`tsc --noEmit -p tsconfig.json` in `src/Acmp.Web` EXITS 0 OVER ZERO FILES** (`DEF-091`); `vitest` does
  NOT typecheck — use `npm run build` or `-p tsconfig.app.json`.
- ⚠ **`DEF-096`: `NFR-054`'s 500 MB cap is UNSATISFIABLE** (sqlserver-fts **3.62 GB**). `SC-024` narrowed the
  SIZE clause; the operator **REJECTED** relaxing minimal-base → **`DW-066`**. **Do not change a base image.**
- ⚠⚠ **Hangfire's `JobStorage.Current` + `GlobalJobFilters` are PROCESS-GLOBAL**; it never hands a filter the
  job's own exception — record `InnerException`.
- ⚠ **Coverage must be UNIONED, never summed.** ⚠ **`gh pr merge` can merge remotely then abort locally** —
  verify by CONTENT. ⚠ **Never push to a branch with CI in flight.**
- ⭐ **`SL-030` MERGED** (#295 → `1a52dba`). ⚠ `MoveTopicPriority` is deliberately UNFILTERED.
  ⚠ `Timeline.tsx`/`Calendar.tsx` are honest SHELLS — `Calendar.tsx`'s work is now `Activated`.
- ⭐ [**Store mechanics proven by experiment**](package-mechanics-proven-2026-08-18.md) — slice-scope
  `wbs-done` is vacuous for the 28 old slices (`DEF-087`); `G-TRACE` needs three legs.

## ★ Earlier state — durable facts only

- ⚠ **`PH-3` stays `Approved` ON PURPOSE** — `WBS-20.4` is the email adapter vs a hard constraint
  (`DEC-055`). Do **not** "repair" it; that is the manufactured-status move `DEF-010` records.
- **Ten lessons Approved + PINNED** (`LL-001`…`LL-010`) bind every session via the auto-loaded note.
  ⚠ **`LL-011` is Proposed and awaiting the operator's interview** — it does not bind until they approve.
- ⚠⚠ **`$?` AFTER A PIPE IS THE PIPE'S LAST COMMAND** — redirect to a file and read `$?` on the bare command.
- **PRODUCTION IS DEPLOYED AND RECONCILED**; `/readyz` 200 on all four checks. `RISK-007` adoption clock
  started 2026-08-17.

## Earlier 2026-08 — durable findings only

- ★★★ [**`DEF-078`: a green control can be blind**](a-green-control-can-be-blind.md) — a healthcheck that
  evaluated ZERO checks; gitleaks passing 153 commits over an allowlist covering every markdown file.
  ⚠ Read `ADR-0043`, **not** `ADR-0042` (Superseded).
- ★★ [**An absence needs a proven instrument**](an-absence-needs-a-proven-instrument.md) — `DEF-056`'s
  "measured blocker" was not real: the helper read a column that is NULL on the rows it counted, and its
  two `NotContain` controls passed **VACUOUSLY**.
- ⚠⚠ [**v4 store + 4.4.x mechanics**](tamheed-v4-and-liveness.md) — `status` → `lifecycle_status`; build
  payloads from the JSONL; `WVR-` operator-only; progress has a `correction` event; approving a lesson
  **refuses without `operator_confirm: true`**.
- ★★ **Requirement status measures whether anyone WROTE an AC, not whether it was built** — a requirement
  advances only via the AC auto-advance trigger. `DEF-012` is Won't-fix (`DEC-055`).
- ⚠⚠ **Stream scope had NEVER run on a real DB** (`DEF-066`) — see
  [[inmemory-provider-hides-db-refusals]]. `DEF-068`'s landmine: **a stream-scoped policy is RESOURCE-ONLY**.
- **Stale branches** (all pre-date `4c1b356`, so they carry `DEF-064`'s broken `ar.json`):
  `chore/design-update-round2`, `chore/docs-v8-local-design`, `feat/budget-notification-observer`,
  `feat/p13-webex-integration`, `docs/defer-p14-tarseem`, `feat/audit-adr`.

## Shipped, reference only (detail in the package)

- **ADR-0039 `AC-090`** (#239): per-request revalidation. ⚠ **An unknown subject must be ALLOWED** —
  ADR-0004 provisions JIT, so failing closed refuses every first login. Seam `IPrincipalRevalidator`.
- **`DEF-052`: there is NO read-side role gate** — every named policy is a WRITE capability. Fixed by
  `GuestSurfaceMiddleware`, deny-by-default. Guest-expiry sweep (#240) hourly; its predicate is
  `AccessExpiresAt != null && < now`, so an **invited** member (role `Guest`, null window) is not swept.

## Standing rules & gotchas (read before editing)

- [★ Read the implementation before calling it a defect](read-before-calling-it-a-defect.md) — **ten** instances, never caught by a gate. **Read the predicate, not the doc comment describing it**; read the guard, not the count of guards.
- [★ The InMemory provider hides DB refusals](inmemory-provider-hides-db-refusals.md) — `DEF-066`: stream assignment had **NEVER** worked on SQL Server under four green suites. Always ask "has this write ever run against SQL Server?" ⚠ Only `Acmp.Integration.Tests` is real SQL Server.
- [★ Controls must DETECT **and** TELL](controls-must-detect-and-tell.md) — **nine** instances; the "tell" half is normally the untested one.
- [★ Verify mechanically, not carefully](verify-mechanically-not-carefully.md) — `entity_upsert` replaces FULL rows; the JSONL flushes on EVERY write, so git HEAD is a live baseline. ⚠ **A measurement that indicts known-good code is measuring itself.** ⚠ PowerShell: always `--body-file` / `-F <file>`, never `-m` with backticks.
- ⚠ **PowerShell joins arrays with SPACES** — `[IO.File]::WriteAllText(path,$array)` writes one space-joined line and nearly **destroyed the SSM env file**. Join explicitly and verify the line count.
- ⚠ **`open_question.lifecycle_status` is a CHECK** over `Draft/Proposed/Approved/Rejected/Deferred/Implemented/Superseded/Obsolete` — "Resolved" rolls the whole batch back. `defect.fixed_by` is a **FK**; PR refs go in `custom_attributes`.
- ⚠ **Env one-offs:** the keycloak container's `docker exec` shell has no `KC_BOOTSTRAP_ADMIN_PASSWORD` (read `/run/secrets/kc_bootstrap_admin_password`); Windows `python3` cannot see Git Bash's `/tmp`.
- [⚠ Baselines are numbers, not properties](baselines-as-numbers-not-properties.md) — a count-based test on a shared topic can never discriminate.
- [⚠ Immutable history → cleanup is asymmetric](immutable-history-cleanup-asymmetry.md) — deleting a Keycloak user ORPHANS its member rows forever. **Disable, never delete.**
- [A static file cannot configure a live realm](a-static-file-cannot-configure-a-live-realm.md) — `realm-export.json` reaches **fresh stacks only**; `reconcile.sh` is the only seam to prod/UAT.
- [Write the handoff LAST](write-the-handoff-last.md) — it found `DEF-053`/`DEF-054` last time. Stamp superseded files with ⛔ immediately.
- [Commit package writes before git ops](commit-package-writes-before-git-ops.md) · [Tamheed stale .lock + PID reuse](tamheed-stale-lock-pid-reuse.md) · [Tamheed data repair](tamheed-data-repair.md) · [migration history](tamheed-migration-reverted.md)
- [Localhost CI hides load races](localhost-ci-hides-load-races.md) · [Git push hang → `gh auth setup-git`](git-push-hang-fix.md) · [Run CI gates locally pre-push](ci-gates-run-locally-pre-push.md) · [Always stage .claude/memory in commits](always-stage-claude-memory-in-commits.md)
- [Coverage & E2E mandate](coverage-and-e2e-mandate.md) — ≥95% FE+BE + adversarial E2E. ⚠ Playwright is **NOT UAT-only** (7 services + real Keycloak per PR) **but runs `KEYCLOAK_ADMIN_ENABLED=false`**, so it never touches the ADR-0038 write path.
- [E2E local run (non-destructive)](e2e-local-run-nondestructive.md) — **`-p acmpe2e` ONLY**, never `npm run e2e:up`. · [Dev-stack rebuild pitfall](dev-stack-rebuild-pitfall.md) — **never `up --build`** the long-lived dev stack.
- [Exact design fidelity + visual loop](exact-design-fidelity-visual-loop.md) · [A green suite is not a look](a-green-suite-is-not-a-look.md) — ⚠ the throwaway harness must import **only** the stylesheets the real route imports.
- [Design: breadcrumb spacing](breadcrumb-spacing-rule.md) · [i18n parity ≠ completeness](i18n-parity-not-completeness.md) · [Web visual-verify cache busting](web-visual-verify-cache-busting.md)
- ⚠ **`.adm-detail-card` has no padding and clips its children** — anything opening a popover needs `.adm-card-overflow`. · **`userEvent.setup()` installs its own clipboard stub** — define a clipboard spy *after* it.
- [User prefers simple English](user-prefers-simple-english.md) · [Phase prompt Standard Footer](phase-prompt-standard-footer.md) · [Install the schedule, not just the daemon](install-the-schedule-not-just-the-daemon.md) · [Arabic rename is a grammar rule](arabic-rename-grammar-not-substitution.md) · [A clean scan must prove it had a subject](scan-must-prove-it-had-a-subject.md) · [Guard the property, not the value](guard-the-property-not-the-value.md) · [The suite assumed a fresh database](e2e-assumes-a-fresh-database.md) · [The feature is often already half-built](check-before-building.md)
- ⚠ **AC id cells in markdown tables must stay BARE** (`| AC-001 |`, never bolded) — bolding breaks the Keystone G-PROGRESS gate.
- ⚠ **A new advisory can turn `main` red with no code change** — `GHSA-q939-rpr3-3284` (SSH.NET) blocked every merge mid-session. "It's only tests" is how a blocking gate becomes advisory.
- ⚠ **A compose `secrets:` entry whose FILE IS MISSING fails the WHOLE stack** — any mounted secret must be written **unconditionally** by `gen-secrets`.

## Completed ladder P1–P19 + PH-5 (reference only — do not re-open)

Topic files in this directory carry the detail: `ph5-sl025-uat-live`, `ph5-aws-deployment`,
`p19-release-readiness`, `p18-deployment`, `p17a-test-hygiene`, `next-p17-p18-p19`,
`audit-slice-literal-ac017`, `topic-prepare-ui-gap-d15`, `keystone-package-migration`,
`keystone-migration-gap-remediation`, and the `p6a-*`…`p16-*` ladder plans (superseded by the
package's slice rows).
