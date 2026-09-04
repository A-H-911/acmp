# Memory Index — ACMP

> One line per entry; detail lives in topic files and the package. Read the linked file before acting.
> ⚠⚠ **MEASURED CEILING: 200 LINES** (not bytes — disproven, wrong dimension). Past 200 drops **silently**
> and had already eaten "Standing rules". ⭐ **`wc -l` BEFORE *and AFTER* every edit** — 2026-09-03 went
> 199→201→202 *while trimming*: replacing 2 lines with 4 is an ADD. Keep under ~140.


## ★★★ 2026-09-04 · `WBS-27.2` MERGED (`1ebf3a5c`) · `DEF-135` · `LL-056`/`LL-057`

★★★ ⚠ **`DEF-136`: CANDIDATE + FIX APPLIED 2026-09-04, *UNVERIFIED*** — [[a-valid-key-can-be-inert]]
(`LL-058`; **`PE-859` CORRECTS `PE-855`**). ✅ **`permissions.defaultMode` IS USER/MANAGED/FLAG SCOPE ONLY**
— in a PROJECT file it does **NOT** take effect, and bypass enters Shift+Tab **only if armed at startup**
⇒ *"there is no bypass mode"* = same fact. Fix = `~/.claude/settings.json` + **restart**; ⛔ AGENT MUST NOT
(self-widening). ⛔ **WITHDRAWN: *"broad wildcards are suspended"*** — only `Bash(*)` + **wildcarded
interpreters** are (`python:*` yes, `grep:*` NO) ⇒ **bare `grep` still unexplained.** ⭐⭐ **`blockReadsOutside
WorkingDirectories` (ON, user scope) prompts reads outside the wd EVEN IN BYPASS** — it survives the fix.
⛔⛔ **I READ A SUBAGENT'S *SUMMARY* OF THE DOCS, NOT THE DOCS — 6 surfaces + 3 commits (`LL-006`: a summary
of a document IS a document).** ⚠ `PE-854`: my own `cd … &&` prefix made this session's prompts.
★★★ ⛔⛔ **ADVICE CAN BE RIGHT WHILE ITS `MECHANISM` IS FALSE — the mechanism is what you generalise from**
(`LL-056`, pinned, **5 instances in one day**): FIRST-TOKEN matching; *format the SOLUTION*
right-for-a-wrong-reason; a store re-flush claim that REFUSED; `DEF-136`; and inventing a person's
mechanism from their one-word outcome. ⭐ **`Edit(.claude/**)` prompts BY DESIGN but is NOT forbidden** —
the classifier blocks *widening*, not correcting prose; that conflation held `DEF-133` open a day.
- ⭐⭐⭐ **`WBS-27.2`: THE EXPENSIVE THING WAS THE HOST, NOT THE DATABASES** (`LL-057`). Two weeks of rows
  priced in *"sharing a host shares its 14 InMemory DBs"*. **Only bundled — nobody chose that isolation,
  so nobody wrote it down.** A ~30-line `Reset()` separates them: **287 constructions → 47 hosts (83.6%)**,
  suite 4m17s→38s, CI backend **9m07s/9m37s → 5m36s**. ⚠ Naive share-everything failed **72/445** first.
  ⛔ **Claims NOTHING about `DEF-109`.**
- ⚠⚠ **C31 FIRES ON *COMMITTED* WRITES TOO** — `gh pr merge --delete-branch` took a branch-only package
  commit; store had the row, JSONL did not. **Safe = REACHABLE FROM A SURVIVING REF.** ⭐ Repair:
  `git checkout <lost-sha> -- <file>` once `git diff` proves the delta. ⚠ `cancel-in-progress` is
  `pull_request`-ONLY. ⚠ **`gh pr checks --watch` right after a push exits 1** — use `gh run watch <id>`.

## ★★★ 2026-09-03 · `DEF-109` occ 6: SCHEDULING REFUTED · `DEC-123`–`DEC-125` · `LL-055`

