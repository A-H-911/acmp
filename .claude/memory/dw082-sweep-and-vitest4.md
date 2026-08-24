---
name: dw082-sweep-and-vitest4
description: The 2026-08-23..25 arc — the Dependabot sweep, DW-082 (coverage-v8 v4 exposing untested inline handlers), and the findings each produced.
metadata:
  type: project
---

# The `DW-082` / sweep arc (2026-08-23 → 2026-08-25)

> Detail moved out of `MEMORY.md` on 2026-08-25 because the index had grown past its
> 200-line read limit and was silently dropping its own tail. The live carried list is
> `tamheed-package/prompts/prm-next.md` §6 — this file is the memory-side summary only.


> ▶▶ **NEXT: the `DW-082` handler tests**, branch **`chore/vitest-4-pair`**. `DEC-075` d2:
> **ALL FOUR** activated streams are in scope (operator said "all"); the ORDER is the agent's
> recommendation only — tests → `SL-033`/`WBS-24.1` → `DW-080` alone → `DW-079`.
> ⚠ **NEVER write the remaining count here — MEASURE it:** `npm run test:cov -- --coverage.reporter=json
> --coverage.reporter=json-summary --coverage.reporter=text` then `node scripts/coverage-triage.mjs`
> (committed 2026-08-25; prints each uncovered line's SOURCE TEXT so causes are confirmed, not assumed).
> ⚠⚠⚠ **THE OLD "`TopBar` IS NOT A HANDLER FIX / needs its own decision" WARNING WAS FALSE AND IS
> WITHDRAWN** (`PE-614`). All seven of its uncovered lines are inline handlers; the `lazy()` decl records
> 3 hits and the `{DevRoleSwitcher && …}` call site has **no statement starting on it**, so it never
> enters the line metric. ⭐ **It came from version one of the triage script misreading the ASCII table's
> compressed `92-138` as a 47-line range. Version two fixed the NUMBER; nobody re-derived the CAUSE built
> on the wrong number.** **A corrected measurement does not correct the inference someone built on it.**
> ⚠ **NEVER lower `ADR-0016`'s threshold** — the number would now
> hide real untested code. ✅ `DW-078` **Done** (8/10, `#308` merged, `#135` closed). ✅ `DEF-105` fixed:
> **`main` IS BRANCH-PROTECTED** — 9 checks, `strict=true`, `enforce_admins=false`. ⚠ **Every push to
> `main` re-stales every open PR**; push package writes BETWEEN merge cycles, never during one.
> ⚠⚠ **DO PACKAGE WRITES ON `main`** — C31: checking out a feature branch rolls `data/` BACKWARDS, and
> the store refused a write for exactly that reason today (the staleness check earned its keep).
> ⚠ `LL-017/018/019` are **`Proposed`** — `lessons-confirmed` FAILS on purpose; do not approve unread.

- ⚠⚠⚠ **THE RESUME'S COUNTER FAILED IN A NEW WAY: I CORRECTED A STATEMENT AND FORGOT TO COUNT IT.**
  The `#135` mis-diagnosis was withdrawn (`PE-606`) and its §6 bullet rewritten — but the counter stayed
  at seventeen with no ordinal written, so the file kept the fix and lost the tally. Now **eighteen /
  six escapes**. ⭐ **Found by grepping for `EIGHTEENTH` and getting ZERO, not by reading.**
  **A correction that does not update the tally of corrections is half a correction — they are one edit.**
- ⭐⭐ **NAME THE PART YOU DID NOT CHECK, IN THE ARTIFACT ITSELF.** This pass verified all 217 ids
  mechanically and re-read §6 + the operator-owned list, but **not §2–§5**. The file now says so, so the
  next session reads them instead of inheriting an unearned assurance.
- ⚠⚠ **`DW-083`: TRAP 28 GUARDS VISUAL-VERIFY *FILES* AND MISSES THE *PROCESSES*.** Six `vite` servers
  ran for 13 days holding `rolldown-binding...node` open — `npm ci` failed `EPERM`/`EBUSY` and, because it
  unlinks first, **gutted `node_modules` 171→35**. A file-level check runs clean over that machine.
  ⭐ **If `npm ci` fails on a `node_modules` file here, enumerate node processes FIRST.**

> ▶▶ **NEXT: `WBS-24.1` / `DW-033` / `FR-032`** — configurable backlog columns (show/hide, reorder);
> verified unbuilt, the single missing member of a family whose other four views shipped. `SL-033` holds
> **eight** rows in `DEC-071` d1's order, `DW-028` LAST because it is the only one adding an
> authorization surface. `prm-next.md` §6 has the table. ⚠⚠ **ONE SLICE OF EIGHT IS AN OPERATOR OVERRIDE**
> (`DEC-071` d3) — I recommended three. Each row still gets its OWN AC in the batch producing its
> evidence. ⚠ **`PH-7` IS NOT CLOSED**; the release close-out is still unscheduled, so
> `DW-041`/`DW-067` have **not** fired. ⚠⚠ **`DW-071` HAS** — `DEC-072` d2, see below.
>
> ⚠⚠ **A SECOND INTERVIEW (`DEC-072`, `SC-032`) ADDED THREE ACTIVATED ROWS — TWO ARE OVERRIDES.**
> **`DW-078`** sweep the WHOLE Dependabot queue, majors included. ⚠⚠⚠ **I WROTE "13 PRs" FROM A COMMAND
> I HAD CAPPED AT `--limit 10`. It is TWELVE, 3 routine / 9 majors** (`PE-599`). ⚠⚠ **THE CAP COST A
> JUDGEMENT: the two hidden rows, #128/#134 `dotnet` 8→10, both edit `deploy/Dockerfile.backend` — so
> `DW-066`'s trigger HAS fired, after I reasoned in the same session that it had not.** A truncated
> instrument deletes the evidence that would have changed your answer, from the end you never think about.
> ✅ **`DEC-074`/`SC-033`: the `dotnet` pair is CARVED OUT to `DW-080` (runtime migration, not a bump), so
> the sweep is TEN PRs; and `DW-066` is `Activated` and BOUND to `DW-080` — the alpine move rides with the
> `FROM` edit as ONE base-image decision. ⚠ musl is the risk, not the edit: full e2e + Arabic FREETEXT.**
> ⚠⚠⚠ **LL-001 CAUGHT IN THE ACT BY ITS OWN CONTROL:** re-sending `DW-066`'s 2317-char title (NOT NULL,
> so a full-row replace must carry it) came back **2315** — I had dropped the `⚠` off *"THIS MUST GO THROUGH
> THE FULL e2e LEG"*, the one sentence saying a green suite proves nothing. **Two characters, highest-value
> sentence, invisible to every gate.** ⭐ **Take the sha256 pre-image BEFORE the write, every time.**
> ⭐ **Hash-and-verify when you must PRESERVE; rewrite openly when you must CHANGE. The dangerous middle is
> re-typing a long field in order to keep it the same** — `DW-078`'s 3933-char title was rewritten instead.
> ⚠ **The sweep itself was an OVERRIDE** — I recommended splitting the majors out; the operator said
> sweep all (`DEC-072` d1). ⚠⚠ **NOT an `NFR-051` breach** — that requires Dependabot be *configured to
> ALERT*, which it is; open alerts are it WORKING. ⚠⚠ **`mssql` 2022→2025 CAN DESTROY THE FIVE DEV VOLUMES**
> — fresh-volume isolated project only.
> ✅✅ **SWEEP RUN 2026-08-23: 7 of 10 MERGED** (`#255 257 259 256 258 260 139`). ⚠ **`typescript` on `main`
> is now `~7.0.2`.** **`SL-033`/`WBS-24.1` IS UNBLOCKED — that is the next action now.**
> ⛔ **TWO BLOCKERS.** `#135` mssql 2025 — ⚠⚠⚠ **MY FIRST DIAGNOSIS WAS WRONG AND IS WITHDRAWN**
> (`PE-606`). I said *"the image won't boot; production must not move to 2025"*. **`ldd` shows NOTHING
> missing in EITHER image** — 2022=Ubuntu 22.04 has `liblber-2.5.so.0`, 2025=24.04 has `liblber.so.2`
> (OpenLDAP soname change). ⚠⚠ **`deploy/Dockerfile.sqlserver` HARDCODES the `ubuntu/22.04/
> mssql-server-2022` repo; `#135` bumps only the `FROM`** — a 22.04 FTS package on a 24.04 base.
> ⭐ **A PINNED BASE IMAGE AND A PINNED PACKAGE REPO ARE ONE DECISION IN TWO PLACES, AND ONLY ONE IS
> AUTOMATED** — dependabot can never make this PR green.
> ⚠⚠⚠ **HOW IT SURVIVED: `LL-009`, walked into hours after quoting it.** Testcontainers AND compose
> failed identically — but **both build the same Dockerfile**, so that was ONE instrument, not two.
> *Different runner, different orchestrator* FEELS like independence and is not.
> `#307`+`#137`+`#261` → **`DW-082`**: the vitest pair must move in ONE commit (each pins an exact peer on
> the other); every test PASSES and only `ADR-0016`'s coverage gate fails, over ~20 files **byte-identical
> to `main`** — so **`coverage-v8` v4 COUNTS LINES DIFFERENTLY**, trap 2 at scale. **Never lower the
> threshold to clear it.**
> ⭐⭐ **THE DISCOVERY GUARD (`total>=9`) SAVED THREE BROKEN POLLERS IN ONE AFTERNOON** — each fell through
> to a default and reported ZERO checks; the floor turned a silent false-PASS into a loud false-FAIL.
> ⚠⚠ **ROOT CAUSE IS ENVIRONMENTAL: multi-line `python` inside `$()` inside a heredoc-written bash script
> fails SILENTLY here — use ONE single-line `python -c` per metric.** ⚠ **An IN_PROGRESS check carries
> `conclusion == ""`, NOT null** — excluding only `None` marks every RUNNING check as failed.
> ⚠ **`npm ci` unlinks `node_modules` first; on Windows it can `EPERM` on a locked native binary and leave
> the tree gutted.** ⚠ **A stale RED is exactly as uninformative as a stale GREEN** — re-run before judging.
> **`DW-079`** the `NFR-018` ASVS L2 evidence pack (override; I said leave it). ⚠⚠ **It does NOT close
> `NFR-018` and NO AC may be written from it.** **`DW-071`** — `WBS-24.2`/`24.6`/`24.8` each add their route
> to `e2e/rtl-a11y.spec.ts` **in the same batch**; the sweep visits **3 of 52** routes.
> ⭐ **HOW IT WAS FOUND: the trigger had TWO clauses and only the second was parked. The SUMMARY over a row
> had dropped half of what the row SAID** — `DEC-064` d2's failure inverted. **Read triggers, not labels.**
> ⭐ **Store facts proven, not assumed:** `scope_adds` → a **deferred-work** target IS accepted (every prior
> one pointed at an AC/requirement/slice); `deferred_work.source_kind` is a CHECK over
> `brief|clarification|code|inferred` and anything else **rolls the whole batch back**.
> ⭐ **A full-row replace is safe if you hash first and verify after** — `DW-071`'s 2097-char title came back
> byte-identical and `custom_attributes` survived **by omission**. That is `PE-585`'s shape *defended*.

