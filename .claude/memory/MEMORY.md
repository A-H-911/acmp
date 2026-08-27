# Memory Index — ACMP

> One line per entry; detail lives in topic files and the package. Read the linked file before acting.
> ⚠⚠ **MEASURED CEILING: 200 LINES** (not bytes — the old byte figure was disproven and the wrong
> dimension). Past 200 is dropped **silently**, and it had already eaten the "Standing rules" section.
> ⭐ **A limit disproven in one unit is not a limit disproven.** `wc -l` before adding; keep under ~140.


## ★★★ 2026-08-27 · `SL-033` `24.1`–`24.6` DONE · ▶▶ **`DW-080` PHASE A IS MID-FLIGHT ON A BRANCH**

⛔⛔ **YOU ARE PROBABLY NOT ON `main`.** `feat/dw-080-phase-a-net10`, PR **`#320`** open, **CI RED on ONE
thing** (`DEF-113`). E2E + Security + every backend TEST are GREEN — **`SearchProvidersFtsTests` passes,
so Arabic `FREETEXT` works on .NET 10.** ⚠ **Docker is DOWN**, which is the session's real constraint:
`Acmp.Integration.Tests` cannot run locally, so a local coverage report names MORE files than CI and none
of the extra list is real. **Ask the operator to start Docker.** ⚠ Check for an unpushed commit.
- ⭐⭐ **`.0` OF A MAJOR IS THE LEAST-TESTED BUILD OF THAT MAJOR** — I pinned every package to `10.0.0`
  with 11 patches out, **having already found `Cryptography.Xml` `10.0.0` itself vulnerable**, written
  that into `Directory.Build.props`, and left twenty others on `.0`.
- ⭐⭐ **A PINNED BASE IMAGE AND A PINNED SDK ARE ONE DECISION IN TWO PLACES** (`LL-019`, SDK band instead
  of distro). `global.json` pinned to the LOCAL SDK → the CONTAINER broke; `latestPatch` cannot cross a
  feature band. **The image is the authority, not the developer's box** — no local gate models it.
- ⭐⭐ **A MIGRATION'S VERDICT COMES FROM EXECUTING, NOT BUILDING** — built clean in Release + `format`
  green, then 355/392 API tests failed at RUNTIME, twice, for two unrelated causes.
- ⛔⛔ **PHASE B: ALPINE/DISTROLESS/CHISELED SHIP WITHOUT ICU** and work only in globalization-invariant
  mode, which **THROWS `CultureNotFoundException`** for any non-invariant culture since .NET 6. ACMP does
  `ar-SA` + `LCID 1025`. **A naive minimal-base move would THROW at runtime after compiling and
  unit-testing perfectly.** Escape hatches: `icu-libs`+`icu-data-full`, or the `-extra` variants.
  ⚠ Documentation, not measurement — the spike needs Docker. **`DEC-083` d2: bring a decision, not a pick.**

★★★ [**`SL-033` per-item findings**](sl033-slice-findings.md) — six items, **six different ways a row
misled**; the two bidi rules and why one does not transfer; the i18n formatter no-op; hollow passes; the
three-place `DbContext` registration; the CI/register traps. ⚠ **Live state is `prm-next.md`, not this file.**

- ⚠⚠⚠ **`24.6`: A ROW CAN BE AN ACCURATE QUOTATION OF A SUPERSEDED CLAUSE, AND NO REGISTER VIEW SEES IT** —
  three rows quoted `FR-154`'s *"Auditor and Administrator"*; `ADR-0027` excludes Administrator and names
  EXPORTING. Ids, statuses and `G-TRACE` all clean, so **both committed checkers ran straight over it.**
  ⭐⭐ **DISCRIMINATOR: an ADR that NAMES the rows it will amend — check that list against every row that
  quotes it.** `ADR-0027` named `FR-151`/`FR-153` (both have the edge); `FR-154` had neither.