★★★ [**A control proves FIRING, never COUPLING**](a-control-proves-firing-not-coupling.md) — **read before
shipping any detector or writing the test that proves it.** The watchdog's trigger measured whether the
PROCESS was scheduled; occ 6 showed drift **under 3 ms** while 18 requests burned 100-second ceilings, so it
could never fire. ⛔ I then committed the same fault **twice inside the fix**, 40 minutes after filing it.
★★ **Permission prompts: read the 2026-09-04 block above — `DEF-136` supersedes the shape story, and the
`cd`/path-form candidate is DEAD.** ⭐ **ASK which tool the prompt named**: three times now, one word from
the operator has settled what rounds of inference could not.
- ⭐⭐⭐ **`DEF-109` occ 6 left the FIRST artefact in six occurrences** (CI `33765425613`): 204 heartbeats,
  ZERO snapshots, `windowMaxPending` never climbing ⇒ **thread-pool starvation REFUTED**, deadlock survives.
  ⛔ Still NOT clause (2) — an elimination is not an identification. `DEC-121` d2's 40-min ceiling is why the
  job finished and uploaded at all.
- ⛔⛔ **`AC-126` ASSERTS FULL-HISTORY SCANNING** (`fetch-depth: 0`) in its own `Then` clause — narrowing the
  gitleaks scan falsifies a `Met` AC and needs an `SC-`+ADR. `DEF-132` checked that AC and cleared it against
  the *allowlist-shape* clause: a clean confirming answer about the wrong clause (`LL-046`).
- ⚠⚠ **`secrets` durations are NOT monotonic** — 5m34s…10m04s, an 80% spread; the *work* grows, the runtime
  varies. A bound sized for growth alone is undersized. Raised 10→30 (`DEC-123` d1).
- ⚠⚠ **xUnit 2.5.3: same collection ⇒ never parallel.** `IClassFixture` creates NO collection (safe);
  `ICollectionFixture` does ⇒ cross-class sharing **serialises the suite** (`DEC-124`). ⭐ **The test CLASS
  is constructed per TEST** — that ctor is the per-test hook even when the fixture is per-class.
  ⚠ **`CreateDefaultClient` is NOT virtual**; use an `IStartupFilter` for a suite-wide seam.

## ★★★ 2026-09-02 · `DEF-109` diagnosed · `DEF-121`: memory pressure REFUTED, clause (2) still unmet · `DEC-111`–`DEC-116`

⛔⛔ **NEVER WRITE A CI COLOUR INTO DURABLE PROSE** — this heading said *`main` still red* and my own session
falsified it hours later. `gh run list --branch main` is the answer; cite a RUN ID (`LL-036`).
★★★ [**`DEF-109`: the HOST is the unit that leaks**](def109-the-host-is-the-unit-that-leaks.md) — **read
before any memory/perf investigation, and before trusting any `gcroot` output.** 137 MB over 20
`WebApplicationFactory` hosts vs 8 MB over 1 for identical work; 20/20 disposed factories alive after a
forced GC; 3% fix declined. ⭐ `DEC-120` ACTIVATED `DW-096`→`WBS-27.2` in `SL-036`; occ 5 was 2026-09-03.
★★★ [**AN INSTRUMENT MUST REPORT ON ITSELF**](an-instrument-must-report-on-itself.md) — before shipping ANY
detector: 4 failure modes, the 2 that PRODUCE OUTPUT are worst; positive controls are practice here but in NO rule register.
★★★ [**PERMISSION PROMPTS**](permission-prompts-four-causes.md) — `Write()` rules are DEAD (only
  `Edit()`); pass Write/Edit a **REPO-RELATIVE** path. ⛔ `rm`/push prompt BY DESIGN. ⭐ Scratch
  `.scratch/<id>/`; memory = junction.
★★★ [**READ THE ARTEFACT, NOT THE ENTRY ABOUT IT**](read-the-artefact-not-the-entry-about-it.md) —
  the memory-pressure hypothesis is **REFUTED** from two files the capture KEPT; `DW-097`'s *"it is in
  the dropped dump"* was FALSE; clause (2) is STILL unmet (an elimination is not an identification).
  ⛔⛔ **Also: `strict:true` means ANY push to `main` leaves every open PR unmergeable** — path-ignore
  stops the CI RUN, not the staleness. Read it before touching CI, an artefact, or an open PR.
- ⛔⛔ **THE HOSTED RUNNER IS ~16 GB, NOT 7.** I asserted 7 from memory into `PE-771`. **Every MEASURED
  `DEF-109` claim survives; the causal BRIDGE to the CI symptom does not** (`LL-020`, `PE-785`). ⚠ So it
  holds a **measured leak** and an **unproven symptom explanation** — and 2026-09-03 refuted scheduling too.
- ⛔⛔ **A QUESTION'S OPTIONS ARE UNVERIFIED PROSE WEARING THE SLATE'S AUTHORITY** (`LL-051`). I offered a
  file that did not exist and the operator **chose it**. ⭐ **`LL-052`, PINNED:** a file's NAME and FIRST
  SCREEN describe its FORMAT, never its content; a manifest saying `kept` is quotable only for its `DROPPED`.