- ⚠⚠⚠ **I ASSERTED "THREE OF `SL-032`'s FOUR ROWS WERE MIS-SIZED". IT WAS TWO, AND I PROPAGATED MINE INTO
  SIX ARTIFACTS IN ONE DAY** (`PE-592`). `prm-next` said TWO and **the file was right**. I had conflated
  its TRUE sentence — *"that habit PAID three times in this slice"*, two catches **plus one confirmation**
  — with a count of wrong sizings. ⚠⚠ **A COUNT OF WHAT AN INSTRUMENT DID IS NOT A COUNT OF WHAT IT
  FOUND.** ⭐ Found only by re-reading `prm-next` end to end as its own preamble instructs; the mechanical
  id-and-status pass ran clean over every identifier and could not see it.
- ⚠⚠ **PREPARING A RESUME MEANS RE-VERIFYING THE FILE, NOT APPENDING TO IT.** The same pass found **five**
  more stale statements at once — a false phase claim, a tally with the right numerator and a stale
  denominator, a lesson count, and two `slice_id` numbers. **Replace each with the COMMAND that measures
  it.** ⚠ Bumping the file's own stale-count then invalidated the sentence beneath it. Numbers in prose breed.
- ⚠⚠⚠ **THE SAME FILE, LATER THAT DAY: THE SECTION THAT COUNTS WRONG STATEMENTS CONTAINED ONE** — two
  findings from two commits were **both** labelled `THE THIRTEENTH`, so its counter was a whole occasion
  short. **A SEQUENCE IN PROSE IS INVISIBLE TO EVERY MECHANICAL CHECK** — an id-and-status pass sees ids and
  statuses, never an ordinal, and it ran clean over 190 identifiers past this. Five more fell out of the
  same read (`PE-593`). ⚠⚠ **TWICE THE FILE CONTRADICTED ITSELF, AND BOTH TIMES THE STALE HALF WAS THE ONE
  WEARING THE DETAIL** — `DEF-104` "Open, low … twelve paged reads" a hundred lines under "`DEF-104` is
  `Fixed`", and `LL-011` "Proposed — needs the operator's interview" under "`LL-011` Approved and PINNED".
  **The half that reads researched is the half that wins the reader.** ⭐ The `LL-011` one was a stale
  INSTRUCTION: it would have sent a fresh session to re-run a ceremony that already happened.
  ⚠ **NOTHING CHECKS PROSE ARITHMETIC** — "twelve dated" + "nine correctly carry no date" over a **17**-row
  register sat there unnoticed; it is five. ⭐ **Re-measure the next action's premise before handing it on:**
  `WBS-24.1`'s unbuilt claim was re-run with a subject proof (4 tokens → 0 over 339 files, control
  `Backlog` → 305) instead of carried on its row's five-day-old word.
