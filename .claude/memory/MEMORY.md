# Memory Index — ACMP

> One line per entry; detail lives in topic files and the package. Read the linked file before acting.
> ⚠⚠ **TWO CEILINGS, BOTH MEASURED: 200 LINES and ~24.4K CHARACTERS (not bytes — `wc -c` over-reads this emoji-heavy file by ~4%)** — past either the loader drops the tail SILENTLY; hit 2026-09-07 at ~26,000 chars. ⭐ Check with `wc -l` and `python -c "import io;print(len(io.open('.claude/memory/MEMORY.md',encoding='utf-8').read()))"` before and after every edit; keep under ~140 lines / 24,000 chars.


## ★★★ 2026-09-07 (later) · `DEC-141` · `SL-038` CLOSED through the slate route · `LL-066` supersedes `LL-064`

- ⭐⭐ **THE MANDATED VERDICT ROUTE WORKS END TO END** (`DEC-141` d1): export EVERY family the generator names in ONE pass → `package_verify()` digest == exports' digest → `gen-slice-review-slate` (+ the other Approved slice's *nothing at Review* refusal as the CONTROL) → operator rules → full-row flips with `expect_unchanged` on `title, phase_id, slice_id, effort, source_kind, source_span`. ⚠ **An OMITTED guarded column REFUSES THE WHOLE BATCH** — calibrated by accident (2 rows omitted 3 short columns → rolled back), which also PROVED the 4–9 KB pasted titles byte-identical. ⭐ **`gen-record-slate` indexes EVERY file in `exports/` ⇒ delete stale exports of OTHER families first** or it refuses the mixed digest. ⛔ Offer no verdict and no "(Recommended)" on the operator's own questions (`DEC-140` d3).
- ⭐⭐ **`DEC-142`/`DEC-143`: THE SAMPLING HARNESS EXISTS AND IS CALIBRATED** — `sql-startup-sampling.yml` (dispatch-only; `arm` × `contention`), `scripts/sql-startup-sample.sh`. ⛔ **Alone on the runner the FTS image gave 0/120; with the 2022 backstop BOOTING ALONGSIDE, 4/120** = CI's rate — `LL-060` in one pair: contention is the channel. Stock 2025 without FTS crashed with `DEF-130`'s exact frames ⇒ FTS not necessary. 2022 pin 0/120 is only p=0.06 — a claim needs the TWO-ARM p, not P(0/N). ⭐ **`LL-067`**: the control was the COUNT (453 pre-move runs). ⛔ **`LL-068`: before `checkout -b`, `git rev-list --left-right --count @{u}...HEAD` must be `0 0`** — a squash carries unpushed base commits. Local Git Bash: `MSYS_NO_PATHCONV=1`. Resolve state from `DEC-143` onward, never here.

## ★★★ 2026-09-07 · `DEC-139`/`DEC-140` · `entity_export` · `WBS-31` SHIPPED · `DW-100` **DONE** · `SKL-001`

⭐⭐⭐ **`SKL-001` `trusting-a-measurement` EXISTS — `.claude/skills/trusting-a-measurement/SKILL.md`, OPERATOR-OWNED, never regenerate it.** 23 lessons **Promoted** and gone from the always-loaded note (`entity_query("lesson", status="Promoted")` lists them) — **if you want one of those, it is IN THE SKILL, not in the note.** Note 58→35→**37** lines vs a ceiling of 20 (`LL-064`/`LL-065` added 2); `skill-promote.md` is re-runnable and 2 clusters remain (`PE-938`). ⚠ **`Approved`→`Promoted` needs EVERY content column byte-identical and an OMITTED column reads as DRIFT** — but `trg_lessons_immutable` ABORTS on drift, so `ok:true` IS the proof (calibrate it first).