- ⭐⭐⭐ **ASK WHAT A *NEGATIVE* RESULT WOULD MEAN BEFORE RUNNING THE EXPERIMENT** (`LL-047`). Working set on
  a 64 GB box cannot tell a leak from lazy collection. Use `GC.GetTotalMemory(true)` after a forced collect.
- ⭐⭐ **A ROOT-PATH TOOL NAMES *A* PATH, NEVER *THE* CAUSE** (`LL-048`). `gcroot` named the rate limiter;
  removing it changed nothing. **Read the root COUNT first**; after two failed bisects, vary the QUANTITY.
- ⚠ **A big hand-paste is survivable IF you verify after** — byte-compare against the pre-image
  (`LL-028`+`LL-001`); caught a real loss once. ⭐⭐ **`LL-049`: a measurement AFTER the action it gates
  is a report, not a control.**
- ⭐⭐ **SWEEP THE DECISION REGISTER FOR A ROW'S ID BEFORE CALLING ITS NUMBERS STALE** (`LL-050`) —
  `DEF-087` reads stale but `DEC-068` d2 ruled it fix-forward-only; a "repair" would reverse a decision.
- ⚠⚠ **COMMIT PACKAGE WRITES *BEFORE* `git checkout -b`** — they ride onto the branch (package→`main`
  direct, code→PR). ⚠⚠ **`strict: true`: ANY push to `main` leaves every open PR stale**, whatever it
  touched — push package writes FIRST, then rebase, then nothing to `main` until the PR lands.
- ⭐⭐ **Ryuk does NOT reap before you can copy** — `docker cp <id>:/path`, not `ReadFileAsync`. ⚠ The
  register discriminates `DEF-121` (`ContainerNotRunningException`) from `DEF-109` (`TaskCanceledException`)
  **on the exception type** — always discriminate by signature before attributing a red.
- ⛔⛔ **NEVER NAME A SLICE ID OR DESCRIBE AN ITEM IN `prm-next.md`** — nine commands named a CLOSED slice
  (46th); *"DIAGNOSE `DEF-109`"* shipped in the commit saying DIAGNOSED (48th). **A briefing on HOW is a
  description too.** ⚠ **`ADR-0045`**: where a `.dc.html` cannot satisfy `INV-014`'s px AND `AA`, AA governs.

## ★★★ 2026-08-31 · CI attribution · activations are not agreement

⛔ **This heading named a live slice and went stale** — resolve it with
`entity_query("slice", status="Approved")`, never from here. ⚠⚠ **Activations routinely OVERRIDE the
agent's recommendation to carry** — never read one as agreement about HOW.

★★★ [**CI run attribution · `skipped` · probability-remedies · `DEF-121` · the image gate**](ci-run-attribution-and-probability-remedies.md)
— **read before recording anything about CI, or proposing a fix to an intermittent failure.**

- ⚠⚠⚠ **A PR-HEAD RUN AND A MERGE-COMMIT RUN ARE DIFFERENT RUNS OVER IDENTICAL CODE** (`LL-036`) — disagreed
  twice; `gh pr checks` shows only the PR one. **Cite the RUN ID, never a colour.** ⚠⚠ **`skipped` CONFLATES
  *`if:` was false* WITH *a `needs:` job failed*** (`LL-039`). ⭐⭐ **A remedy reducing a PROBABILITY cannot be
  falsified by recurrence** (`LL-035`). ⭐ **A re-run samples every OTHER question** (`LL-037`) — found `DEF-122`.
- ⚠⚠ **A LIFECYCLE STATUS CAN BE LOAD-BEARING, NOT LAGGING** (`LL-038`) — tell is **uniformity**. ⭐⭐ **A
  progress entry is a ruling's record too — sweep those, not just DEC/ADR.** ⛔ **Name no readiness answer.**
- ⚠⚠⚠ **PARSE THE JSON; NEVER REGEX A JSONL ROW.** `[^}]*` stops at the first `}`, so a row with nested
  `custom_attributes` is **silently deleted from the result**, not undercounted (the **FORTY-FIRST**).
- ⭐⭐ **SWEEP BEFORE THE INTERVIEW, NEVER AFTER** (`LL-005`) — keeps producing rulings the agenda lacked, has
  *dissolved* items 4×, and on 2026-09-03 **removed an OPTION from inside a row**. ⚠ `Open` ≠ never-ruled.
