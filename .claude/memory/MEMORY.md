# Memory Index — ACMP

> Compacted 2026-08-21 (10th). One line per memory; detail lives in topic files and the package.
> Read the linked file before acting. ⚠ **The old "keep under ~17KB or the tail is SILENTLY dropped"
> note was an UNMEASURED number and it is false at that size** — on 2026-08-21 this file was **18,668
> bytes and loaded IN FULL** (its last line was verified byte-identical to the loaded copy's). Where the
> real ceiling is, is still unknown, so keep the index lean **because a long index buries its own top**,
> not because of a threshold nobody measured. Do not restore a number you have not observed.

## ★★★ 2026-08-23 (END) · **`DW-082` IS THE LIVE WORK — 14 of 32 done** · run the checks, never quote them

> ▶▶ **NEXT: the `DW-082` handler tests — 18 files left**, branch **`chore/vitest-4-pair`**. `DEC-075` d2:
> **ALL FOUR** activated streams are in scope (operator said "all"); the ORDER is the agent's
> recommendation only — tests → `SL-033`/`WBS-24.1` → `DW-080` alone → `DW-079`.
> ⚠⚠ **`TopBar` IS NOT A HANDLER FIX** — `DevRoleSwitcher.tsx` is in coverage `exclude` but **its call
> site is not**, so the exclusion hides the component and still charges `TopBar`. Needs its own decision.
> `DecisionPage` is PARTIAL (retry done). ⚠ **NEVER lower `ADR-0016`'s threshold** — the number would now
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