- ⚠⚠ **READING `src` TELLS YOU WHAT EXISTS AND NOTHING ABOUT WHAT WAS SPECIFIED** (`24.5`: `SEC-080`/
  `SEC-103` named a Configuration table that did not exist; my code-only answer was an architectural
  divergence). **Sweep the narrative docs by keyword (`LL-008`) BEFORE sizing.** ⭐ `24.6` adds: **a
  CONTROL can decide architecture** — `C-AUDIT-08` forces a SERVER export (a client blob cannot audit
  itself), so the in-repo client-side-CSV precedent was the wrong answer.
- ⛔ **NEVER apply `PageSize.Clamp` to an export** — on a compliance artifact `DEF-104`'s habit becomes
  `DEF-103`'s silent truncation, indistinguishable from *"those rows do not exist"*.
- ⚠⚠ **`ReadAsStringAsync` STRIPS THE BOM** — assert BYTES. Same hollow-pass shape as `24.4`'s Arabic dual
  form, one item later. ⚠⚠ **A GREP OF THE E2E LOG FOR A TEST NAME IS BLIND** — zero for an EXISTING test
  too. **The count is the instrument** (88→90 = +2 for one test × 2 browsers); **re-measure the baseline**
  from the prior PR. ⭐ Scan a popover **with it OPEN** — a closed `Menu` renders only its trigger.
- ⚠⚠ **`DOC-011`'s `OQ-DATA-*` labels were INVISIBLE to the register** (zero `OQ-` rows vs a control of
  78) while **three `Met` verdicts leaned on them being open**. Now `OQ-079`/`OQ-080`. ⛔ **`SEC-080`
  asserts a legal hold overrides any future purge and NO HOLD MECHANISM EXISTS** — build Phase-2
  enforcement without it and that guarantee goes false **silently**.
- ⚠ **Approved ACs are IMMUTABLE, including against being marked superseded** — `AC-147`'s NULL
  `superseded_by` was ACCEPTED by the operator; do not "repair" it. ⚠ **`DW-087`:** `SEC-248`'s *"ACMP has
  no export feature"* is now false; **its trigger named a Phase-3 ITEM, not a property, so the row could
  not see its own condition met from another direction.**

- ⚠⚠ **THE RULE, NOT THE ROSTER: a row at `Review` is done-claimed work awaiting the operator's verdict,
  it is ALWAYS merged, and it is NEVER a reason to rebuild.** ⛔ **Which rows are at `Review` is not
  written here** — `readiness_check(scope="slice", id="SL-033")` is the only answer that cannot go stale,
  and a "nothing is owed" sentence is falsified by the next thing you finish. ⭐ **An ABSENT reason for a
  decline is not evidence of a reason: asking got `24.4` promoted, where every inference was wrong**
  (`LL-003`). ⭐ Both 2026-08-27 verdicts were taken against a GENERATED slate
  (`scripts/gen-slice-review-slate.mjs`), never a summary — `LL-011`/`LL-023` discharged mechanically.
- ⚠⚠⚠ **AFTER BUILDING ANYTHING, GREP `prm-next.md` FOR THE FILE NAMES AND REQUIREMENT IDS YOU TOUCHED**
  — not just the row you closed. One pre-handoff read found **7** stale statements the id pass ran clean
  over, incl. a **"do NOT rebuild" entry naming a file that had just been built**. ⭐ **Never write a
  lifecycle status inline in prose** — the register has it. ⭐ **Grep the ADVISORY NAME too**
  (`lessons-confirmed` found two stale instructions no id-based pass could see).
- ⚠⚠ **`LL-024` is Approved + PINNED** (2026-08-27) and it FIRED AGAIN ninety minutes later: a heredoc
  ate an escaped apostrophe and two unicode ranges. **Use the editor tool, not a heredoc — remove the
  second interpretation layer rather than escaping through it.** Then RUN the generated file.
- ⛔⛔ **A RED FROM `SearchProvidersFtsTests` IS REAL — STOP, DO NOT RE-RUN** (`DEC-077` d3). ⚠
  **`readiness_check` is `ready:FALSE` ON PURPOSE** (`DEF-108` Open/high, `DEC-077` d1) — **do NOT
  "fix" it by softening or converting; both were declined.**