- ⚠ **`G-TRACE` needs THREE legs** for a new `mvp=1` requirement (trap 16b). ⚠ `verification_method` is a
  CHECK; `verified_by` ∈ `human|agent|ci`; approving a lesson needs `"operator_confirm": true` **plus
  byte-identical content**; trace edges use `from_id`/`to_id`. ⚠ `entity_upsert` needs FULL rows — NOT NULL
  is evaluated before conflict resolution — but **nullable fields are preserved by omission** (so a status
  flip can omit `custom_attributes`; generate the payload from the JSONL and verify byte-identity after).
- ⭐⭐⭐ **A TARGETED SWEEP FINDS CLAIMS ABOUT WHAT YOU *CHANGED*; ONLY A FULL READ FINDS CLAIMS ABOUT WHAT
  *REMAINS***. ⚠⚠ **A POINTER AT A FINISHED SLICE RETURNS A CLEAN ANSWER ABOUT THE WRONG SUBJECT.** ⭐ **Name
  no slice id in durable prose.** ⚠ **`PH-3`/`PH-7` are `Approved`, not closed** — close-out is not due.

★★★ [**`SL-034` — slate generator + ASVS pack**](sl034-slate-generator-and-asvs-pack.md) ·
[**`SL-033` findings**](sl033-slice-findings.md) · [**`DW-082`/Dependabot**](dw082-sweep-and-vitest4.md)

- ⭐⭐⭐ **`LL-032` (pinned): a fixture that is the LIVE REGISTER changes meaning when somebody does ordinary
  work, and the dangerous outcome is the *PASS*.** ⛔⛔ **NEVER carry `security-controls.md` §20's *"L2 is met
  across all applicable chapters"***. ⚠⚠ **`jq` IS NOT INSTALLED** — use `gh --jq`; a monitor built on it is
  silent, and silence reads as "still running". ⭐ **Emit on EVERY terminal conclusion** — `cancelled` is a
  third one and a `failure` filter steps over it (`DEF-132`).
- ⭐⭐ **THE HABIT THAT PAID ON ALL TEN `SL-033` ITEMS:** read the row's own text, then sweep the NARRATIVE
  docs **and** the ADR/decision/OQ registers by keyword before sizing (`LL-008`, `LL-025`).
- ⚠⚠ **A DEFENCE LAYER CAN BE INVISIBLE TO ANY FRONT-DOOR TEST** (`LL-030`). ⚠⚠ **A REFACTOR CAN CROSS THE
  PER-FILE COVERAGE FLOOR WITH NO NEW UNTESTED LINE** (`LL-031`) — ⛔ **RUN THE GATE, NOT THE TESTS**, and
  never lower `ADR-0016`'s 95%. ⚠ Coverage excludes `tests/`, so test-only files add no pressure.
- ⛔⛔ **A RED FROM `SearchProvidersFtsTests` IS REAL — STOP, DO NOT RE-RUN** (`DEC-077` d3, overridden
  TWICE; a third reopens the rule). It names that test ONLY, so a `DEF-109` red does not fire it.
  ⚠⚠ **`scripts/**` and `.gitignore` are NOT path-ignored** — PR route; **poll CI after ANY push to `main`**.
  ⚠ **`DEF-107`: approving+pinning a lesson does NOT bind it** — run `handoff_emit` in the SAME batch.
- ⛔ **`SEC-080` asserts a legal hold overrides any purge and NO HOLD MECHANISM EXISTS** (`OQ-080`).
  ⚠ **Approved ACs are IMMUTABLE** (`AC-147`). ⛔ **Never `PageSize.Clamp` an export.** ⚠ **`DW-088`:
  `TopicDetail`'s download button is hardcoded `disabled`.**
- ⭐ **Instruments to USE, not re-derive:** `coverage-triage` · `gen-lesson-docket` · `gen-slice-review-slate`
  · `gen-record-slate` (cross-register) · `check-image-contract` · `check-asvs-pack-paths` ·
  `gen-dw-disposition-slate` · `count-prompt-ids.py` · `number-render-scan`.

## ★★ 2026-08-20 · the disposition session — durable rules only

- ⚠⚠⚠ [**AN ID IS A POINTER, NOT A REFERENCE**](an-id-is-a-pointer-not-a-reference.md) — the operator
  **refused an interview** over it. `LL-011`, pinned. Anything they read to DECIDE carries each record's
  full text inline, **generated** from the JSONL. ⭐ `G-IDS` checks FKs, **not ids in prose** (`DEF-101`).
