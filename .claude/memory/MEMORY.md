# Memory Index — ACMP

> One line per entry; detail lives in topic files and the package. Read the linked file before acting.
> ⚠⚠ **MEASURED CEILING: 200 LINES** (not bytes — disproven, wrong dimension). Past 200 drops **silently**
> and had already eaten "Standing rules". ⭐ **`wc -l` BEFORE *and AFTER* every edit** — 2026-09-03 went
> 199→201→202 *while trimming*: replacing 2 lines with 4 is an ADD. Keep under ~140.


## ★★★ 2026-09-02 · `DEF-109` diagnosed · `DEF-121`: memory pressure REFUTED, clause (2) still unmet · `DEC-111`–`DEC-116`

⛔⛔ **NEVER WRITE A CI COLOUR INTO DURABLE PROSE** — this heading said *`main` still red* and my own session
falsified it hours later. `gh run list --branch main` is the answer; cite a RUN ID (`LL-036`).
★★★ [**`DEF-109`: the HOST is the unit that leaks**](def109-the-host-is-the-unit-that-leaks.md) — **read
before any memory/perf investigation, and before trusting any `gcroot` output.** 137 MB over 20
`WebApplicationFactory` hosts vs 8 MB over 1 for identical work; 20/20 disposed factories alive after a
forced GC; 3% store fix declined on `LL-035`. ⭐ `DEC-120` ACTIVATED `DW-096` → `WBS-27.2` in new `SL-036`
(**overrode carry**); may NOT claim to fix it. ⛔ Clause (2) is **UNREACHABLE** — the capture hooks the
OTHER family's `ContainerNotRunningException` — so `WBS-27.1` builds the missing instrument.
★★★ [**PERMISSION PROMPTS: FOUR CAUSES, ONLY TWO CONFIG**](permission-prompts-four-causes.md) — ⛔ *shape*
  was ONE of them; that half-answer cost **five complaints**. `Write()` rules are DEAD (only `Edit()`);
  pass Write/Edit a **REPO-RELATIVE** path; a PIPE is a compound like `&&`; `node -e` is an
  allowlist-escape → script FILE. ⚠ You cannot SEE prompts — ask which tool. ⛔ `rm`/push/`Edit(.claude/**)`
  prompt BY DESIGN; the classifier blocks self-widening. ⭐ Scratch `.scratch/<id>/`; memory = junction.
★★★ [**READ THE ARTEFACT, NOT THE ENTRY ABOUT IT**](read-the-artefact-not-the-entry-about-it.md) —
  the memory-pressure hypothesis is **REFUTED** from two files the capture KEPT; `DW-097`'s *"it is in
  the dropped dump"* was FALSE; clause (2) is STILL unmet (an elimination is not an identification).
  ⛔⛔ **Also: `strict:true` means ANY push to `main` leaves every open PR unmergeable** — path-ignore
  stops the CI RUN, not the staleness. Read it before touching CI, an artefact, or an open PR.
- ⛔⛔ **THE HOSTED RUNNER IS ~16 GB, NOT 7** (`Total Memory: 16766414848`, and `DEF-121`'s row had it all
  along). I asserted 7 from memory into `PE-771`. **Every MEASURED `DEF-109` claim survives; the causal
  BRIDGE to the CI symptom does not** — it rested entirely on the sum not fitting (`LL-020`, `PE-785`).
  ⚠ So `DEF-109` holds a **measured leak** and an **unproven symptom explanation**, not one finding.
- ⛔⛔ **A QUESTION'S OPTIONS ARE UNVERIFIED PROSE WEARING THE SLATE'S AUTHORITY** (`LL-051`, Approved).
  I offered *"take the .gdmp we still have locally"*; the operator **chose it** and the file did not exist.
  ⭐ **`LL-052`, PINNED:** a file's NAME and FIRST SCREEN describe its FORMAT, never its content, and a
  manifest saying `kept` is quotable only for what it `DROPPED`. Check every premise before offering it.
- ⭐⭐⭐ **ASK WHAT A *NEGATIVE* RESULT WOULD MEAN BEFORE RUNNING THE EXPERIMENT** (`LL-047`).
  Working set on a 64 GB box cannot tell a leak from lazy collection, so `PE-769`'s null result was
  uninterpretable in BOTH directions. Use `GC.GetTotalMemory(true)` after a forced full collect.
