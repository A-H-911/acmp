# Memory Index — ACMP

> One line per entry; detail lives in topic files and the package. Read the linked file before acting.
> ⚠⚠ **MEASURED CEILING: 200 LINES** (not bytes — the old byte figure was disproven and the wrong
> dimension). Past 200 is dropped **silently**, and it had already eaten the "Standing rules" section.
> ⭐ **A limit disproven in one unit is not a limit disproven.** `wc -l` before adding; keep under ~140.


## ★★★ 2026-08-29 · `SL-033` AND `SL-034` BOTH DONE · **NOTHING IS SCHEDULED ON THE BUILD LADDER**

⚠ Live state is `prm-next.md`'s numbered list, never this file. `readiness_check("package")` is
`ready:FALSE` on **`DEF-108` alone**, by `DEC-077` d1 — that is the intended state, not a fault.
⛔ **`SL-033` is deliberately NOT closed** (`DEC-088`) — `DEF-108` holds it; there is nothing left to build
in it. **`SL-034` IS closed** (`DEC-093`), no waiver, no force. Next actions are operator acts: disposition
the rows this slice filed (`DW-088/090/091/092/093`), and `release-close-out.md`, never run.

★★★ [**`SL-034` — the slate generator's 3 refusals + the ASVS pack**](sl034-slate-generator-and-asvs-pack.md)
— read it before touching `gen-slice-review-slate.mjs`, the ASVS pack, or any test that reads the package.

- ⭐⭐⭐ **`LL-032` (Approved + pinned): A TEST WHOSE FIXTURE IS THE LIVE REGISTER CHANGES MEANING WHEN
  SOMEBODY DOES ORDINARY WORK — AND THE DANGEROUS OUTCOME IS THE *PASS*.** Promoting a row deleted a
  calibration's subject; it failed loudly **only by luck**. ⭐ Stage the whole selection; ask *what would a
  normal day's work have to change for this test to stop testing what it names?*
- ⭐⭐ **THE REGRESSION CASE WRITTEN AS A CONTROL FOUND A LIVE DEFECT** (`DEF-119`) — `DEF-116` read `Fixed`
  while one of the two rows it names still aborted. **Multi-criterion ≠ multi-requirement.**
- ⛔⛔ **NEVER carry forward `security-controls.md` §20's *"L2 is met across all applicable chapters"*** —
  the self-assertion `DW-079` forbids. ⭐ **ASVS levels are CUMULATIVE: "L2" = 253 reqs at L1+L2**, not 183.
- ⚠⚠ **`jq` IS NOT INSTALLED** and I built a monitor on it anyway after reading that warning. It emits
  **nothing** — silence reads as "still running". Use `gh --jq`. ⚠ Pin the sha in a CI poller; one that
  re-reads `HEAD` prints `0 running / 0 runs` once you commit, which looks like success.
- ⚠ **Approving a lesson needs `"operator_confirm": true`** — plus byte-identity and `confirmed_by`.

★★★ [**`SL-033` per-item findings**](sl033-slice-findings.md) · [**`DW-082` / Dependabot arc**](dw082-sweep-and-vitest4.md)

- ⭐⭐ **THE HABIT THAT PAID ON ALL TEN ITEMS:** read the row's own text, then sweep the NARRATIVE docs
  **and** the ADR/decision/OQ registers **by keyword** before sizing (`LL-008`, `LL-025`). It has now
  mis-sized in ten different directions, including *the row was accurate and the trap was in the code*.
- ⚠⚠ **A DEFENCE LAYER CAN BE INVISIBLE TO ANY FRONT-DOOR TEST** (`LL-030`) — adding `Guest` to a preview
  query's `AllowedRoles` left all 10 API tests GREEN, because the path gate intercepts guests first.
  ⭐ **Pin each refusal to a DISTINGUISHABLE SIGNATURE**; three tests asserting `403` test whichever layer
  runs first. ⭐ **When an item asks you to add the thing whose ABSENCE is the guarantee, ISOLATE it.**
- ⚠⚠ **A REFACTOR CAN PUSH A FILE UNDER THE PER-FILE COVERAGE FLOOR WITH NO NEW UNTESTED LINE** (`LL-031`)
  — the numerator never moved; the denominator did. ⛔ **RUN THE GATE, NOT THE TESTS**: `dotnet test` per
  project passed while `check-coverage.mjs` failed on 4 files.
- ⛔ **NEVER lower `ADR-0016`'s 95%** — v3 credited lines wrapping *uninvoked* inline handlers, so files
  with **no test file** scored ≥95%.
- ⚠ **`DEF-107`: approving+pinning a lesson does NOT make it bind** — run `handoff_emit` in the SAME batch.
- ⚠ **Push package writes BETWEEN merge cycles** — every push to `main` re-stales every open PR.
- ⚠ **`AcmpWebApplicationFactory.WithIdentityProvider()` is opt-in** — without it the guest-invite
  path is unreachable and the API answers look like feature bugs.
- ⚠ **`DW-088`: `TopicDetail`'s download button is hardcoded `disabled`** — no principal but a guest
  presenter can open a topic attachment anywhere in the product.
- ⭐ **Instruments to USE, not re-derive:** `coverage-triage.mjs` · `gen-lesson-docket.mjs` ·
  `gen-slice-review-slate.mjs` · `test-gen-slice-review-slate.py` · `check-asvs-pack-paths.mjs` ·
  `count-prompt-ids.py` · `number-render-scan.mjs`.
- ⛔⛔ **A RED FROM `SearchProvidersFtsTests` IS REAL — STOP, DO NOT RE-RUN** (`DEC-077` d3).
  ⚠⚠ **`scripts/**` is NOT path-ignored** (`DEC-077` d2) — PR route, and **poll CI to completion after ANY
  direct push to `main`**. ⛔ Never propose path-ignoring it; several `check-*.mjs` **are** the gates.
- ⚠ **`DEF-109`**: `Acmp.Api.Tests` ran 20m35s / 17 failed between two green runs. Append an occurrence;
  don't re-run into silence. ⛔ **`SEC-080` asserts a legal hold overrides any purge and NO HOLD MECHANISM
  EXISTS** (`OQ-080`) — answer it BEFORE building Phase 2 retention enforcement.
- ⚠ **Approved ACs are IMMUTABLE, including against being marked superseded** — `AC-147`'s NULL
  `superseded_by` was ACCEPTED; do not "repair" it. ⛔ **Never `PageSize.Clamp` an export** — silent
  truncation on a compliance artifact.

## ★★ 2026-08-20 · the disposition session — durable rules only

- ⚠⚠⚠ [**AN ID IS A POINTER, NOT A REFERENCE**](an-id-is-a-pointer-not-a-reference.md) — the operator
  **refused an interview** over it. `LL-011`, pinned. Anything they read to DECIDE carries each record's
  full text inline, generated from the JSONL. ⭐ `G-IDS` checks FKs, **not ids in prose** (`DEF-101`).
- ⚠⚠ **A REQUIREMENT'S STATUS AND ITS `DW-` ROW'S STATUS ARE UNRELATED COLUMNS AND NOTHING COMPARES**
  them — activating a `DW-` row → check its requirement in the same breath. ⚠ `assumptions-current`'s
  field is a FUTURE due date; more will redden and that is the control working. ⛔ `DEF-087` untouched.
- ⚠ **I reported "four" truncated assumption titles; it was EIGHT** — measuring inside the set you are
  already holding is not measuring the register.
★★ [**Durable rules from batches 13–21**](batches-13-21-durable-rules.md) — `Met`-verdict scope, the
enforcing-mechanism trap, never leave a Pending AC, Hangfire process-globals, union coverage, `$?` after
a pipe, and production's reconciled state.

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

## ⚠ Topic files this index does NOT link — an unlinked file is invisible to recall

The ladder files below are covered by the blanket note in the last section. **These eleven are not, and
nothing points at them**: `absence-claims-need-untruncated-search` · `ask-every-time-never-bank-answers` ·
`audit-slice-literal-ac017` · `body-assertions-miss-the-envelope` · `package-mechanics-proven-2026-08-18` ·
`reconciliation-and-voting-eligibility` · `substring-checks-bind-to-prose` · `topic-prepare-ui-gap-d15` ·
`wbs233-csp-spike` · `wbs234-reclassify` · `webex-coverage-gate-async-exclusion`.
⚠ **Their current value is NOT assessed here** — this line exists so they are findable, not to vouch for
them. Found 2026-08-26 by checking every topic file for an inbound link; do that after any compaction.

## Completed ladder P1–P19 + PH-5 (reference only — do not re-open)

Detail lives in this directory's topic files (`ph5-*`, `p17a-*`, `p18-*`, `p19-*`, `keystone-*`, the `p6a-*`…`p16-*` ladder plans) — all superseded by the package's slice rows. `ls` the directory when you need one.