- ⚠⚠ **A REQUIREMENT'S STATUS AND ITS `DW-` ROW'S ARE UNRELATED COLUMNS AND NOTHING COMPARES THEM** —
  activating a `DW-` row → check its requirement in the same breath (`LL-042` is the `SC-`/`WBS-` twin).
  ⚠ **Measuring inside the set you hold is not measuring the register.**
★★ [**Durable rules from batches 13–21**](batches-13-21-durable-rules.md) — `Met`-verdict scope, the
enforcing-mechanism trap, never leave a Pending AC, Hangfire process-globals, union coverage, `$?` after a
pipe, production's reconciled state.

## Earlier 2026-08 — durable findings only

- ★★★ [**`DEF-078`: a green control can be blind**](a-green-control-can-be-blind.md) — a healthcheck
  evaluating ZERO checks; gitleaks passing over an allowlist exempting every markdown file. ⚠ Read
  `ADR-0043`, **not** `ADR-0042`. · ★★ [**An absence needs a proven
  instrument**](an-absence-needs-a-proven-instrument.md) · ⚠⚠ [**v4 store + 4.4.x
  mechanics**](tamheed-v4-and-liveness.md) — build payloads from the JSONL; `WVR-` operator-only.
- ★★ **Requirement status measures whether anyone WROTE an AC, not whether it was built.** ⚠⚠ **Stream
  scope had NEVER run on a real DB** (`DEF-066`) — [[inmemory-provider-hides-db-refusals]].
- **ADR-0039 `AC-090`** per-request revalidation — ⚠ **an unknown subject must be ALLOWED** (ADR-0004
  provisions JIT). **`DEF-052`: there is NO read-side role gate** — every named policy is a WRITE
  capability; fixed by `GuestSurfaceMiddleware`, deny-by-default.

## Standing rules & gotchas (read before editing)

- [★ Read the implementation before calling it a defect](read-before-calling-it-a-defect.md) — **ten**+ instances, never caught by a gate. **Read the predicate, not the doc comment describing it.** ⚠ The LSP panel is stale constantly, but a fresh diagnostic on your OWN edit is usually right — it caught `CreateDefaultClient` not being virtual.
- [★ The InMemory provider hides DB refusals](inmemory-provider-hides-db-refusals.md) — always ask "has this write ever run against SQL Server?" ⚠ Only `Acmp.Integration.Tests` is real SQL Server.
- [★ Controls must DETECT **and** TELL](controls-must-detect-and-tell.md) — **nine** instances; the "tell" half is normally the untested one.
- [★ Verify mechanically, not carefully](verify-mechanically-not-carefully.md) — `entity_upsert` replaces FULL rows; the JSONL flushes on EVERY write, so git HEAD is a live baseline. ⚠ **A measurement that indicts known-good code is measuring itself.** ⚠ PowerShell: always `--body-file`/`-F <file>`, never `-m` with backticks; it also joins arrays with SPACES.
- ⚠ **`open_question.lifecycle_status` is a CHECK** over `Draft/Proposed/Approved/Rejected/Deferred/Implemented/Superseded/Obsolete` — "Resolved" rolls the whole batch back. `defect.fixed_by` is a **FK**; PR refs go in `custom_attributes`. ⚠ `progress_update` `event_type` is a CHECK too — `finding` is NOT valid; use `note`.
- ⚠ **Env one-offs:** the keycloak container's `docker exec` shell has no `KC_BOOTSTRAP_ADMIN_PASSWORD` (read `/run/secrets/kc_bootstrap_admin_password`); Windows `python3` cannot see Git Bash's `/tmp`.
- [⚠ Baselines are numbers, not properties](baselines-as-numbers-not-properties.md) · [⚠ Immutable history → cleanup is asymmetric](immutable-history-cleanup-asymmetry.md) — **disable a Keycloak user, never delete** · [A static file cannot configure a live realm](a-static-file-cannot-configure-a-live-realm.md) — `reconcile.sh` is the only seam to prod/UAT.
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
**Findable by `ls`, value NOT vouched for** — eleven unlinked files + the completed ladder P1–P19 + PH-5
(`p*`/`keystone-*`/`ph5-*`), superseded by the slice rows. ⛔ Do not re-open. ⚠ Re-run the inbound-link
check after ANY compaction (2026-08-29: 25 orphans, all ladder files).