- ⭐⭐ **A ROOT-PATH TOOL NAMES *A* PATH, NEVER *THE* CAUSE** (`LL-048`). `gcroot` named the rate
  limiter; removing it changed nothing, and stripping ALL of OpenTelemetry freed **zero** hosts. **Read the
  root COUNT first** (505 here); after two failed bisects, stop naming suspects and vary the QUANTITY.
- ⚠⚠ **`DEC-111` d2 DECLINED A RE-RUN — THAT IS COMPLIANCE WITH `DEC-077` d3, NOT AN OVERRIDE**, so it does
  not advance the count. ⛔ **No number here — the decision register is the count.** A third override would
  reopen the rule; do not let a session mis-log this one as one.
- ⚠ **A 14 KB hand-paste is survivable IF you verify after:** `git show HEAD:…/defects.jsonl`, then assert
  the new title **startswith** the old byte-for-byte (`LL-028`+`LL-001`). Ran 4×; **caught a real loss once**
  — a dropped provenance sentence. ⭐⭐ **`LL-049` (pinned): a measurement AFTER the action it gates is a
  report, not a control** — `wc -l` printed 202 in the same `&&` chain that had already pushed. Gate the
  commit on it: `if [ "$N" -lt 200 ]; then git commit …; fi`.
- ⭐⭐⭐ **SWEEP BEFORE THE INTERVIEW HAS NOW *DISSOLVED* AN AGENDA ITEM TWICE, which nobody counts.**
  `DEF-087` reads stale (155 of **187**) but `DEC-068` d2 RULED it fix-forward-only, so that is the intended
  steady state and a repair would reverse a decision. ⛔ **Sweep the DECISION register for a row's id before
  calling its numbers stale** (`LL-050`, Approved).
- ⚠⚠ **COMMIT PACKAGE WRITES *BEFORE* `git checkout -b`** — they ride onto the branch (package→`main`
  direct, code→PR). Recovery without stash/reset: commit code on the branch, `git checkout main` (package
  changes travel with you), commit+push there, then back to the branch and `git rebase main`.
- ⭐⭐ **Ryuk does NOT reap before you can copy** — an exited container still yields to `docker cp <id>:/path`
  (use that, **not** `ReadFileAsync`: crash filenames are timestamped). ⚠ `StartOrFailFastAsync`'s
  `when (…TimeoutException…)` filter let a **crashed** container through uncaught: put the filter *inside*
  the catch and rethrow **unwrapped** — the register discriminates `DEF-121`
  (`ContainerNotRunningException`) from `DEF-109` (`TaskCanceledException`) on the type itself.
  ⚠ Coverage does not touch `tests/` (coverlet `IncludeTestAssembly` = false), so no ≥95% pressure there.
- ⛔ **THE FORTY-EIGHTH was mine, THIRD IN A ROW**: `prm-next.md` still said *"DIAGNOSE `DEF-109`"* in the
  very commit whose message says DIAGNOSED. **A briefing on HOW is a description too.**

## ★★★ 2026-09-01 · four PRs merged · `LL-041`–`LL-044` bind from the tool-owned note

⛔⛔ **NEVER WRITE A SLICE ID INTO A COMMAND IN `prm-next.md`** — nine sites named a CLOSED slice, each
returning a clean verdict about the wrong subject, and the fix went where the error was REPORTED rather than
where the pattern LIVED (the FORTY-SIXTH). `entity_query("slice", status="Approved")` resolves it without
naming one; `count-prompt-ids.py` cannot see this, because the id is real and its status correct.
⚠ **`ADR-0045`**: `INV-014` names the px literals AND `AA` in ONE statement, so where a `.dc.html` cannot
satisfy both, **AA governs** — minimum change to conformance, nothing more.

## ★★★ 2026-08-31 · **`SL-035` IS THE LIVE SLICE** · `SL-033`+`SL-034` CLOSED

