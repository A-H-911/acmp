# Memory Index — ACMP

> One line per entry; detail lives in topic files and the package. Read the linked file before acting.
> ⚠⚠ **MEASURED CEILING: 200 LINES** (not bytes — the old byte figure was disproven and the wrong
> dimension). Past 200 is dropped **silently**, and it had already eaten the "Standing rules" section.
> ⭐ **A limit disproven in one unit is not a limit disproven.** `wc -l` before adding; keep under ~140.


## ★★★ 2026-09-02 · **`DEF-109` DIAGNOSED** (`DEC-111` d1) · `main` still red at `f91bf705`

★★★ [**`DEF-109`: the HOST is the unit that leaks**](def109-the-host-is-the-unit-that-leaks.md) — **read
before any memory/perf investigation, and before trusting any `gcroot` output.** 137 MB over 20
`WebApplicationFactory` hosts vs 8 MB over 1 for identical work; 20/20 disposed factories alive after a
forced GC. Carried, not fixed — remedy is `DW-096` (share hosts, ~56 classes); the 3% store fix was
declined on `LL-035`.
- ⭐⭐⭐ **ASK WHAT A *NEGATIVE* RESULT WOULD MEAN BEFORE RUNNING THE EXPERIMENT** (`LL-047`, Proposed).
  Working set on a 64 GB box cannot tell a leak from lazy collection, so `PE-769`'s null result was
  uninterpretable in BOTH directions. Use `GC.GetTotalMemory(true)` after a forced full collect.
- ⭐⭐ **A ROOT-PATH TOOL NAMES *A* PATH, NEVER *THE* CAUSE** (`LL-048`, Proposed). `gcroot` named the rate
  limiter; removing it changed nothing, and stripping ALL of OpenTelemetry freed **zero** hosts. **Read the
  root COUNT first** (505 here); after two failed bisects, stop naming suspects and vary the QUANTITY.
- ⚠⚠ **`DEC-077` d3's OVERRIDE COUNTER IS STILL TWO.** `DEC-111` d2 declined a re-run — that is compliance,
  not a third override. ⛔ A third would reopen the rule; do not let a session mis-log this one as one.
- ⚠ **A 14 KB hand-paste into `entity_upsert` is survivable IF you verify after:** `git show
  HEAD:…/defects.jsonl`, then assert the new title **startswith** the old byte-for-byte (`LL-028`+`LL-001`).
- ⚠ **`entity_upsert` FK trap:** `deferred_work.invariant_at_stake` is a **foreign key** — the JSONL prints
  `None` for null, and sending the string `"None"` rolls the whole batch back. Omit nullable fields.
- ⛔ **THE FORTY-EIGHTH was mine and is the THIRD IN A ROW of its class** — `prm-next.md` item 1 still said
  *"DIAGNOSE `DEF-109`"* in the very commit whose message says DIAGNOSED. **A briefing on HOW is a
  description too**, and the FORTY-FIFTH's rule (*never describe an item*) did not read as covering it.

## ★★★ 2026-09-01 · four PRs merged · **`LL-041`–`LL-044` now BIND — read them, they are not restated here**

⛔⛔ **NEVER WRITE A SLICE ID INTO A COMMAND IN `prm-next.md`.** Nine sites said
``readiness_check(scope="slice", id="SL-033")`` — a CLOSED slice, so each returned a clean verdict about the
wrong subject. The FORTY-SECOND fixed two such sites in the kickoff block and **missed nine in §6**: the fix
went where the error was REPORTED, not where the pattern LIVED (the FORTY-SIXTH). `entity_query("slice",
status="Approved")` resolves it without naming one. ⚠ `count-prompt-ids.py` cannot see this — the id is real
and its status correct.
⛔ **`LL-041`–`LL-044` ARE APPROVED AND PINNED, SO THEY ALREADY BIND FROM THE TOOL-OWNED NOTE — the
four-bullet restatement that stood here is DELETED, not refreshed. Two sources for one instruction is
how the wrong one gets read; this block's own header said *not restated here* while restating them.
⚠ **`ADR-0045`**: `INV-014` names the px literals AND `AA` in ONE statement, so where a `.dc.html` cannot
satisfy both, **AA governs** — minimum change to conformance, nothing more.