- ★★ [**`WBS-23.3`: the technique passed, the package failed**](wbs233-csp-spike.md) · ★★ [**`WBS-23.4`:
  right row, missing requirement**](wbs234-reclassify.md) — read these two before touching report export,
  topic classification, or shared dialog CSS. Between them: `LL-014` (registry metadata cannot rank
  correctness), the `ADR-0022` clause-4 conflict a **keyword** sweep found, and two findings against my own
  instruments (a hollow assertion that passed its mutant; a row that claimed to preserve text it deleted).
- ⭐⭐ **`DEF-087`'s fix-forward rule WORKS:** slice `wbs-done` ran **5→4→3→2→0** across `SL-032` — the first
  slice exit it could ever adjudicate (it returns zero rows for all 28 older slices). **Keep new wbs rows
  carrying `slice_id`.**
- ⚠⚠ **COMMITTING TO `main` IS NOT PUBLISHING TO `main`** — 10 unpushed package commits were folded into
  one feature squash. **Push after every package commit**; check `git rev-list --left-right --count
  HEAD...origin/main` — ⚠⚠ **`git fetch` FIRST. That count reads the LOCAL `origin/main` ref, so `0 0`
  against an unfetched ref means nothing**; it said "synced" right after a remote merge. ⚠ Trap 25 fires
  often: verify a merge by CONTENT, and back up `data/` before git ops.