⚠⚠ **`SL-035`'s SIX ACTIVATIONS ALL OVERRODE THE AGENT'S RECOMMENDATION TO CARRY** — never read an
activation as agreement about HOW. (Live state: `prm-next.md`'s list, never this file.)

★★★ [**CI run attribution · `skipped` · probability-remedies · `DEF-121` · the image gate**](ci-run-attribution-and-probability-remedies.md)
— **read before recording anything about CI, or proposing a fix to an intermittent failure.**

- ⚠⚠⚠ **A PR-HEAD RUN AND A MERGE-COMMIT RUN ARE DIFFERENT RUNS OVER IDENTICAL CODE** (`LL-036`) — disagreed
  twice; `gh pr checks` shows only the PR one. **Cite the RUN ID, never a colour.** ⚠⚠ **`skipped` CONFLATES
  *`if:` was false* WITH *a `needs:` job failed*** (`LL-039`). ⭐⭐ **A remedy reducing a PROBABILITY cannot be
  falsified by recurrence** (`LL-035`). ⭐ **A re-run samples every OTHER question** (`LL-037`) — found `DEF-122`.
- ⚠⚠ **A LIFECYCLE STATUS CAN BE LOAD-BEARING, NOT LAGGING** (`LL-038`) — `AC-088`–`AC-093` are `Proposed`
  **on purpose** and promoting is one-way; tell is **uniformity**. ⭐⭐ **A progress entry is a ruling's
  record too — sweep those, not just DEC/ADR.**
- ⛔ **NAME NO READINESS ANSWER — `readiness_check("package")` is it**; greens satisfy no clause, by design.
  ⚠ **`DEC-077` d3 overridden TWICE** while unconditional — a third reopens the rule; it names
  `SearchProvidersFtsTests` only, so an `Acmp.Api.Tests` re-run is not a third, but **record first**.

- ⚠⚠⚠ **PARSE THE JSON; NEVER REGEX A JSONL ROW.** `[^}]*` stops at the first `}`, so a row with nested
  `custom_attributes` is **silently deleted from the result**, not undercounted (the **FORTY-FIRST**).
- ⭐⭐ **SWEEP BEFORE THE INTERVIEW, NEVER AFTER** (`LL-005`) — it keeps producing rulings the agenda did not
  contain, and once *dissolved* an item. ⚠ `Open`/`Proposed` ≠ never-ruled (`LL-006`).
- ⚠ **`G-TRACE` needs THREE legs** for a new `mvp=1` requirement (trap 16b). ⚠ `verification_method` is a
  CHECK; `verified_by` ∈ `human|agent|ci`; approving a lesson needs `"operator_confirm": true` **plus
  byte-identical content**; trace edges use `from_id`/`to_id`. ⚠ `entity_upsert` needs FULL rows — NOT NULL
  is evaluated before conflict resolution — but **nullable fields are preserved by omission**. ⚠ `deferred_work.invariant_at_stake` is an FK: omit it, never send `"None"`.
- ⭐⭐⭐ **A TARGETED SWEEP FINDS CLAIMS ABOUT WHAT YOU *CHANGED*; ONLY A FULL READ FINDS CLAIMS ABOUT WHAT
  *REMAINS*** (the **FORTY-SECOND**). ⚠⚠ **A POINTER AT A FINISHED SLICE RETURNS A CLEAN ANSWER ABOUT THE
  WRONG SUBJECT** — a closed slice's `wbs-done` **passes with zero entities**. ⭐ **Name no slice id in
  durable prose.** ⚠ **`PH-3` and `PH-7` are `Approved`, not closed** — release close-out is **not** due
  while a slice is open.

★★★ [**`SL-034` — the slate generator's 3 refusals + the ASVS pack**](sl034-slate-generator-and-asvs-pack.md)
— read it before touching `gen-slice-review-slate.mjs`, the ASVS pack, or any test that reads the package.

- ⭐⭐⭐ **`LL-032` (pinned): a fixture that is the LIVE REGISTER changes meaning when somebody does ordinary
  work, and the dangerous outcome is the *PASS*.** Ask *what would a normal day's work change?* (`LL-044`
  extends it to the CLOCK.) ⛔⛔ **NEVER carry `security-controls.md` §20's *"L2 is met across all applicable
  chapters"*** — the self-assertion `DW-079` forbids. ⭐ ASVS levels are CUMULATIVE: "L2" = 253 at L1+L2.
- ⚠⚠ **`jq` IS NOT INSTALLED** — a monitor built on it emits nothing and silence reads as "still running".
  Use `gh --jq`. ⚠ Pin the sha in a CI poller.

★★★ [**`SL-033` per-item findings**](sl033-slice-findings.md) · [**`DW-082` / Dependabot arc**](dw082-sweep-and-vitest4.md)

- ⭐⭐ **THE HABIT THAT PAID ON ALL TEN ITEMS:** read the row's own text, then sweep the NARRATIVE docs
  **and** the ADR/decision/OQ registers **by keyword** before sizing (`LL-008`, `LL-025`) — ten different
  directions of mis-sizing, including *the row was accurate and the trap was in the code*.
- ⚠⚠ **A DEFENCE LAYER CAN BE INVISIBLE TO ANY FRONT-DOOR TEST** (`LL-030`) — a path gate intercepts first,
  so all 10 API tests stayed GREEN. ⭐ **Pin each refusal to a DISTINGUISHABLE SIGNATURE.** ⭐ **When an item
  asks you to add the thing whose ABSENCE is the guarantee, ISOLATE it.**
- ⚠⚠ **A REFACTOR CAN CROSS THE PER-FILE COVERAGE FLOOR WITH NO NEW UNTESTED LINE** (`LL-031`) — the
  denominator moved. ⛔ **RUN THE GATE, NOT THE TESTS.** ⛔ **NEVER lower `ADR-0016`'s 95%** — v3 scored
  files with **no test file** ≥95%.
- ⛔⛔ **A RED FROM `SearchProvidersFtsTests` IS REAL — STOP, DO NOT RE-RUN** (`DEC-077` d3). ⚠⚠ **`scripts/**`
  is NOT path-ignored** (`DEC-077` d2) — PR route, and **poll CI after ANY direct push to `main`**; several
  `check-*.mjs` **are** the gates. ⚠ **`DEF-107`: approving+pinning a lesson does NOT bind it** — run
  `handoff_emit` in the SAME batch. ⚠ **Push package writes BETWEEN merge cycles.**
- ⚠ **`DW-088`: `TopicDetail`'s download button is hardcoded `disabled`** — no principal but a guest
  presenter can open a topic attachment. ⚠ **`WithIdentityProvider()` is opt-in.**
- ⛔ **`SEC-080` asserts a legal hold overrides any purge and NO HOLD MECHANISM EXISTS** (`OQ-080`) — answer
  it BEFORE Phase 2 retention enforcement. ⚠ **Approved ACs are IMMUTABLE even against being marked
  superseded** (`AC-147`). ⛔ **Never `PageSize.Clamp` an export.** ⚠ **`DEF-109`** is DIAGNOSED — a further red
  wants clause (2)'s CAPTURED ARTEFACT and an appended occurrence, never a re-run.
- ⭐ **Instruments to USE, not re-derive:** `coverage-triage` · `gen-lesson-docket` · `gen-slice-review-slate`
  · `gen-record-slate` (cross-register) · `check-image-contract` · `check-asvs-pack-paths` ·
  `gen-dw-disposition-slate` · `count-prompt-ids.py` · `number-render-scan`.

## ★★ 2026-08-20 · the disposition session — durable rules only

- ⚠⚠⚠ [**AN ID IS A POINTER, NOT A REFERENCE**](an-id-is-a-pointer-not-a-reference.md) — the operator
  **refused an interview** over it. `LL-011`, pinned. Anything they read to DECIDE carries each record's
  full text inline, **generated** from the JSONL. ⭐ `G-IDS` checks FKs, **not ids in prose** (`DEF-101`).
- ⚠⚠ **A REQUIREMENT'S STATUS AND ITS `DW-` ROW'S ARE UNRELATED COLUMNS AND NOTHING COMPARES THEM** —
  activating a `DW-` row → check its requirement in the same breath (`LL-042` is the `SC-`/`WBS-` twin).
  ⚠ `assumptions-current`'s field is a FUTURE due date; more will redden and that is the control working.
  ⚠ **Measuring inside the set you hold is not measuring the register** (said 4; 8). ⛔ **`DEF-087` is not
  "untouched" — it is RULED** (`DEC-068` d2, fixed-forward-only); see the 2026-09-02 block.
★★ [**Durable rules from batches 13–21**](batches-13-21-durable-rules.md) — `Met`-verdict scope, the
enforcing-mechanism trap, never leave a Pending AC, Hangfire process-globals, union coverage, `$?` after a
pipe, production's reconciled state.

## Earlier 2026-08 — durable findings only

- ★★★ [**`DEF-078`: a green control can be blind**](a-green-control-can-be-blind.md) — a healthcheck
  evaluating ZERO checks; gitleaks passing 153 commits over an allowlist exempting every markdown file.
  ⚠ Read `ADR-0043`, **not** `ADR-0042` (Superseded). · ★★ [**An absence needs a proven
  instrument**](an-absence-needs-a-proven-instrument.md) — `DEF-056`'s two `NotContain` controls passed
  **VACUOUSLY**. · ⚠⚠ [**v4 store + 4.4.x mechanics**](tamheed-v4-and-liveness.md) — build payloads from
  the JSONL; `WVR-` operator-only; approving a lesson needs `operator_confirm`.
- ★★ **Requirement status measures whether anyone WROTE an AC, not whether it was built.** `DEF-012` is
  Won't-fix (`DEC-055`). ⚠⚠ **Stream scope had NEVER run on a real DB** (`DEF-066`) —
  [[inmemory-provider-hides-db-refusals]]; `DEF-068`: a stream-scoped policy is RESOURCE-ONLY.
  ⚠ **6 stale branches** (2026-08-21) all pre-date `4c1b356`, so all carry `DEF-064`'s broken `ar.json`.
- **ADR-0039 `AC-090`** per-request revalidation — ⚠ **an unknown subject must be ALLOWED** (ADR-0004
  provisions JIT, so failing closed refuses every first login). **`DEF-052`: there is NO read-side role
  gate** — every named policy is a WRITE capability; fixed by `GuestSurfaceMiddleware`, deny-by-default.

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
- [Localhost CI hides load races](localhost-ci-hides-load-races.md) · [Git push hang → `gh auth setup-git`](git-push-hang-fix.md) · [CI gates locally pre-push](ci-gates-run-locally-pre-push.md) · [Always stage .claude/memory](always-stage-claude-memory-in-commits.md)
- [Coverage & E2E mandate](coverage-and-e2e-mandate.md) — ≥95% FE+BE + adversarial E2E. ⚠ Playwright is **NOT UAT-only** (7 services + real Keycloak per PR) **but runs `KEYCLOAK_ADMIN_ENABLED=false`**, so it never touches the ADR-0038 write path.
- [E2E local run (non-destructive)](e2e-local-run-nondestructive.md) — **`-p acmpe2e` ONLY**, never `npm run e2e:up`. · [Dev-stack rebuild pitfall](dev-stack-rebuild-pitfall.md) — **never `up --build`** the long-lived dev stack.
- [Exact design fidelity + visual loop](exact-design-fidelity-visual-loop.md) · [A green suite is not a look](a-green-suite-is-not-a-look.md) (⚠ the throwaway harness must import **only** the stylesheets the real route imports) · [breadcrumb spacing](breadcrumb-spacing-rule.md) · [i18n parity ≠ completeness](i18n-parity-not-completeness.md) · [visual-verify cache busting](web-visual-verify-cache-busting.md)
- ⚠ **`.adm-detail-card` has no padding and clips its children** — a popover needs `.adm-card-overflow`. · **`userEvent.setup()` installs its own clipboard stub** — define a clipboard spy *after* it.
- [User prefers simple English](user-prefers-simple-english.md) · [Phase prompt Standard Footer](phase-prompt-standard-footer.md) · [Install the schedule, not just the daemon](install-the-schedule-not-just-the-daemon.md) · [Arabic rename is a grammar rule](arabic-rename-grammar-not-substitution.md) · [A clean scan must prove it had a subject](scan-must-prove-it-had-a-subject.md) · [Guard the property, not the value](guard-the-property-not-the-value.md) · [The suite assumed a fresh database](e2e-assumes-a-fresh-database.md) · [The feature is often already half-built](check-before-building.md)
- ⚠ **AC id cells in markdown tables must stay BARE** (`| AC-001 |`) — bolding breaks the G-PROGRESS gate. · **A new advisory can turn `main` red with no code change** (`GHSA-q939-rpr3-3284`). · **A compose `secrets:` entry whose FILE IS MISSING fails the WHOLE stack** — write mounted secrets unconditionally.

## ⚠ Unlinked topic files + the completed ladder
**Findable by `ls`, current value NOT vouched for** — eleven unlinked files plus the completed ladder
P1–P19 + PH-5 (`p*`/`keystone-*`/`ph5-*`), superseded by the package's slice rows. ⛔ Do not re-open.
⚠ Re-run the inbound-link check after ANY compaction (2026-08-29: 25 orphans, all ladder files).