- ⚠⚠ **`scripts/**` is NOT path-ignored** (`DEC-077` d2): a package-and-prose commit carrying one
  instrument runs the FULL pipeline. **`scripts/**` goes via PR, and poll CI to completion after ANY
  direct push to `main`.** ⛔ Never propose path-ignoring it — several `check-*.mjs` **are** the gates.
- ⚠⚠ **`DEF-109`: `Acmp.Api.Tests` ran 20m35s / 17 failed BETWEEN two normal runs**, all 100s
  `HttpClient` timeouts over TWELVE unrelated classes, backend tree byte-identical. ⛔ **The mitigation
  cannot be credited — the run BEFORE it was green too.** Append an occurrence; don't re-run into silence.
  ⚠ **`DEF-110`** (*record it, change nothing*): SLA thresholds are a hardcoded `switch` while
  `ASM-011`/`OQ-035` promise **configuration** — so `ASM-011` is not an overdue date, **its remediation
  path does not exist.** ⛔ Don't re-date it.
- ⚠⚠ **CI CAUGHT A VACUOUS TEST I HAD "FIXED" LOCALLY** — spying jsdom `localStorage` is version-dependent,
  so the injected fault never happened while the test asserted a spy count. **Replace the GLOBAL
  (`vi.stubGlobal`) and assert the OBSERVABLE.** ⭐ Tuning until green is not a fix.
★★★ [**`DW-082` / Dependabot arc**](dw082-sweep-and-vitest4.md) — vitest-4 / coverage-v8 findings.
⚠⚠ **NEVER lower `ADR-0016`'s 95% threshold**: v3 credited the line wrapping an *uninvoked* inline handler,
so four closed files with **no test file** scored ≥95%. ⚠ **`DEF-107`: approving+pinning a lesson does NOT
make it bind** — the note is rebuilt only by `handoff_emit`; run it in the SAME batch as any approval.
⚠ **Push package writes BETWEEN merge cycles, never during one** — every push to `main` re-stales every
open PR (`strict=true`). ⭐ **Instruments to USE, not re-derive:** `scripts/coverage-triage.mjs` ·
`gen-lesson-docket.mjs` · `count-prompt-ids.py` · `src/Acmp.Web/scripts/number-render-scan.mjs`.
⚠ `DEF-106`: declare `types: ["vite/client","node"]`; **a CHECKOUT DOES NOT CHANGE `node_modules`**.

## ★★ 2026-08-20 (later) · the disposition session — durable rules only

- ⚠⚠⚠ [**AN ID IS A POINTER, NOT A REFERENCE**](an-id-is-a-pointer-not-a-reference.md) — the operator
  **refused an interview** over it. `LL-011`, pinned. Anything they read to DECIDE carries each record's full
  text inline, generated from the JSONL. ⭐ The fix found **`DEF-082` does not exist** though 3 records cite it
  as real and fixed — `G-IDS` checks FKs, **not ids in prose**. Carried as `DEF-101`, not reconstructed.
- ⚠⚠ **A REQUIREMENT'S STATUS AND ITS `DW-` ROW'S STATUS ARE UNRELATED COLUMNS AND NOTHING COMPARES THEM** —
  activating a `DW-` row → check its requirement in the same breath. ⚠ **`deferred-work-reviewed` CANNOT go
  green from reviewing** (it selects `Open`+`Activated`+`Scheduled`); ⚠ **`assumptions-current`'s field is a
  FUTURE due date** — more will redden, that is the control working; don't clear them. ⛔ `DEF-087` untouched.
  ⚠ **`DEF-102`: `NFR-013` mandates a columnstore `ADR-0022` removed** (*record it, change nothing*), found
  by **keyword** sweep only (`LL-008`). ⚠ **I reported "four" truncated assumption titles; it was EIGHT** —
  **measuring inside the set you are already holding is not measuring the register.**
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
