# Memory Index — ACMP

> Compacted 2026-08-25 (11th). One line per memory; detail lives in topic files and the package.
> Read the linked file before acting.
> ⚠⚠ **THE CEILING IS NOW MEASURED AND IT IS A LINE COUNT, NOT A BYTE COUNT: 200 LINES.** On 2026-08-25
> the tooling refused a write at **275 lines**, stating that everything past 200 is silently dropped on
> load — so the **"Standing rules & gotchas"** pointers at the bottom, the most durable content here, had
> **already been invisible**. The earlier note was right that the *byte* figure was unmeasured (18,668
> bytes loaded in full on 2026-08-21) and wrong to conclude the ceiling was unknown — **it was measuring
> the wrong dimension.** ⭐ **A limit you have disproven in one unit is not a limit you have disproven.**
> Keep this under ~140 lines: one line per entry, detail in topic files.


## ★★★ 2026-08-25 · **`DW-082` IS THE LIVE WORK** — branch `chore/vitest-4-pair`, PR `#307`

★★★ [**The `DW-082` / Dependabot-sweep arc**](dw082-sweep-and-vitest4.md) — read this before touching
the vitest-4 branch, the coverage gate, or the open PR queue. Everything below is the short form.

- ⚠⚠⚠ **NEVER WRITE THE REMAINING-FILE COUNT ANYWHERE — MEASURE IT.** `npm run test:cov --
  --coverage.reporter=json --coverage.reporter=json-summary --coverage.reporter=text`, then
  `node scripts/coverage-triage.mjs` (committed 2026-08-25). It prints each uncovered line's **source
  text**, so a cause is confirmed rather than assumed, and refuses to report unless 3 calibrations pass.
- ⚠⚠ **NEVER lower `ADR-0016`'s 95% threshold.** `coverage-v8` v4 is the HONEST counter: v3 credited the
  line wrapping an *uninvoked* inline handler, so the gate could not see untested handlers at all.
  Four closed files **had no test file whatsoever** and v3 scored them ≥95%.
- ⚠⚠⚠ **"`TopBar` IS NOT A HANDLER FIX / needs its own decision" WAS FALSE AND IS WITHDRAWN** (`PE-614`).
  All seven of its uncovered lines are inline handlers. ⭐ **It came from version one of the triage script
  misreading an ASCII table's compressed `92-138` as a 47-line range; version two fixed the NUMBER and
  nobody re-derived the CAUSE built on the wrong one. A corrected measurement does not correct the
  inference someone built on it.**
- ⚠⚠ **`DEF-106`: the local `npm run build` reports 16 TS errors on `main` while CI compiles it cleanly** —
  so trap 22b's named arbiter is unusable here. Don't "fix" code on its say-so; CI is the gate.
- ⚠⚠ **DO PACKAGE WRITES ON `main`** (C31 — a feature checkout rolls `data/` backwards), **and PUSH after
  each one.** Every push to `main` re-stales every open PR (`strict=true`), so push between merge cycles,
  never during one. ⚠ `main` IS branch-protected: 9 checks, `enforce_admins=false`.
- ⚠ **`LL-017/018/019` are `Proposed`** — `lessons-confirmed` FAILS on purpose; never approve them unread.
- ⚠ **If `npm ci` fails `EPERM`/`EBUSY` here, enumerate node processes FIRST** (`DW-083`: six `vite` servers
  ran 13 days holding a native binary). ⚠ **`ls node_modules | wc -l` is NOT npm's package count** — 125
  directories vs "169 added" is apples to oranges, and reading it as a gutted tree cost a detour.
- ⚠⚠ **`prm-next.md` has carried a stale statement TWENTY times.** Two more this session: §6 and `PE-612`
  disagreed 206-vs-217 on one measurement (`PE-613` — the fix was to **commit the instrument**,
  `scripts/count-prompt-ids.py`), and the `TopBar` instruction above. **Read its prose; the id-and-status
  pass is the easy half.** ⚠ Its §2–§5 were declared UNREAD by the prior session — read them.
## ★★ 2026-08-20 (later) · the disposition session — durable rules only

- ⚠⚠⚠ [**AN ID IS A POINTER, NOT A REFERENCE**](an-id-is-a-pointer-not-a-reference.md) — the operator
  **refused an interview** over it. `LL-011`, pinned. Anything they read to DECIDE carries each record's full
  text inline, generated from the JSONL. ⭐ The fix found **`DEF-082` does not exist** though 3 records cite it
  as real and fixed — `G-IDS` checks FKs, **not ids in prose**. Carried as `DEF-101`, not reconstructed.
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
  vanish from a worklist. Match on TITLE.
- ⚠⚠ **`DEF-099`: OTLP traces had NEVER reached Seq in ANY environment.** ⭐ **Reusable discriminator:**
  events grew 679→739 while DB spans sat at **exactly 72** — same host/port/container. **Verify
  observability by sending traffic and asserting spans MOVE, never by a clean boot.**
- ⚠ **COUNT THE REQUIREMENT'S CLAUSES BEFORE YOU COUNT YOUR FINDINGS** — twice, strong evidence covered
  two-thirds of a three-part requirement and nearly carried a `Met`.
- ⚠ **The operator declined to relax a requirement TWICE.** The register states the **TARGET**, not the
  status quo — never offer a narrowing as the easy path.
- ⚠⚠ **A number entering a durable artifact must come from output visible in the same breath.**
- ⚠ **`entity_query("requirement", ...)` OVERFLOWS the token limit**; count from the JSONL.
- ⚠ **Running a stack: `docker ps` empty does NOT mean safe** — 5 populated volumes; recipe in
  `prm-next.md` §6 "HOW TO RUN A STACK HERE".
- ⚠⚠ **`entity_upsert` REPLACES FULL ROWS** — NOT NULL on UPDATE: `defects.title/severity`,
  `scope_changes.decision_ref/description/iteration`, `slices.title/objective/phase_id`, `phases.title`;
  nullable preserved by omission. **Before writing that you PRESERVED something, check the tool can.**
  A `SL-032` objective claimed to leave text "in place" in the very write that deleted it (`PE-585`).
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
- **6 stale branches still exist** (verified 2026-08-21, `git branch -a`), all pre-dating `4c1b356` so
  **all carry `DEF-064`'s broken `ar.json`**. ⚠ Merged `feat/`/`fix/` branches also linger on `origin`
  against the "delete branch" half of the branching rule.

## Shipped, reference only (detail in the package)

- **ADR-0039 `AC-090`** (#239) per-request revalidation — ⚠ **an unknown subject must be ALLOWED** (ADR-0004 provisions JIT, so failing closed refuses every first login).
- **`DEF-052`: there is NO read-side role gate** — every named policy is a WRITE capability; fixed by `GuestSurfaceMiddleware`, deny-by-default. ⚠ The hourly guest-expiry sweep skips an **invited** member (role `Guest`, null window).

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

Detail lives in this directory's topic files (`ph5-*`, `p17a-*`, `p18-*`, `p19-*`, `keystone-*`, the `p6a-*`…`p16-*` ladder plans) — all superseded by the package's slice rows. `ls` the directory when you need one.
