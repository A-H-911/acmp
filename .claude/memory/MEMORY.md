# Memory Index — ACMP

> Compacted 2026-08-21 (10th). One line per memory; detail lives in topic files and the package.
> Read the linked file before acting. ⚠ **The old "keep under ~17KB or the tail is SILENTLY dropped"
> note was an UNMEASURED number and it is false at that size** — on 2026-08-21 this file was **18,668
> bytes and loaded IN FULL** (its last line was verified byte-identical to the loaded copy's). Where the
> real ceiling is, is still unknown, so keep the index lean **because a long index buries its own top**,
> not because of a threshold nobody measured. Do not restore a number you have not observed.

## ★★★ 2026-08-21 · **BUILDING SL-032** · `gate` 7/7 · `ready:TRUE` (measure them, never quote these)

> ▶▶ **THE `WBS-23.3` SPIKE IS DONE — the next action is the OPERATOR's install decision**, not a spike
> and not a build. `prm-next.md` §6, first block. ⚠ Read the live `LL-014` status and any `DEC-` after
> `DEC-068` before assuming it is still open.

- ★★ [**`WBS-23.3` spike: the technique passed, the package failed**](wbs233-csp-spike.md) — DOM→PNG is
  **clean under `style-src 'self'`** (the STOP-and-ask condition never arose; header untouched), but
  **`modern-screenshot` 4.7.0 — the pre-selected pick — throws on every card**, and its `font: false`
  escape **silently breaks the Arabic layout**. `html-to-image` 1.11.13 works, fonts embedded, zero
  violations. **Read the file before proposing anything here.**
- ⚠⚠ **`LL-014` (Proposed): REGISTRY METADATA CANNOT RANK CORRECTNESS.** Size/deps/downloads/freshness
  rank packages that all work. Every number in that table was right and the ranking still **inverted**
  against the one axis that decided anything. **"The technique works here" never transfers to "this
  package works here."** ⭐ Two libs, same technique, opposite outcome — one unguarded property read.
- ✅ **`SL-032`: `WBS-23.1` (`DW-061` mobile notice, #301→`78b5ca2`, `AC-140`) and `23.2` (`DW-040` drag,
  #302→`145d9bf`, `AC-141`) MERGED.** ⭐⭐ **`DEF-087`'s fix-forward rule VISIBLY WORKS:** slice `wbs-done`
  went **5→4→3** where it returns **zero rows for all 28 closed slices**. Keep new wbs rows carrying `slice_id`.
- ⚠⚠ **APPROVED LESSONS ARE IMMUTABLE** — *"supersede, never edit"*; a merge is a NEW row superseding the
  old (`LL-013` supersedes `LL-007`+`LL-012`). ⚠ A mutation check has TWO subjects — **a passing mutant
  proves nothing**; prove the DETECTOR exists (test count moved 10→13) BEFORE mutating. **Count the binding
  lessons; never quote a number.**
- ⚠⚠ **THREE `DW-` ROWS IN THIS SLICE HAD WRONG SIZING**, each saying "only the gesture" / "blocked on
  nothing": `DW-040` needed a backend change (the op was a **SWAP**) *and* a new addressing mode (the kanban
  renders the filtered/sorted/truncated backlog, so it must send the **TARGET'S IDENTITY**, not a position —
  a test exists to fail if anyone "simplifies" that back to a delta), and `DW-038` was blocked on a real
  thing with no canvas anywhere. ⚠ **`DEC-068` d1's RATIONALE repeats `DW-038`'s wrong sizing verbatim** —
  wrong prose inside an Approved decision; its outcome stands, left unedited, recorded in `PE-578`.
  **READ THE CODE BEFORE BELIEVING A ROW'S SIZING.**
- ⚠ **`PH-3` stays `Approved` ON PURPOSE** — `WBS-20.4` is the email adapter vs a hard constraint
  (`DEC-055`). Do **not** "repair" it; that is the manufactured-status move `DEF-010` records. `SL-014`
  `Deferred` (`DEC-028`) and off the ladder.
- ⚠ **`DEF-103` Fixed** (kanban rendered a silent 25-row prefix → `KANBAN_PAGE_SIZE=500` + actionable notice).
  ⚠ **`DEF-104` Open**: **12** paged reads uncapped, **2** capped — the remedy already exists in-repo
  (`Math.Clamp` to `MaxPageSize`; `Math.Min(take, MaxTakePerType)`). ⭐ **My first sweep keyed on `PageSize`
  and returned 10 — blind to the audit endpoint and `GetDecisions` (`size`, `n`).** Sweeping every `.Take(...)`
  found them. **A sweep keyed on one identifier silently narrows; the denominator is part of the finding.**
- ⚠⚠ **COMMITTING TO `main` IS NOT PUBLISHING TO `main`** — 10 unpushed package commits got folded into one
  feature squash. **Push after package commits**; `git rev-list --left-right --count HEAD...origin/main`.
  ⚠ **Trap 25 fired again** (merged remotely, aborted locally) — verify by CONTENT, back up `data/` first.
- ⚠⚠ **ARABIC MORPHOLOGY BITES TEST ASSERTIONS** — a literal substring failed against a string that
  *visibly contains it* (`لـ` absorbs the alef of `ال`). **Assert the SCRIPT RANGE, never a fragment.**
- ⚠ **A failure message names a symptom; WHERE it fires is the evidence.** A red e2e looked like "Playwright
  can't drive native HTML5 drag" (a real limitation) — it was a shared helper's hard-coded `toHaveCount(2)`.
  The drag had worked. Nearly cost a rewrite of working code.

## ★★ 2026-08-20 (later) · the disposition session — durable rules only

- ⚠⚠⚠ [**AN ID IS A POINTER, NOT A REFERENCE**](an-id-is-a-pointer-not-a-reference.md) — the operator
  **refused an interview** over it. `LL-011`, pinned. Anything they read to DECIDE carries each record's full
  text inline, generated from the JSONL. ⭐ The fix found **`DEF-082` does not exist** though 3 records cite it
  as real and fixed — `G-IDS` checks FKs, **not ids in prose**. Carried as `DEF-101`, not reconstructed.
- ⚠⚠ **ALL TWELVE demand-triggered `DW-` rows were `Activated`** (`DEC-067`/`SC-029`) — **against my
  recommendation**, recorded as an override. v1 is materially larger.
- ⚠⚠ **A REQUIREMENT'S STATUS AND ITS `DW-` ROW'S STATUS ARE UNRELATED COLUMNS AND NOTHING COMPARES THEM.**
  `DEC-064` d2 said *"DW-037 is ACTIVATED"* and never reached the row, while `SC-020` had it `Deferred`.
  **Activating a `DW-` row → check its requirement in the same breath.**
- ✅ **`assumptions-current` FIXED — it FAILS now** (`ASM-011`, overdue on purpose). The field is a **FUTURE
  due date**; more will redden — **that is the control working, don't clear them.** ⛔ `DEF-087` untouched.
  ⚠ **`deferred-work-reviewed` CANNOT go green from reviewing** — it selects `Open`+`Activated`+`Scheduled`.
- ⚠ **`DEF-102`: `NFR-013` mandates a columnstore `ADR-0022` removed**; `DEC-020`/`ADR-0003`/`OQ-040` still
  assume it. Operator: *record it, change nothing.* Found by **keyword** sweep only (`LL-008`).
- ⚠ **I reported "four" truncated assumption titles; it was EIGHT.** I measured inside the twelve rows I was
  already editing. **Measuring inside the set you are holding is not measuring the register.**

## ★ 2026-08-20 · the DW-029 programme closed — durable rules only

- ⚠⚠ **A MENTION IS NOT A COVERAGE** — matching a requirement "named anywhere in a DW row" made one
  vanish from a worklist. **A well-cross-referenced register would report itself finished.** Match on TITLE.
- ⚠⚠ **`DEF-099`: OTLP traces had NEVER reached Seq in ANY environment.** ⭐ **Reusable discriminator:**
  events grew 679→739 while DB spans sat at **exactly 72** — same host/port/container. **Verify
  observability by sending traffic and asserting spans MOVE, never by a clean boot.**
- ⚠ **COUNT THE REQUIREMENT'S CLAUSES BEFORE YOU COUNT YOUR FINDINGS** — twice, strong evidence covered
  two-thirds of a three-part requirement and nearly carried a `Met`.
- ⚠ **The operator declined to relax a requirement TWICE.** The register states the **TARGET**, not the
  status quo — never offer a narrowing as the easy path.
- ⚠⚠ **A number entering a durable artifact must come from output visible in the same breath.**
- ⚠ **`entity_query("requirement", ...)` OVERFLOWS the token limit**; count from the JSONL.
- ⚠ **Running a stack: `docker ps` empty does NOT mean safe** — 5 populated volumes, `dev-up.sh` is
  `up -d --build`. Full recipe in `prm-next.md` §6 "HOW TO RUN A STACK HERE" — follow it exactly.
- ⚠ **`entity_upsert` NOT NULL on UPDATE:** `defects.title/severity`, `scope_changes.decision_ref/
  description/iteration`, `slices.title/objective/phase_id`, `phases.title`. Nullable preserved by omission.
  ⚠ **CHECKs:** `verified_by IN (human|agent|ci)`, `verification_method IN (auto-test|manual|inspection)`,
  `progress.event_type` is a fixed set (no `decision-made`). ⚠ **Approved lessons are IMMUTABLE.**

## ★ batches 13–15 — durable rules only (fuller record in `prm-next.md`)

- ⭐⭐ **Surface that DOES NOT EXIST can be excluded BY NAME in a `Met` verdict; surface that EXISTS but is
  unproven CANNOT.** That line decided every borderline call.
- ⚠⚠ **A COUNT OF THE ENFORCING MECHANISM IS NOT A MEASURE OF THE PROPERTY** (`LL-006`) — an `NFR-021` census
  read as a 38-command validation hole; all four commands carrying scalar input are guarded in the domain.
- ⚠ **Never leave a Pending/Partial AC** — `acs-met` counts by `retired_in` and ignores `lifecycle_status`, so
  an AC ahead of its evidence holds readiness false **forever**. **A part-verified requirement gets a `DW-` row.**
- ⚠ **`tsc --noEmit -p tsconfig.json` in `src/Acmp.Web` EXITS 0 OVER ZERO FILES** (`DEF-091`); `vitest` does
  NOT typecheck — use `npm run build`. ⚠ **`DEF-096`: `NFR-054`'s 500 MB cap is UNSATISFIABLE** (fts 3.62 GB);
  operator REJECTED relaxing minimal-base → `DW-066`. **Do not change a base image.**
- ⚠⚠ **Hangfire's `JobStorage.Current` + `GlobalJobFilters` are PROCESS-GLOBAL**; it never hands a filter the
  job's own exception — record `InnerException`.
- ⚠ **Coverage must be UNIONED, never summed.** ⚠ **Never push to a branch with CI in flight.**
  ⚠ `Timeline.tsx` is an honest SHELL; `Calendar.tsx`'s work is `Activated` (`DW-037`).
- ⭐ [**Store mechanics proven by experiment**](package-mechanics-proven-2026-08-18.md) — `G-TRACE` needs 3 legs.
- **Still open, needing a stack or scanner:** `NFR-018` DAST+pentest, `NFR-019` TLS scan, `NFR-052`, the ops
  group (`NFR-015 017 044 052 062`, `PE-485`), and much of `DW-043`…`DW-060` measured from trace data.
- ⚠⚠ **`$?` AFTER A PIPE IS THE PIPE'S LAST COMMAND** — redirect to a file, read `$?` on the bare command.
- **PRODUCTION IS DEPLOYED AND RECONCILED**; `/readyz` 200 on all four. `RISK-007` clock started 2026-08-17.

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
- **6 stale branches still exist** (verified 2026-08-21) — `chore/design-update-round2`,
  `chore/docs-v8-local-design`, `docs/defer-p14-tarseem`, `feat/audit-adr`,
  `feat/budget-notification-observer`, `feat/p13-webex-integration`. All pre-date `4c1b356`, so **all
  carry `DEF-064`'s broken `ar.json`**. ⚠ Several merged `feat/`-`fix/` branches also linger on `origin`
  against the "delete branch" half of the branching rule.

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
