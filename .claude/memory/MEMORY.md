# Memory Index — ACMP

> One line per entry; detail lives in topic files and the package. Read the linked file before acting.
> ⚠⚠ **MEASURED CEILING: 200 LINES** (not bytes — the old byte figure was disproven and the wrong
> dimension). Past 200 is dropped **silently**, and it had already eaten the "Standing rules" section.
> ⭐ **A limit disproven in one unit is not a limit disproven.** `wc -l` before adding; keep under ~140.


## ★★★ 2026-08-26 · **`SL-033`: `WBS-24.1`–`24.4` DONE** — ▶▶ NEXT: **`WBS-24.5`** (`DW-036`/`FR-155`)

★★★ [**`SL-033` per-item findings**](sl033-slice-findings.md) — four items, **four different ways a row
misled**; the bidi/`unicode-bidi: plaintext` rule; the i18n formatter no-op; two hollow passes; the
`number-render-scan` warnings. ⚠ **Live state is `prm-next.md`, not this file.**

- ⚠⚠ **3 OPERATOR VERDICTS OWED** (`WBS-24.2`/`24.3`/`24.4` sit at `Review`; `Implemented` is yours,
  `DEC-079` d3) **+ the `LL-024` interview.** ⚠ **Do NOT rebuild any of them** — `Review` counting as
  open in `readiness_check` is the rule working.
- ⚠⚠⚠ **AFTER BUILDING ANYTHING, GREP `prm-next.md` FOR THE FILE NAMES AND REQUIREMENT IDS YOU TOUCHED**
  — not just the row you closed. One pre-handoff read found **7** stale statements the id pass ran clean
  over, incl. a **"do NOT rebuild" entry naming a file that had just been built**. ⭐ **Never write a
  lifecycle status inline in prose** — the register has it.
- ⚠⚠ **`LL-024` (Proposed): GENERATED CODE LOSES ITS ESCAPES SILENTLY AND STILL LOOKS PLAUSIBLE.** Fired
  again 2026-08-26: a bash heredoc ate one backslash level out of a scanner regex. **Build escapes with
  `chr(92)`, then EXTRACT AND RUN the generated block** — or use the Write tool, not a heredoc.
- ⛔⛔ **A RED FROM `SearchProvidersFtsTests` IS REAL — STOP, DO NOT RE-RUN** (`DEC-077` d3). ⚠
  **`readiness_check` is `ready:FALSE` ON PURPOSE** (`DEF-108` Open/high, `DEC-077` d1) — **do NOT
  "fix" it by softening or converting; both were declined.**
- ⚠⚠ **`scripts/**` is NOT path-ignored** (`DEC-077` d2): a package-and-prose commit carrying one
  instrument runs the FULL pipeline. **`scripts/**` goes via PR, and poll CI to completion after ANY
  direct push to `main`.** ⛔ Never propose path-ignoring it — several `check-*.mjs` **are** the gates.
- ⚠⚠ **`DEF-109`: `Acmp.Api.Tests` ran 20m35s / 17 failed BETWEEN two normal runs**, all 100s
  `HttpClient` timeouts over TWELVE unrelated classes, backend tree byte-identical. ⛔ **The mitigation
  cannot be credited — the run BEFORE it was green too.** Append an occurrence; don't re-run into silence.
- ⚠ **`DEF-110`** (`DEC-079` d2, *record it, change nothing*): the SLA thresholds are a hardcoded
  `switch` while `ASM-011`/`OQ-035` promise **configuration**. **So `assumptions-current` naming
  `ASM-011` is not an overdue date — its remediation path does not exist.** ⛔ Don't re-date it.
- ⚠⚠ **CI CAUGHT A VACUOUS TEST I HAD "FIXED" LOCALLY.** Spying jsdom `localStorage` is
  version-dependent — the injected fault never happened while the test asserted a spy call count.
  **Replace the GLOBAL (`vi.stubGlobal`) and assert the OBSERVABLE.** ⭐ Tuning until green is not a fix.
- ⭐ **Instruments to USE, not re-derive:** `scripts/coverage-triage.mjs` · `scripts/gen-lesson-docket.mjs`
  · `scripts/count-prompt-ids.py` · `src/Acmp.Web/scripts/number-render-scan.mjs`. ⚠ `DEF-106`: declare
  `types: ["vite/client","node"]`; **a CHECKOUT DOES NOT CHANGE `node_modules`**.

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

## ★ Batches 13–21 + the DW-029 close-out

★★ [**Durable rules from batches 13–21**](batches-13-21-durable-rules.md) — `Met`-verdict scope, the
enforcing-mechanism trap, never leave a Pending AC, Hangfire process-globals, union coverage, `$?` after
a pipe, the still-open stack/scanner group, and production's reconciled state.

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