## ★★★ 2026-08-31 · **`SL-035` IS THE LIVE SLICE** · `SL-033`+`SL-034` CLOSED

⚠ Live state is `prm-next.md`'s numbered list, never this file. ⛔⛔ **NAME NO READINESS ANSWER HERE — ASK
IT.** This block said *"`ready:FALSE` on `DEF-108` alone, the intended state"*; `DEC-097` d1 closed that row
and the sentence went **doubly** false. `readiness_check("package")` is the answer, and
`entity_query("slice", status="Approved")` resolves the live slice without naming one.
⛔ **`SL-033`/`SL-034` are CLOSED** (`DEC-098` d2 / `DEC-093`). **`SL-035` = `WBS-26.1`–`26.5`**, security
first (`DEC-094`/`SC-038`). ⚠⚠ **ALL SIX ACTIVATIONS OVERRODE THE AGENT'S RECOMMENDATION TO CARRY** — rows
record it as an override, so do not read it as agreement about HOW.

★★★ [**CI run attribution · `skipped` · probability-remedies · `DEF-121` · the image gate**](ci-run-attribution-and-probability-remedies.md)
— **read before recording anything about CI, or proposing a fix to an intermittent failure.**

- ⚠⚠⚠ **A PR-HEAD RUN AND A MERGE-COMMIT RUN ARE DIFFERENT RUNS OVER IDENTICAL CODE** (`LL-036`, pinned) —
  disagreed twice here. `gh pr checks` shows only the PR one. **Cite the RUN ID, never a colour.**
- ⚠⚠ **`skipped` CONFLATES *`if:` was false* WITH *a `needs:` job failed*** (`LL-039`) — reached 3 commit
  messages. ⭐⭐ **A REMEDY THAT REDUCES A *PROBABILITY* CANNOT BE FALSIFIED BY THE FAILURE RECURRING**
  (`LL-035`, pinned). ⭐ **A re-run samples every question the suite asks** (`LL-037`) — one found `DEF-122`.
- ⚠⚠ **A LIFECYCLE STATUS CAN BE LOAD-BEARING, NOT LAGGING** (`LL-038`, Proposed). `AC-088`–`AC-093` are
  `Proposed` **on purpose**; promoting is **one-way**. ⭐ **Tell: uniformity** — six of six is a decision.
  ⭐⭐ **A progress entry is a ruling's record too — sweep those, not just DEC/ADR.**
- ⛔ **A READINESS ANSWER STOOD HERE AND IS DELETED, NOT REFRESHED** — *"`DEF-121` is the sole blocking
  readiness failure"*, which `DEF-125`/`DEF-126` falsified on 2026-09-01 and their fixes falsified back.
  This block's header already says ASK IT. **`readiness_check("package")` is the answer**; greens satisfy
  no clause, by design. ⚠ **`DEC-077` d3 overridden TWICE** while unconditional — a third reopens the rule.
  ⭐ **d3 names `SearchProvidersFtsTests`**, and `DEF-109`'s row records d3 did NOT fire on
  `Acmp.Api.Tests` — a re-run there is not a third override, but **record the occurrence FIRST**.

- ⚠⚠⚠ **PARSE THE JSON; NEVER REGEX A JSONL ROW.** `[^}]*` stops at the first `}`, so a row with nested
  `custom_attributes` is **silently deleted from the result**, not undercounted (the **FORTY-FIRST**).
- ⭐⭐ **SWEEP BEFORE THE INTERVIEW, NEVER AFTER** (`LL-005`) — it has now produced rulings the agenda did
  not contain **three** times, twice catching the agent's own gaps. ⚠ **`Open`/`Proposed` ≠ never-ruled**
  (`LL-006`) — and a **progress entry is a ruling's record too**, not just DEC/ADR.
- ⚠ **`G-TRACE` needs THREE legs** for a new `mvp=1` requirement (trap 16b). ⚠ `verification_method` is a
  CHECK; `verified_by` ∈ `human|agent|ci`; approving a lesson needs `"operator_confirm": true` **plus
  byte-identical content**; trace edges use `from_id`/`to_id`. ⚠ `entity_upsert` needs FULL rows — NOT NULL
  is evaluated before conflict resolution — but **nullable fields are preserved by omission**.
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
  superseded** (`AC-147`). ⛔ **Never `PageSize.Clamp` an export.** ⚠ **`DEF-109`**: append an occurrence,
  don't re-run a red into silence.
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
  ⛔ `DEF-087` untouched. ⚠ **Measuring inside the set you hold is not measuring the register** (said 4; 8).