⛔⛔⛔ **ALL PACKAGE READS *AND* WRITES GO THROUGH THE `tamheed` MCP TOOLS — NO EXCEPTIONS** (`DEC-135` d1, an OVERRIDE). **Never open/`cat`/`grep`/`wc -l` a `tamheed-package/data/*.jsonl`** — 78 ad-hoc scripts deleted (`PE-897`/`PE-898`). ⭐⭐⭐ **RESOLVED 2026-09-07 BY tamheed 4.7.0 + `DEC-139` d1: a committed script reads `tamheed-package/exports/<family>.json`, written by the MCP tool `entity_export` — that SATISFIES d1 (the tool does the store read), it is not an exception.** All 4 `gen-*.mjs` rebuilt onto it (`WBS-31`, PR #372 → `1fdb33c0`); shared reader `scripts/lib/package-export.mjs`; `DW-100` **Done**. ⛔ `count-prompt-ids.py` still reads JSONL — out of scope by `DEC-138` d3. ⚠ **`exports/` is GITIGNORED** — export immediately before generating, never reuse. ⛔ **Per-item verdicts are the OPERATOR's (`DEC-079` d3) and are now TAKEABLE again — resolve which rows are at `Review` from the register, never from this line.**
⭐⭐ **`entity_query` TRUNCATES NO FIELD** — `tamheed_server.py:993-1076`, `SELECT {cols}`→`dict(zip(...))`, **read in source** (`PE-910`). **TRAP 13 IS REVERSED**; `LL-045`→**`LL-063`**. ⭐⭐⭐ **HOW: `DW-100` AND `DEC-135` d1 BOTH CALLED IT UNTESTABLE — both framed the instruments as *OUTPUTS to compare*, so the CODE THAT PRODUCES one was invisible.** Ask that whenever a premise looks circular. ⚠ JSONL flushes on EVERY write (`AGENTS.md` line 38 fixed). ⭐⭐ **TWO GUARDS FROM EARLIER SESSIONS CAUGHT ME IN ONE HOUR:** `DEC-137` d4 (*`decision_ref` == `amends` target ⇒ provenance, not amendment*) refused my `SC-051`→`DEC-138` edge; and **`trg_lessons_immutable` ABORTS any content drift on an `Approved` lesson ⇒ a supersession paste is SELF-VERIFYING** (calibrated with a corrupt title — rolled back atomically). ⛔ **`deferred_work`/`wbs_items`/`scope_changes` have NO such trigger** — `DEF-127`'s register. ⭐⭐⭐ **4.7.0 FIXES THAT: `expect_unchanged: [cols]` on an `entity_upsert` item REFUSES the write if those columns differ from stored ⇒ a long-row status flip is SELF-VERIFYING. An OMITTED named column counts as CHANGED. CALIBRATE IT (I did: `effort` M→L was refused, batch rolled back).** ⭐ Cheaper still: **omit a nullable column entirely and it is preserved untouched** — no transit, no risk.
- ⭐⭐⭐ **`DEC-140`: A GREEN `lessons-confirmed` CAN MEAN *NOTHING WAS RECORDED*** — it counts `Proposed` rows, so it goes green precisely when you filed no lesson. ⭐ **`entity_query("lesson", status="Proposed")` + a `Promoted` control is the real check.** `LL-064` (a ruling and the prose it falsifies ship in the SAME commit — the fix is ORDERING, sweep before committing the ruling; **superseded 2026-09-07 by `LL-066`, which keeps that rule and adds a SECOND sweep keyed on the SHAPE of an absence-claim — `no |none|nothing|names no|not scheduled|deliberately` — read for what each hit DENIES**) and `LL-065` (a PROVEN premise argues FEASIBILITY, never DESIRABILITY) Approved unpinned; `DW-101` files two readiness gates that pass because their subject is missing (5 defects w/ null `found_in`, `AC-109`–`112` unbound). ⛔ **`SL-035` is BUILD-COMPLETE, held by `DEF-121`+`DEF-130` alone.**
- ⚠⚠⚠ **THE DIGEST IS PACKAGE-WIDE ⇒ *ANY* WRITE AFTER GENERATING A SLATE INVALIDATES ITS CURRENCY CLAIM — AND `progress_update` IS SUCH A WRITE** (`PE-949`, measured on first use). ⭐ **Do EVERY package write first — rows, progress, `work_bind`+flush — then export, then generate, then hand over.** ⛔ **Never "fix" it by narrowing the digest to the families a slate reads**: the check would pass while a register the slate QUOTES changed underneath.
- ⭐⭐ **`ids` + `search` COMPOSE ⇒ a substring test scoped to ONE row:** `entity_query(t, ids=["X"], search="phrase")` → 1/0. Used it as a **paragraph-level** survival check on a 4,405-char re-send (7 phrases, 1 per paragraph — trap 14a's real failure is a vanished paragraph, not a short total); control differed by **one word** and returned 0.

## ★★★ 2026-09-05 · `DEC-134` · `WBS-29` capture shipped · `PERMISSIONS.md` DELETED · `DEF-141`

⛔⛔ **`.claude/PERMISSIONS.md` NO LONGER EXISTS** (`DEC-134` d3 / `DEF-141`): 4 false claims incl. *"the
allowlist is not consulted"* — what `DEC-129` d1 closed `DEF-136` for. **`permission-prompts-four-causes.md` is now the only account.**
★★★ ⚠⚠ **`readiness_check` ON A CLOSED PHASE RETURNS `ready:false` ON `wbs-done` — BOOKKEEPING, NOT A
PROBLEM** (`PH-1` fails on `WBS-1..16`): the `P1`–`P19` tree ran via SLICE rows and was never flipped.
**`DEC-134` d2 = leave and record; `PE-886` is the record.** ⛔ Don't "repair" — `LL-038`'s load-bearing
tell is UNIFORMITY and nobody measured whether that status does work.
- ⭐⭐ **AN INSTRUMENT CAN BE HALF-UNCONDITIONAL AND STILL READ AS AN INSTRUMENT.** `e2e`'s CLIENT capture
  was `!cancelled()`, its SERVER capture `failure()` — so a **flaked-GREEN** run uploaded traces and no
  Keycloak. That is every `DEF-129` occurrence. `WBS-29` (#370 → `32efd525`) fixed it. ⭐ **Bar = DELIVERY,
  not execution:** artefact 5,354 B, `keycloak.log` **50 lines**, on a GREEN run.
- ⚠⚠ **`work_bind`/`progress_update`/`export_html` WRITE *AFTER* THE COMMIT THEY RECORD** (`LL-061`) — dirty again exactly when you `git checkout -b`. ⭐ **`git status --porcelain -uall` right before branching**; a memory of committing is not the check. ⚠ **Clean tree ≠ pushed**: `git fetch -q` then `git rev-list --left-right --count @{u}...HEAD`.

## ★★★ 2026-09-05 (earlier) · `DEF-136`/`DEF-138`/`DEF-140` closed · `LL-056`–`LL-060`

★★★ ⭐⭐ **READS OUTSIDE THE WORKING DIR PROMPT IN *EVERY* MODE, BYPASS INCLUDED** —
`blockReadsOutsideWorkingDirectories` is ON at user scope. **By design; never file it as a defect.**
`DEF-136` closed: **the allowlist WAS working** — [[a-valid-key-can-be-inert]] (`LL-058`/`LL-059`).
⚠ **The project allowlist is now `{}`** (operator emptied it, `313e8ff9`) — more prompts, not a fault.
★★★ ⛔⛔ **ADVICE CAN BE RIGHT WHILE ITS MECHANISM IS FALSE — the mechanism is what you generalise from**
(`LL-056`, pinned): **5 instances in one day**, then two more on 09-05.
★★★ [**AN INSTRUMENT MUST DELIVER, NOT JUST FIRE**](an-instrument-must-deliver-not-just-fire.md) —
**`LL-060`, PINNED. Read before building ANY detector, before trusting a calibration, and before calling
an artefact empty.** To watch an instrument you add a channel and that channel is often what production
lacks; **a `trace.zip` holds TWO streams and I merged them**; `DEF-140`'s cause was `archive.ubuntu.com`,
absent from the Dockerfile; **`DEF-129` is the guest's `POST …/token` returning `status=-1`** — neither
of its two standing remedies. ⭐ *What did I change in order to watch this, and does production have it?*
- ⛔ **MediatR 12.5.0 does NOT drop the token** (`PE-875`): the new `CancellationToken` param is defaulted; only test *lambdas* break — arity at CONSTRUCTION, invisible at INVOCATION. ⛔ `DW-099` corrected: the suite's 72/445 ARE a red-detector for lost isolation.
- ⚠⚠ **BACKEND DURATION IS A SIGNATURE:** ~1m34s = Format check; ~5m = healthy; 11–18m = `DEF-140`. ⭐ Run the gate's OWN command BEFORE pushing — `dotnet format acmp.sln --verify-no-changes --no-restore`. **Write-created `.cs` files carry NO BOM ⇒ `error CHARSET`.** ⛔ JSON admits no comment; XML forbids `--` inside one.
- ⭐⭐ **DEPENDENCY PRs: COLOUR AND CAUSE POINT OPPOSITE WAYS** (`PE-856`/`PE-867`) — a "risky major" was a REPAIR (`DEF-138`). ⭐ `@dependabot rebase` is silently ignored ⇒ `gh pr update-branch`. ⚠ `gh pr checks --watch` right after a push exits 1 — use `gh run watch <id>`.
- ⭐⭐ **`WBS-27.2`** (`LL-057`, pinned): the expensive thing was the HOST — 287 constructions → 47, backend 9m → 5m36s. ⛔ Claims NOTHING about `DEF-109`. ⚠⚠ **C31 FIRES ON *COMMITTED* WRITES TOO** — `--delete-branch` took a branch-only package commit; **safe = REACHABLE FROM A SURVIVING REF.**

## ★★★ 2026-09-03 · `DEF-109` occ 6: SCHEDULING REFUTED · `DEC-123`–`DEC-125` · `LL-055`

★★★ [**A control proves FIRING, never COUPLING**](a-control-proves-firing-not-coupling.md) — the watchdog's trigger measured whether the PROCESS was scheduled; occ 6 showed drift **under 3 ms** while 18 requests burned 100-second ceilings, so it could never fire. ⛔ I then committed the same fault **twice inside the fix**, 40 min after filing it. ⚠ **Its rule now lives in `SKL-001` step 4 (`LL-055`); the topic file keeps the worked case.**
⭐ **ASK the operator what they OBSERVE** — four times now one word has settled what inference could not.
- ⭐⭐⭐ **`DEF-109` occ 6 left the FIRST artefact in six occurrences** (CI `33765425613`): 204 heartbeats,
  ZERO snapshots, `windowMaxPending` never climbing ⇒ **thread-pool starvation REFUTED**, deadlock survives.
  ⛔ Still NOT clause (2) — an elimination is not an identification.
- ⛔⛔ **`AC-126` ASSERTS FULL-HISTORY SCANNING** (`fetch-depth: 0`) in its own `Then` clause — narrowing the
  gitleaks scan falsifies a `Met` AC and needs an `SC-`+ADR. `DEF-132` checked that AC and cleared it against
  the *allowlist-shape* clause: a clean confirming answer about the wrong clause (`LL-046`).
- ⚠⚠ **`secrets` durations are NOT monotonic** (5m34s…10m45s) and now sit **at or over the old 10-min bound on most runs** — `DEC-123` d1's raise to 30 was necessary, not precautionary.
- ⚠⚠ **xUnit 2.5.3: same collection ⇒ never parallel.** `IClassFixture` creates NO collection (safe);
  `ICollectionFixture` does ⇒ cross-class sharing **serialises the suite** (`DEC-124`). ⭐ **The test CLASS
  is constructed per TEST** — that ctor is the per-test hook even when the fixture is per-class.
  ⚠ **`CreateDefaultClient` is NOT virtual**; use an `IStartupFilter` for a suite-wide seam.

## ★★★ 2026-09-02 · `DEF-109` diagnosed · `DEF-121`: memory pressure REFUTED, clause (2) still unmet · `DEC-111`–`DEC-116`

⛔⛔ **NEVER WRITE A CI COLOUR INTO DURABLE PROSE** — `gh run list --branch main` is the answer; cite a RUN ID (`LL-036`).
★★★ [**`DEF-109`: the HOST is the unit that leaks**](def109-the-host-is-the-unit-that-leaks.md) — read before any memory/perf investigation or trusting `gcroot`; 137 MB over 20 hosts vs 8 MB over 1; `DEC-120` activated `DW-096`→`WBS-27.2`.
★★★ [**AN INSTRUMENT MUST REPORT ON ITSELF**](an-instrument-must-report-on-itself.md) · [**READ THE ARTEFACT, NOT THE ENTRY ABOUT IT**](read-the-artefact-not-the-entry-about-it.md) — memory pressure REFUTED from files the capture KEPT; clause (2) STILL unmet (an elimination is not an identification).
- ⛔⛔ **THE HOSTED RUNNER IS ~16 GB, NOT 7** (`PE-785`): every MEASURED `DEF-109` claim survives, the causal BRIDGE to the CI symptom does not (`LL-020`). ⚠ Pass Write/Edit a REPO-RELATIVE path; scratch `.scratch/<id>/`; memory dir = a junction.
- ⛔⛔ **A QUESTION'S OPTIONS ARE UNVERIFIED PROSE WEARING THE SLATE'S AUTHORITY** (`LL-051`); **`LL-052`, PINNED:** a file's NAME and FIRST SCREEN describe its FORMAT, never its content. ⭐ Ask what a NEGATIVE would mean before the experiment (`LL-047`); a root-path tool names A path, never THE cause (`LL-048`); a measurement AFTER the action it gates is a report (`LL-049`); sweep the decision register for a row's id before calling its numbers stale (`LL-050`).
- ⚠⚠ **COMMIT PACKAGE WRITES *BEFORE* `git checkout -b`**; **`strict: true`: ANY push to `main` stales every open PR** — path-ignore stops the RUN, not the staleness. ⭐ Ryuk doesn't reap first: `docker cp`. ⚠ Discriminate a red by SIGNATURE — `DEF-121` `ContainerNotRunningException` vs `DEF-109` `TaskCanceledException`.
- ⛔⛔ **NEVER NAME A SLICE ID OR DESCRIBE AN ITEM IN `prm-next.md`** (46th, 48th) — **a briefing on HOW is a description too.** ⚠ **`ADR-0045`**: where a `.dc.html` cannot satisfy `INV-014`'s px AND `AA`, AA governs.

## ★★★ 2026-08-31 · CI attribution · activations are not agreement

★★★ [**CI run attribution · `skipped` · probability-remedies · `DEF-121` · the image gate**](ci-run-attribution-and-probability-remedies.md) — read before recording anything about CI or proposing a fix to an intermittent failure. ⚠⚠ **Activations routinely OVERRIDE the agent's recommendation to carry** — never read one as agreement about HOW.
- ⚠⚠⚠ **A PR-HEAD RUN AND A MERGE-COMMIT RUN ARE DIFFERENT RUNS OVER IDENTICAL CODE** (`LL-036`); `gh pr checks` shows only the PR one. **Cite the RUN ID, never a colour.** ⚠⚠ **`skipped` CONFLATES *`if:` was false* WITH *a `needs:` job failed*** (`LL-039`). ⭐⭐ A remedy reducing a PROBABILITY cannot be falsified by recurrence (`LL-035`). ⭐ A re-run samples every OTHER question (`LL-037`).
- ⚠⚠ **A LIFECYCLE STATUS CAN BE LOAD-BEARING, NOT LAGGING** (`LL-038`; tell = uniformity). ⭐⭐ **A progress entry is a ruling's record too — sweep those, not just DEC/ADR.** ⚠⚠⚠ **PARSE THE JSON; NEVER REGEX A JSONL ROW** — `[^}]*` silently DELETES rows with nested `custom_attributes` (41st).
- ⭐⭐ **SWEEP BEFORE THE INTERVIEW, NEVER AFTER** (`LL-005`) — has dissolved items 4× and removed an OPTION from inside a row. ⚠ `Open` ≠ never-ruled. ⭐⭐⭐ **A TARGETED SWEEP FINDS CLAIMS ABOUT WHAT YOU *CHANGED*; ONLY A FULL READ FINDS CLAIMS ABOUT WHAT *REMAINS*.** ⚠⚠ A pointer at a FINISHED slice returns a clean answer about the wrong subject — name no slice id in durable prose. ⚠ `PH-3`/`PH-7` are `Approved`, not closed.
- ⚠ **`G-TRACE` needs THREE legs** for a new `mvp=1` requirement (trap 16b). `verification_method` is a CHECK; `verified_by` ∈ `human|agent|ci`; approving a lesson needs `"operator_confirm": true` plus byte-identical content; trace edges use `from_id`/`to_id`. `entity_upsert` needs FULL rows (NOT NULL evaluated before conflict resolution) but **nullable fields are preserved by omission**; build payloads from `entity_query`, never the JSONL (`DEC-135` d1).
- ⭐⭐⭐ **`LL-032` (pinned): a fixture that is the LIVE REGISTER changes meaning when somebody does ordinary work, and the dangerous outcome is the PASS.** ⛔ NEVER carry `security-controls.md` §20's *"L2 is met across all applicable chapters"*. ⚠⚠ **`jq` IS NOT INSTALLED** — use `gh --jq`. ⭐ **Emit on EVERY terminal conclusion** — `cancelled` is a third one (`DEF-132`).
- ⭐⭐ Read the row's own text, then sweep the NARRATIVE docs and the ADR/decision/OQ registers by keyword before sizing (`LL-008`, `LL-025`). ⚠⚠ A defence layer can be invisible to any front-door test (`LL-030`). ⚠⚠ A refactor can cross the per-file coverage floor with no new untested line (`LL-031`) — **RUN THE GATE, NOT THE TESTS**; never lower `ADR-0016`'s 95%; coverage excludes `tests/`.
- ⛔⛔ **A RED FROM `SearchProvidersFtsTests` IS REAL — STOP, DO NOT RE-RUN** (`DEC-077` d3, overridden TWICE; a third reopens the rule). It names that test ONLY. ⚠⚠ **`scripts/**` and `.gitignore` are NOT path-ignored** — PR route; **poll CI after ANY push to `main`**. ⚠ **`DEF-107`: approving+pinning a lesson does NOT bind it** — `handoff_emit` in the SAME batch.
- ⛔ **`SEC-080` asserts a legal hold overrides any purge and NO HOLD MECHANISM EXISTS** (`OQ-080`). ⚠ **Approved ACs are IMMUTABLE** (`AC-147`). ⛔ Never `PageSize.Clamp` an export. ⚠ `DW-088`: `TopicDetail`'s download button is hardcoded `disabled`.
- ⭐ **Instruments to USE, not re-derive:** `coverage-triage` · `gen-lesson-docket` · `gen-slice-review-slate` · `gen-record-slate` · `check-image-contract` · `check-asvs-pack-paths` · `gen-dw-disposition-slate` · `count-prompt-ids.py` · `number-render-scan`. The four `gen-*.mjs` read `exports/` via `scripts/lib/package-export.mjs` — `entity_export` each family FIRST or they fail closed and print the call.
- ⚠⚠⚠ [**AN ID IS A POINTER, NOT A REFERENCE**](an-id-is-a-pointer-not-a-reference.md) — operator refused an interview over it (`LL-011`, pinned). `G-IDS` checks FKs, not ids in prose. ⚠⚠ A requirement's status and its `DW-` row's are UNRELATED columns.
★★ 2026-08-20 disposition session: [**batches 13–21**](batches-13-21-durable-rules.md) · [**`SL-034`**](sl034-slate-generator-and-asvs-pack.md) · [**`SL-033`**](sl033-slice-findings.md) · [**`DW-082`**](dw082-sweep-and-vitest4.md)

## Earlier 2026-08 — durable findings only

- ★★★ [**`DEF-078`: a green control can be blind**](a-green-control-can-be-blind.md) — a healthcheck evaluating ZERO checks; gitleaks passing over an allowlist exempting every markdown file. ⚠ Read `ADR-0043`, **not** `ADR-0042`. · ★★ [**An absence needs a proven instrument**](an-absence-needs-a-proven-instrument.md)
- ⚠⚠ [**v4 store + 4.4.x mechanics**](tamheed-v4-and-liveness.md) — ⛔ its *"build payloads from the JSONL"* is **SUPERSEDED by `DEC-135` d1**; `WVR-` operator-only.
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
- [Commit package writes before git ops](commit-package-writes-before-git-ops.md) · [Tamheed stale .lock + PID reuse](tamheed-stale-lock-pid-reuse.md) · [Tamheed data repair](tamheed-data-repair.md) · [migration history](tamheed-migration-reverted.md) · [Localhost CI hides load races](localhost-ci-hides-load-races.md) · [Git push hang → `gh auth setup-git`](git-push-hang-fix.md) · [CI gates locally pre-push](ci-gates-run-locally-pre-push.md) · [Always stage .claude/memory](always-stage-claude-memory-in-commits.md)
- [Coverage & E2E mandate](coverage-and-e2e-mandate.md) — ≥95% FE+BE + adversarial E2E. ⚠ Playwright is **NOT UAT-only** (7 services + real Keycloak per PR) **but runs `KEYCLOAK_ADMIN_ENABLED=false`**, so it never touches the ADR-0038 write path.
- [E2E local run (non-destructive)](e2e-local-run-nondestructive.md) — **`-p acmpe2e` ONLY**, never `npm run e2e:up`. · [Dev-stack rebuild pitfall](dev-stack-rebuild-pitfall.md) — **never `up --build`** the long-lived dev stack.
- [Exact design fidelity + visual loop](exact-design-fidelity-visual-loop.md) · [A green suite is not a look](a-green-suite-is-not-a-look.md) (⚠ the throwaway harness must import **only** the stylesheets the real route imports) · [breadcrumb spacing](breadcrumb-spacing-rule.md) · [i18n parity ≠ completeness](i18n-parity-not-completeness.md) · [visual-verify cache busting](web-visual-verify-cache-busting.md)
- ⚠ **`.adm-detail-card` has no padding and clips its children** — a popover needs `.adm-card-overflow`. · **`userEvent.setup()` installs its own clipboard stub** — define a clipboard spy *after* it.
- [User prefers simple English](user-prefers-simple-english.md) · [Phase prompt Standard Footer](phase-prompt-standard-footer.md) · [Install the schedule, not just the daemon](install-the-schedule-not-just-the-daemon.md) · [Arabic rename is a grammar rule](arabic-rename-grammar-not-substitution.md) · [A clean scan must prove it had a subject](scan-must-prove-it-had-a-subject.md) · [Guard the property, not the value](guard-the-property-not-the-value.md) · [The suite assumed a fresh database](e2e-assumes-a-fresh-database.md) · [The feature is often already half-built](check-before-building.md)
- ⚠ **AC id cells in markdown tables must stay BARE** (`| AC-001 |`) — bolding breaks the G-PROGRESS gate. · **A new advisory can turn `main` red with no code change** (`GHSA-q939-rpr3-3284`). · **A compose `secrets:` entry whose FILE IS MISSING fails the WHOLE stack** — write mounted secrets unconditionally.

## ⚠ Unlinked topic files (`p*`/`keystone-*`/`ph5-*`) = the completed ladder P1–P19 + PH-5, superseded by the slice rows. ⛔ Do not re-open.