- ⚠⚠ **ARABIC MORPHOLOGY BITES TEST ASSERTIONS** — a substring failed against a string that *visibly
  contains it* (`لـ` absorbs `ال`'s alef). **Assert the SCRIPT RANGE, never a fragment.**
- ⚠⚠ **`DEF-104` FIXED (#306 → `bdbd8b6`) AND ITS OWN COUNT WAS WRONG** — the row said TWELVE, its
  enumeration listed ELEVEN. Two sweeps on different keys agree on **eleven**, and **neither was complete
  alone**. ⭐ One shared `PageSize.Clamp` beside `PagedResult`, **`Max = 500`** because that is the largest
  page the SPA itself requests and `ADR-0022` verified 500 covers every register — **copying
  `GetNotifications`' 50 would have broken reports and the kanban.** ⚠ `GetDecisions` with a NULL limit
  still does **no** `Take`: capping where no cap existed is `DEF-103`'s silent-truncation shape.
  ⚠⚠ **MY OWN NEW GUARD WAS TOO NARROW ON FIRST WRITE** — keyed on the OUTPUT shape (`PagedResult<T>`), it
  excluded the one read that was already correct. Its **discovery guard** ("must find ≥10") caught it.
  **Key a discovery on the INPUT that defines the risk, not the output shape.**
- ⚠ **`PH-3` stays `Approved` ON PURPOSE** — `WBS-20.4` is the email adapter vs a hard constraint
  (`DEC-055`). Do **not** "repair" it; that is the manufactured-status move `DEF-010` records. `SL-014`
  `Deferred` (`DEC-028`) and off the ladder.