★★ [**Durable rules from batches 13–21**](batches-13-21-durable-rules.md) — `Met`-verdict scope, the
enforcing-mechanism trap, never leave a Pending AC, Hangfire process-globals, union coverage, `$?` after
a pipe, and production's reconciled state.

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
- [Localhost CI hides load races](localhost-ci-hides-load-races.md) · [Git push hang → `gh auth setup-git`](git-push-hang-fix.md) · [Run CI gates locally pre-push](ci-gates-run-locally-pre-push.md) · [Always stage .claude/memory in commits](always-stage-claude-memory-in-commits.md)
- [Coverage & E2E mandate](coverage-and-e2e-mandate.md) — ≥95% FE+BE + adversarial E2E. ⚠ Playwright is **NOT UAT-only** (7 services + real Keycloak per PR) **but runs `KEYCLOAK_ADMIN_ENABLED=false`**, so it never touches the ADR-0038 write path.
- [E2E local run (non-destructive)](e2e-local-run-nondestructive.md) — **`-p acmpe2e` ONLY**, never `npm run e2e:up`. · [Dev-stack rebuild pitfall](dev-stack-rebuild-pitfall.md) — **never `up --build`** the long-lived dev stack.
- [Exact design fidelity + visual loop](exact-design-fidelity-visual-loop.md) · [A green suite is not a look](a-green-suite-is-not-a-look.md) — ⚠ the throwaway harness must import **only** the stylesheets the real route imports.
- [Design: breadcrumb spacing](breadcrumb-spacing-rule.md) · [i18n parity ≠ completeness](i18n-parity-not-completeness.md) · [Web visual-verify cache busting](web-visual-verify-cache-busting.md)
- ⚠ **`.adm-detail-card` has no padding and clips its children** — a popover needs `.adm-card-overflow`. · **`userEvent.setup()` installs its own clipboard stub** — define a clipboard spy *after* it.
- [User prefers simple English](user-prefers-simple-english.md) · [Phase prompt Standard Footer](phase-prompt-standard-footer.md) · [Install the schedule, not just the daemon](install-the-schedule-not-just-the-daemon.md) · [Arabic rename is a grammar rule](arabic-rename-grammar-not-substitution.md) · [A clean scan must prove it had a subject](scan-must-prove-it-had-a-subject.md) · [Guard the property, not the value](guard-the-property-not-the-value.md) · [The suite assumed a fresh database](e2e-assumes-a-fresh-database.md) · [The feature is often already half-built](check-before-building.md)
- ⚠ **AC id cells in markdown tables must stay BARE** (`| AC-001 |`) — bolding breaks the G-PROGRESS gate. · **A new advisory can turn `main` red with no code change** (`GHSA-q939-rpr3-3284`). · **A compose `secrets:` entry whose FILE IS MISSING fails the WHOLE stack** — write mounted secrets unconditionally.

## ⚠ Unlinked topic files + the completed ladder

**Named only so they are findable; current value NOT vouched for**: `absence-claims-need-untruncated-search`
· `ask-every-time-never-bank-answers` · `audit-slice-literal-ac017` · `body-assertions-miss-the-envelope` ·
`package-mechanics-proven-2026-08-18` · `reconciliation-and-voting-eligibility` ·
`substring-checks-bind-to-prose` · `topic-prepare-ui-gap-d15` · `wbs233-csp-spike` · `wbs234-reclassify` ·
`webex-coverage-gate-async-exclusion`. ⚠ **Re-run the inbound-link check after ANY compaction**
(2026-08-29: 25 orphans, all `p*`/`keystone-*`/`ph5-*` ladder files — no non-ladder file orphaned).
**The completed ladder P1–P19 + PH-5** lives in those `p*`/`keystone-*`/`ph5-*` files, all superseded by
the package's slice rows — `ls` the directory when you need one. ⛔ Do not re-open them.
