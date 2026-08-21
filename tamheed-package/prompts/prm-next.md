# Kickoff prompt — the durable one

Paste everything between the two `=====` lines into a fresh session. This file is durably named:
when the work changes, **edit it — do not create a new `prm-*.md`.**

=====

Read `tamheed-package/prompts/README.md` (the operator guide) and `AGENTS.md` before anything else.

```
server_info()                                # expect tamheed 4.4.2, root = C:\Users\ahammo\Repos\acmp
package_open("tamheed-package")
gate_run()                                   # 7/7 is the NORM again (tamheed >= 4.4.2). A red gate is a
                                             # REAL finding - read its failure list, it names the token.
readiness_check("package")                   # expect ready:TRUE. Advisories failing is NORMAL (see §1).
                                             # ready:FALSE = a real blocker, go read it, never soften it.
git status --porcelain -uall                 # expect clean; you are on `main`, everything is merged
```

⚠ **Do not trust any tally written into a prompt, including this one.** Read the live numbers. This
file has carried a stale statement **twelve** times, and **two** unmeasured assertions have escaped into
commit messages, which cannot be amended. **Five of the eleven were written and then invalidated within
the SAME session** by the very work that session was doing: on 2026-08-19 it said
`readiness ready:TRUE` and `gate 7/7` hours after `DEF-093` made both false; its requirement tally
went stale the moment batch 13 recorded two verdicts; and on 2026-08-20 §1's *"`assumptions-current`
reports `indeterminate` because 0 of 17 rows carry a `validation_date`"* was made false by the
disposition session that was reading it. A prompt that restates a number is a prompt that
will lie to you. **Point at the live check, do not quote it.** If you find this section wrong again,
**fix it in the same session and bump this count.**

⚠ **THE TWELFTH WAS AN INSTRUCTION, NOT A NUMBER, AND IT WOULD HAVE COST A DEPENDENCY.** §6's
`▶▶ DO THIS FIRST` block told a fresh session to run a spike and, three lines down, *"THE DEPENDENCY
EVALUATION IS DONE — do not redo it"* over a table marking `modern-screenshot` as the pick. The spike
ran on 2026-08-21 and that package **cannot run under this app's CSP at all**. Every number in the table
was correct; the *instruction built on them* was wrong, and a fresh session obeying it would have
installed a package that throws on every card. The block was **replaced, not annotated** — see §6.
**This is the tenth fix's lesson repeating: an id-and-status verifier cannot see a stale INSTRUCTION.**

⚠⚠ **THE THIRTEENTH IS THE MOST EMBARRASSING, BECAUSE IT HAPPENED IN THE SESSION WHOSE OWN FINDINGS ARE
ABOUT THIS.** Commit `a2066ba` asserts *"readiness ready:TRUE"* about the state after twelve activations.
Only `gate_run()` was run after those writes; the last `readiness_check` preceded every one of them. The
assertion was later verified true — **and it was still unmeasured when written.** ⚠ **An assertion that
happens to be correct is still an unmeasured assertion**, and "it came out right" is the reasoning that
lets the practice rot. It was caught by a review pass, not by me. Same breath, or don't write it.

⚠⚠ **THE ELEVENTH DID NOT REACH THIS FILE — IT REACHED A COMMIT MESSAGE, WHICH CANNOT BE AMENDED.**
Commit `46821d6` states the deferred-work triage split as *"out of scope 11 / blocked on a stack 17"*. The real
split is **10 and 18**. The script had always said so; the command that ran it piped through `tail`, so those two
lines were never on screen, and I wrote the counts from recollection. It was caught only because preparing this
file starts by re-measuring everything it asserts. **THE MECHANICAL RULE: a number entering a durable artifact
must come from output visible in the same breath — not from what you believe the output said.**

⚠ **THE TENTH (batch 14) IS THE MOST INSTRUCTIVE, BECAUSE THE MECHANICAL CHECK PASSED.** After reconciling
this file I extracted every governance id it references — 68 across nine families — and verified all 68 live:
zero dangling, zero status mismatches. **The file still contained three wrong statements**, because none of
them is an id or a status: a `⚠ read this requirement carefully` instruction for a requirement that batch had
just closed, and two prose counts ("the first subtraction alone yields twenty", "five of the original
twenty"). ⚠⚠ **An id-and-status verifier cannot see a stale INSTRUCTION or a PROSE NUMBER — and a prose
number is exactly what this section is about.** The fix was to delete every intermediate count and leave the
three-step rule. **When you re-verify this file, read the prose; the mechanical pass is the easy half.**

⚠ The eighth fix (batch 13) stopped patching the numbers and **deleted them**, replacing each with the
command that measures it. A tally you can re-type is a tally that will go stale again; a command cannot.

⚠ **The NINTH fix was different and is worth copying.** It was not found by tripping over a wrong number
mid-task — it was found by **reading this file end to end** before handing it to a fresh session, which
surfaced **nine** wrong statements at once: a stale tool version, four row states that had since changed,
three tallies, and a **section heading that contradicted its own body** (`SC-021` was labelled "needs you,
Proposed" three lines above a paragraph saying it was Merged). A fresh session would have acted on the
heading. **Before you hand this file on, re-read the WHOLE thing and re-verify every row state it asserts
against the live register** — batch 13's pass checked 19 of them mechanically and found zero mismatches
only AFTER the nine fixes.

---

## §1 — The state the 2026-08-20 disposition session began from (⚠ superseded in part — see §6)

> ⚠⚠ **TWO LATER SESSIONS CHANGED THE SHAPE OF v1 AND STARTED BUILDING. THIS SECTION IS THE STATE THEY
> BEGAN FROM, NOT THE STATE YOU ARE IN.** Rows were activated, requirements returned to `Approved`, one
> of the two blind controls was fixed, and `PH-7`/`SL-032` now carries live build work with two items
> already merged. **Go to §6 — read `▶▶ `WBS-23.3` IS SHIPPED` and
> `▶ WHERE THE BUILD WORK STANDS` before
> acting on anything in §1, §2 or §4.** ⚠ Every phase statement below predates `PH-7`.

**THE BUILD LADDER IS FINISHED AND SO IS THE REGISTER PROGRAMME.** `P1`–`P19` shipped long ago; the
`DW-029` acceptance-criterion programme that replaced it ran **twenty batches** and was accepted by the
operator on 2026-08-20. **`SL-031` is `Implemented`, `PH-6` is closed, and every phase is `Implemented`
except `PH-3`.**

⚠ **`PH-3` stays `Approved` ON PURPOSE — do not "repair" it.** `WBS-20.4` is the email adapter against a
hard constraint (`DEC-055`), and closing it is the manufactured-status move `DEF-010` records.
⚠ `SL-014` is `Deferred` (`P14`/Tarseem, `DEC-028`) and is off the ladder. Do not start it.

**You are on `main`, clean, everything merged, CI green.** No feature branch is open.

### Measure, do not trust — the three commands that replace every tally

⚠ **`entity_query("requirement", ...)` OVERFLOWS THE TOOL'S TOKEN LIMIT** — 226 rows is ~82 KB even
with `columns` set, because `columns` does not actually narrow the payload. **Count from the canonical
JSONL instead**, which is also what trap 13 already tells you to do when building any payload:

```
tamheed-package/data/requirements.jsonl     # 226 rows: count by lifecycle_status / kind / priority
tamheed-package/data/deferred_work.jsonl    # the DW register: count by lifecycle_status / severity
entity_query("defect", status="Open")       # small enough to query directly
gate_run() / readiness_check("package")     # the live verdicts — never quote a remembered one
```

⚠ **No count is written into this file on purpose.** Ten stale tallies, and an eleventh that reached an
unamendable commit message, are why. The one structural fact worth stating is a *shape*, not a number:
**the requirement register is now mostly `Implemented` or carried by a `DW-` row, and what remains
`Approved` is either externally blocked or covered by deferred work.**

### THE CANDIDATE RULE — and it was BROKEN until batch 20, so read this before running it

The rule for "which Must-priority non-functional requirements could still be closed by reading code":

1. the `Approved` **Must**-priority **non-functional** ids;
2. **minus** every id named in a `deferred-work` row's **TITLE** — i.e. the row is *about* it;
3. **minus** the ops/runtime group `PE-485` names (`NFR-015 017 044 052 062`), which no `DW-` row covers
   but which is separately blocked on a running stack.

⚠⚠ **Step 2 used to read *"named anywhere in a deferred-work row"*, and that CONFLATES A MENTION WITH A
COVERAGE.** Writing `DW-074` with the phrase *"the `NFR-018` ASVS assessment"* in its activation trigger
made `NFR-018` vanish from the candidate set though nothing about it had changed. **The loose rule
silently shrinks the worklist every time a row cross-references a requirement — so the better the
register's prose gets at linking things, the more requirements quietly disappear, and a
well-cross-referenced register would eventually report itself finished.**

**Run it and expect TWO, both externally blocked:** `NFR-018` (needs an external OWASP ASVS 5.0 Level 2
assessment) and `NFR-038` (rides Tarseem, whose `P14` is deferred indefinitely and which has no endpoint
in the product at all). That is the operator's stated end condition for the programme, and it is met.

### The advisories — none is a task, and two are BLIND

`defects-minor` (carried deliberately — **read the live list, it grew on 2026-08-20**),
`deferred-work-reviewed` (grows on purpose), `acs-slice-bound` (`AC-109`–`AC-112`, accepted by `DEC-058 d3`),
and `assumptions-current`, which **now fails on purpose** — see below.

⚠⚠ **ONE OF THE TWO BLIND CONTROLS IS FIXED; THE OTHER IS NOT.** ✅ `assumptions-current` no longer reports
`indeterminate` — twelve rows were dated on 2026-08-20 and it now **fails**, naming the genuinely overdue
one. ⚠ **The field is a FUTURE re-validation DUE date**, so more will go red as dates pass: **that is the
control working, and clearing a date to restore the amber would be re-blinding it.** ⛔ Slice-scope
`defects-closed` and `wbs-done` are STILL permanently `indeterminate` because almost no defect row carries
`found_in` and 155 of 164 `wbs_items` have a NULL `slice_id` (`DEF-087`) — `readiness_check` says so in its
own note. **A rule that cannot fail is not a green light**; this is the shape this project keeps finding in
instruments, living inside the package's own controls. See §6.

### The mechanical guarantee, and the token rule

`gate_run()` returns **7/7** and that is the norm (tamheed ≥ 4.4.2). **A red gate is a REAL finding —
read its failure list, it names the token.** ⚠ **Journal text is EXEMPT** (`progress_entries.entry`,
`audit_verdicts.evidence`), so a progress note may quote marker tokens freely. **Every live ENTITY row is
still screened** — `title`, `statement`, `description` — so there, name the concept or backtick the token.

**Ten lessons are Approved and PINNED** (`LL-001`…`LL-010`) and bind every session via the tool-owned
note. Three were added this session; `LL-008`, `LL-009` and `LL-010` are below because they earned
themselves within days of being written.

- **`LL-008` — sweep the registers by KEYWORD as well as by identifier.** An id-only sweep returned **zero
  hits for six of seven** candidates and would have concluded the registers were silent; the keyword sweep
  found `ADR-0035`, ratified, replacing the storage technology `NFR-027` still mandated by name.
  **Neither row contained the other's id.** It then fired twice more in three batches.
- **`LL-009` — two instruments agreeing is ONE instrument when they share a mechanism.** Two independently
  written scanners returned an identical 13-sites/7-files answer and were **both blind to the same two
  files** (C# target-typed `new(`). Corroboration requires independence, not repetition.
- **`LL-010` — when a requirement says X is the single source for Y, check X EXISTS before measuring Y.**
  `NFR-039`'s glossary is a **circular pointer between two English documents**, so its measurable clause
  is *undecidable*, not merely unverified.

### What this session landed (batches 14–21) — reference, do not redo

- **Requirements closed:** `NFR-027` (`AC-135`), `NFR-050` (`AC-136`), `NFR-034` (`AC-137`),
  `NFR-028` (`AC-138`), `NFR-023` (`AC-139`). **`NFR-026` moved to `Deferred`** by `SC-026`.
- **Defects fixed:** `DEF-095` (#298), `DEF-097` (#299), `DEF-098`, `DEF-099` (#300).
- **New deferred work:** `DW-067`…`DW-074`.
- **Scope changes merged:** `SC-025` (NFR-027 → the configured object store, per `ADR-0035`),
  `SC-026` (NFR-026 → Deferred), `SC-027` (NFR-023's `[unverified]` marker removed after `DEC-065`).
- ⚠⚠ **`DEF-099` IS THE ONE TO READ IF YOU READ ONLY ONE.** OTLP traces had **never reached Seq in any
  environment**: the endpoint was the bare `/ingest/otlp` and `OTEL_EXPORTER_OTLP_PROTOCOL` was set
  nowhere, so the exporter's gRPC default posted to a path Seq 404s. The worker separately had **no
  `OTEL_` variables at all**. **The discriminator is the transferable part:** under shipped config, 8
  `/readyz` calls grew Seq's event count 679→739 while the DB span count stayed at **exactly 72**. Same
  host, same port, same container — log path delivering, trace path not. **Verify any observability change
  by sending traffic and asserting spans MOVE, never by a clean start-up.**
- ⚠ **The operator declined to relax a requirement TWICE** — `SC-024` (`NFR-054`'s minimal-base clause →
  `DW-066`) and `DEC-066` (`NFR-019` kept, gap → `DW-074`). **The register is being kept as a statement of
  the target, not of the status quo. Do not offer a narrowing as the easy path.**

## §2 — Things already built that get re-proposed. Do NOT rebuild.

- **`SL-030` — Confidentiality ABAC for Restricted topics, with egress redaction** (#295 → `1a52dba`),
  all mutation-proven; `AV-192` is the evidence. ⚠ **`MoveTopicPriority` is deliberately UNFILTERED** —
  `BacklogPrioritize` admits Chairman/Secretary only, both committee-wide readers, and filtering would
  renumber the column as if hidden topics were absent. ⚠ **`SC-021` is Merged but `WBS-22.3` still reads
  "notification bodies"** — the SC row and its `scope_modifies` edges ARE the correction. Do not "tidy" it.
- **`SL-029` — FR-030 topic conversion with provenance** (`bcc3d00`, `b065a29`).
- ⚠ **`Timeline.tsx` and `Calendar.tsx` EXIST AND ARE DELIBERATE EMPTY SHELLS** — routed, well-commented,
  drawing nothing; their own headers say so. Requirement ids in source comments are **positive-only**
  evidence: one such citation was a *deferral* note.
  ⚠⚠ **BUT `Calendar.tsx` IS NO LONGER IN THIS "DO NOT REBUILD" SECTION'S SPIRIT.** `DW-037` is
  **`Activated`** (`SC-028`) and `FR-035` is back to `Approved`: filling that shell is **live, approved
  work**. This bullet now means only *"the file existing is not evidence it was built"* — it is **not** a
  prohibition. ⚠ Build it against the **Meetings** API (`MeetingDetailDto.ScheduledStart` +
  `AgendaItemDto.TopicId`); `Topic.Schedule` discards the meeting id into an unconsumed event.
  `Timeline.tsx` is unchanged — `FR-036`/`DW-001` stay deferred, since topics still carry no planned span.
- **Real accessibility instruments already exist and are easy to miss:** `axe-core` is a dependency, and
  there is a jsdom axe test over 5 surfaces, a **live Playwright axe sweep in BOTH locales** with the full
  `wcag22aa` tag set, and a token-contrast test computing real WCAG luminance over 20 pairs in two
  palettes. **Check what exists before believing a "needs a browser" label.**
- **Real app-side inactivity detection exists** (`useIdleSignOut.ts`, 30 minutes, wiring mutation-tested).

## §3 — The two registers that will mislead you

- **Requirement status measures whether anyone WROTE an acceptance criterion, not whether the thing
  was built.** A requirement advances only via the AC auto-advance trigger, so one with no AC can
  never leave `Approved` however well it shipped. ⚠ **The three counts MOVE EVERY BATCH and are
  deliberately absent from this file — measure them (§1 gives the commands), never quote a written one.**
  Since 2026-08-18 the three labels finally denote different things.
- ⚠ **The `mvp` / Phase attributes record the ORIGINAL scoping and the ladder outgrew them.** Of the
  53 `mvp=0` requirements once described as "deliberately not built", **about 30 are built and
  shipped**. `SC-020` reclassified only the **24 verified absent from source**. See `LL-006`.
  ⚠ **`Deferred` is no longer a one-way label.** Four of `SC-020`'s 24 have since come back to
  `Approved` — `FR-035` via `SC-028`, and `FR-032`, `FR-154`, `FR-155` via `SC-029` — because their
  deferred-work rows were activated and **a requirement labelled not-in-v1 beside `Activated` work is a
  contradiction nothing in the package can see.** Measure the Deferred set; do not assume it only grows.
- **`DEF-012` is Won't-fix** (`DEC-055`): the one mechanical rule that would "fix" `v_backlog` closes
  `WBS-20.4`, the email adapter, against a hard constraint.

## §4 — Traps. Every one has cost real time here.

### A — Before you call something broken, or built

1. **Read the implementation.** Rows read as pre-checked; they are not. `DEF-062/061/065/071/041`
   and `AV-159` were each wrong in my own hand. ⚠ `AV-159`'s shape: **"I searched for X and found
   none" rules out X, never the family.**
1b. ⚠⚠ **AN ABSENCE IS ONLY EVIDENCE IF THE INSTRUMENT IS PROVEN PRESENT.** A parked branch once
   handed over *"both positive tests fail with an empty audit table"* — every word false. The helper
   read a NULL column and its two `NotContain` controls passed **vacuously**.
1c. ⚠⚠ **`LL-006` — A PROXY IS NOT THE ARTIFACT, AND IT FAILED FOUR TIMES IN ONE SESSION, EACH TIME
   INSIDE THE CORRECTION OF THE LAST.** A register attribute, then a register row, then a **filename**,
   then a filename again. ⚠ **`Timeline.tsx` and `Calendar.tsx` EXIST AND ARE DELIBERATE EMPTY
   SHELLS** — present, routed, well-commented, and drawing nothing; their own headers say so. (⚠ Status
   note, so this is not misread as a prohibition: `Calendar.tsx`'s row `DW-037` is now **`Activated`** —
   see §2. The LESSON here is about the file's existence proving nothing, not about the work being off
   limits.) Check **both** directions: the sweep also found `FR-032` unbuilt inside the "presumed built"
   group — and `FR-032` is now `Approved` with `DW-033` `Activated`, so that example has moved too.
   Requirement ids cited in source comments are strong evidence of being BUILT, but the instrument is
   **positive-only** and one citation was a **deferral note** (`InvariantStatus.cs:7`).
2. **A measurement that indicts known-good code is measuring itself.** ⚠ Fired again 2026-08-18:
   `grep -c "Convert/3"` returned 2 and nearly read a deleted allowlist entry as surviving — the hits
   were the **comment explaining the deletion**. Match the quoted entry, not the substring.
3. **The LSP diagnostics panel is stale constantly** — it fired repeatedly again in batch 13, including a
   full screen of errors for `Topic.IsRestricted` and friends that had been merged and green for days, and
   twice for `vr-*.tsx` files that do not exist and were never tracked. **Build before believing it**; the
   build is the arbiter and it took ten seconds to settle every one.

### B — What your tests structurally cannot see

4. **THE PROVIDER YOU TEST ON DECIDES WHAT CAN PASS.** `DEF-066`: assigning a stream had **never**
   worked on a real database under four green suites. `Acmp.Application.Tests` and `Acmp.Api.Tests`
   are **EF InMemory**; only `Acmp.Integration.Tests` is real SQL Server, and
   `SearchProvidersFtsTests` is the **only** place any `FREETEXT` branch executes.
5. **JSDOM DOES NOT RENDER.** If a change is visual, **look at it**: a throwaway page importing only
   `main.tsx`'s stylesheets and the real components, served over **http**. ⚠ Last session this found
   **three** defects no suite could: a standalone `.sub-card` collapsing to 43px in RTL, an icon
   forcing a label onto a second line, and `aria-pressed={undefined}` **omitting the attribute**,
   silently demoting a toggle to a plain button. ⚠ A portaled `Dialog` escapes a wrapper `div`, so
   set `dir` on **`<html>`** as the app does — otherwise the RTL reading is a harness artifact.
5b. ⚠ **AN UNDEFINED CSS *CLASS* IS AS SILENT AS AN UNDEFINED CUSTOM PROPERTY.** `.sub-cards-2` does
   not exist (only `.sub-cards`, already 2-column, and `.sub-cards-3`). Grep `styles/` for a class and
   `tokens.css` for a variable **before** writing either (`DW-031`).
6. **Coverage catches unread state but never an uncalled method** — `DW-026`, fired seven times.
7. **A requirement typed to a RESOURCE is invisible wherever that resource type is absent**
   (`DEF-068`). ⚠ This fired again on `SL-030`: adding `ConfidentialityRequirement` to a policy broke
   **every** `Topic.Edit` cell until the test's `StubTopic` implemented the new contract. **A policy
   may join a `*Scoped` set only if EVERY call site passes a matching aggregate** — which is why
   `TopicTriage` is excluded (endpoint-level on `/close`, `/reopen`, `/reactivate`, `/convert`).

### C — Proving things

9. **The test must fail without the change.** Mutation-check every guard and name which test fails.
10. **A mutation nothing catches is a decision nobody recorded.**
11. **A HOLLOW PASS IS WORSE THAN AN `indeterminate`.** ⚠ Slice-scope `defects-closed` is
   `indeterminate` — almost no defect row carries `found_in` (`DEF-087`). The **superset** answers only
   part: package-scope `defects-closed` passing rules out open **critical/high** and says nothing about
   medium/low, which live in the `defects-minor` advisory. Read both.
12. **A green exit code can come from a run that checked nothing.** Confirm the mutant **COMPILED**
   — a mutation run that silently failed to apply reports a clean pass.

### D — Writing to the package

13. **Build payloads from `data/*.jsonl`, never from `entity_query` output** — v4 needs FULL rows and
   the store holds **truncated** ones.
13b. ⚠ **PROVEN 2026-08-18: OMITTING A NULLABLE FIELD *PRESERVES* IT; NOT NULL FIELDS ARE REQUIRED.**
   `{type, id, lifecycle_status}` alone is refused (`NOT NULL constraint failed: requirements.kind`),
   but sending only the NOT NULL columns preserves `statement`, `priority`, `rationale`,
   `verification_method`, `custom_attributes` **byte-identically**. A status-only requirement update
   needs just `id, kind, title, mvp, lifecycle_status, source_kind, source_span, introduced_in`.
14. ⚠ **A GENERATED payload must be PASTED, not RE-TYPED** (`LL-001`). Where composition is
   unavoidable, **hash a pre-image and assert byte-identity after** — done twice last session over
   24 requirements and 4 WBS items, both identical.
15. **Omitting `custom_attributes` PRESERVES it; sending it REPLACES the whole blob.**
16. **Run `gate_run()` AFTER writing.** ⚠ Creating an AC ahead of its build turns `G-PROGRESS` RED —
   record a verdict in the same session (`Pending`, or `Partial` when part is genuinely done).
16b. ⚠ **`G-TRACE` WANTS THREE LEGS WHERE THE ADVISORY WANTS ONE.** A new `mvp=1` requirement must
   link to a **decision-or-ADR**, a **wbs-item-or-slice**, AND a **test**. Wiring two clears
   `requirements_unwired` while `G-TRACE` stays red, which reads like the fix not working.
16c. ⚠ **`acs-met` COUNTS BY `retired_in`, AND IGNORES `lifecycle_status` ENTIRELY.** A `Deferred` AC
   still counts; only retirement removes one. So **writing ACs for unbuilt work holds readiness false
   forever** — this is why `DW-029` cannot be executed as one bulk pass.
16d. ⚠ **SLICE-SCOPE `wbs-done` IS VACUOUS FOR EVERY PRE-EXISTING SLICE** (`DEF-087`): all 155
   `wbs_items` have `slice_id` NULL, so the rule returns zero rows for all 28 closed slices. It also
   **breaks the obvious AC→slice derivation**. New WBS rows must set `slice_id`.
16e. ⚠ **`RELATION_RULES` REFUSE PLAUSIBLE EDGES.** `lesson --learned_from--> deferred-work` is
   rejected (allowed targets: decision, defect, progress-entry, risk, slice, wbs-item). The error is
   the only documentation, and **the whole batch rolls back** — re-send the entire corrected batch.
17. ⚠ `progress_entries` is **append-only** — correct via a `correction` event, never an edit.
17b. ⚠⚠ **BRANCH TOPOLOGY IS PACKAGE TOPOLOGY (C31).** The package lives in the git working tree. Do
   package writes on the branch you will merge, and **merge `main` in FIRST** when the branch predates
   a package-only commit. Commit `tamheed-package/data` immediately after every write batch.

### D2 — Batch 13's additions

32. ⚠⚠ **SWEEP `adrs`, `decisions` AND `open_questions` — NOT JUST `requirements`.** `LL-005` is usually
   read as "check the requirement register". Batch 13 did exactly that, thoroughly, and still offered the
   operator a build that would have reversed **`ADR-0037`'s decision clause**, whose own consequences name
   the fix as deferred under **`OQ-061`**. What caught it was reading `docker-compose.cloud.yml` before
   writing code and seeing a bare ADR id in a COMMENT. **Sweep all three registers before writing a `DW-`
   row or putting an option in front of the operator.**
33. ⚠⚠ **A COUNT OF THE ENFORCING MECHANISM IS NOT A MEASURE OF THE PROPERTY.** The `NFR-021` census read
   as a **38-command server-side-validation hole** — the shape of a critical security finding. All four
   commands that actually carry scalar input are guarded **in the domain** instead (`SetTimebox` clamps
   5..120, `MoveItem` bounds-checks the index, `RecordActualMinutes` floors at 0, `DeleteRecording` uses its
   string only as an EF equality predicate). Filing off the census alone would have been wrong.
34. ⚠⚠ **COVERAGE MUST BE UNIONED ACROSS PER-PROJECT REPORTS, NEVER SUMMED.** A file appears in several
   projects' cobertura reports, unexecuted in most. Summing reported **20%** for a file the gate correctly
   scored **100%**. `check-coverage.mjs` says it unions; believe it over your own ad-hoc script. Trap 2's
   exact shape — the measurement indicting known-good code was the broken thing.
35. ⚠⚠ **HANGFIRE'S `JobStorage.Current` AND `GlobalJobFilters` ARE PROCESS-GLOBAL, and a second
   `BackgroundJobServer` in one process does not reliably pick up work.** A filter test that ran a real
   in-memory server passed ALONE and reported **zero spans** in the full suite — which reads exactly like a
   broken filter. If you need a real Hangfire server, expect that; a test green alone and red in the suite is
   worse than no test. `WebexJobDeadLetterTests` already owns the serialized collection.
36. ⚠ **HANGFIRE NEVER HANDS A FILTER THE JOB'S OWN EXCEPTION** — it wraps it in a `JobPerformanceException`
   whose message is the same fixed sentence every time. Record `InnerException`, or every failed job gets an
   identical, useless error tag.
37. ⚠ **A PACKAGE THAT DOES THE JOB MAY NOT BE SHIPPABLE.** `OpenTelemetry.Instrumentation.Hangfire` has
   never had a stable release (`1.17.0-beta.1`). Check for a stable version BEFORE designing around a
   package, and treat "prerelease dependency in a production image" as the operator's call.
38. ⚠ **DO NOT PUSH TO A BRANCH WITH CI IN FLIGHT.** For `pull_request` events GitHub evaluates
   `paths-ignore` against the WHOLE PR diff, so once a PR touches `src/` even a package-only commit re-runs
   everything and cancels the in-flight e2e. Batch 13 cost itself a full ~20-minute e2e cycle this way.

### E — Environment

18. **`.cs` files need a UTF-8 BOM and LF**; the Write tool adds neither. ⚠ `git status --porcelain`
   collapses untracked directories — use `-uall`.
18b. ⚠ `dotnet ef migrations add` rewrites the model snapshot as **CRLF** against this LF repo.
   Normalise the migration, its Designer and the snapshot to **BOM + LF** before building.
19. ⚠ `gh pr create --body` and `git commit -m` with backticks break under PowerShell — use
   `--body-file` / `-F <file>`. ⚠ A `git commit -F -` **heredoc on stdin** can trip the
   no-verify guard hook; write the message to a file instead.
22. ⚠ The coverage gate is **per-file ≥95%**, and the line a new feature most often misses is the
   **validator**. `rm -rf tests/*/TestResults` before trusting a local run (`DEF-069`).
22b. ⚠⚠ **THE SPA TYPECHECK HAS TWO ENTRY POINTS AND ONE OF THEM CHECKS NOTHING.**
   `npx tsc --noEmit -p tsconfig.json` **exits 0 over a tree that does not compile**, because
   `src/Acmp.Web/tsconfig.json` is solution-style: `"files": []` plus project references. A clean scan
   with no subject (`DEF-091`). `vitest` will not catch it either — it transpiles per file and never
   typechecks, so 1241 tests passed over 13 real type errors for ten commits. **Use `npm run build`
   (`tsc -b && vite build`, exactly what CI runs) or `-p tsconfig.app.json`, and prove your checker has
   a subject by injecting a deliberate error and watching the count move.**
23. ⚠ **`gh pr checks --watch` AND `gh run watch` BOTH REPORT SUCCESS ON UNFINISHED RUNS.** Poll the
   `status` field until `completed`, then read `conclusion`; treat a 503 as **unknown**, never success.
23b. ⚠⚠ **`$?` AFTER A PIPE IS THE PIPE'S LAST COMMAND, NOT YOUR GATE'S.** Fired again 2026-08-18:
   reading it bare caught a real `IMPORTS` failure `dotnet format` would have hidden behind `tail`.
   Redirect to a file and read `$?` on the bare command.
24. ⚠ **NEVER `git checkout -- .` WITH UNCOMMITTED WORK.** Commit first, then mutate against the commit.
25. ⚠⚠ **`gh pr merge --delete-branch` CAN MERGE REMOTELY AND STILL FAIL LOCALLY.** It did again on
   #293: squash-merged, then aborted with *"Not possible to fast-forward"*, leaving the tree on a
   pre-feature `main` that **looks exactly like lost work**. Check `gh pr view --json state` first,
   verify the content is in `origin/main` **by content, not ancestry**, then `git reset --hard`.
26. **A log tail is the wrong instrument for a failure whose distance you do not know** (`DEF-074`).
27. ⚠ **NOTHING A LATER SESSION MUST READ MAY LIVE IN THE SCRATCHPAD.** Repository or package, never
   scratchpad.
27b. ⚠ **A PLUGIN RELOAD ORPHANS THE PACKAGE LOCK.** `/reload-plugins` kills the MCP server process while
   `data/.lock` still names its pid, so the next `package_open` refuses with "another writer owns this
   package". **Do not just delete it.** The operator guide's two discriminators decide: an **identity**
   failure (the named pid is not running) OR an **ordering** failure (the process started AFTER the lock's
   `taken_at`). Either one proves staleness. On Windows check with PowerShell — `Get-Process -Id <pid>` —
   because Git Bash mangles `tasklist /FI` into a path. Then remove it deliberately and say why.
28. ⚠ Delete throwaway visual-verify harnesses before committing — `vr-out/` is gitignored but a
   `vr-*.tsx` in `src/` is not, and it would ship.
29. ⚠⚠ **A SCRIPT'S PATHS MUST NOT BE cwd-RELATIVE — CI RUNS IT FROM SOMEWHERE ELSE.** `ci.yml`'s
   frontend job sets `working-directory: src/Acmp.Web`, so a scanner rooted at `'src/Acmp.Web/src'`
   resolves to `src/Acmp.Web/src/Acmp.Web/src` and either crashes or, far worse, **reports a clean tree
   over ZERO files**. Resolve from the script's own location (`fileURLToPath(import.meta.url)`), and
   **run the script the way CI runs it**, not only from the repo root. Same family as trap 22b.
30. ⚠⚠ **A SCANNER YOU WRITE CAN MEASURE ITSELF.** The first hardcoded-string scanner used
   `/>([^<>{}]*)</` and matched straight through TypeScript: `day >= 1 && dayNum <` read as a JSX text
   node. It reported FIVE findings and **all five were the bug** — acting on the count would have meant
   "fixing" five pieces of correct code. Before trusting a new instrument, inject a KNOWN-POSITIVE and
   confirm it is found, then confirm the tree is clean without it.
31. ⚠ **A GATE WITH NO SUBJECT MUST FAIL, NOT PASS.** Write the guard INTO the tool: refuse to report
   a clean result over an implausibly small file set. `scripts/check-hardcoded-strings.mjs` exits
   non-zero below 50 components for exactly this reason. Same lesson as `DEF-078`'s healthcheck that
   evaluated zero checks and `.gitleaks.toml`'s allowlist that exempted every markdown file.

## §5 — Definition of done

Unit + integration tests, each guard proven by **forcing its refusal** and verified to fail without
the change · flip AC verdicts via `audit_record` with evidence, `verified_by`, `verification_method`
and `against_commit`, and say plainly when something is ANALYSIS rather than measurement ·
authorization enforced server-side, `AuditEvent`s asserted as **ROWS** · no hardcoded strings, EN + AR
together, RTL verified · **if it is visual, look at it** · no secrets · `progress_update` +
`work_bind`, then `gate_run()` and `export_html()`; **commit `tamheed-package/data` immediately** ·
conventional commits, small and reviewable · branch → PR → green CI → squash-merge ·
**register every finding as a Tamheed row AS YOU GO — including findings against your own work.**

⚠ Before offering the operator a disposition for a capability, **sweep `adrs`, `decisions` AND
`open_questions` as well as `requirements`** (`LL-005`, and trap 32 — sweeping only the requirement register
is what nearly reversed an Approved ADR). ⚠ Scope changes, waivers and `force` are the operator's alone, and the interview
runs **every** time (`LL-002`) — plan approval is not scope-change approval.

### ⚠ Rules the `DW-029` programme paid for — they outlived it, and they override habit

The programme is closed, but these were expensive and they generalise to any verification work here.

- **Never leave a Pending or Partial acceptance criterion.** `acs-met` counts by `retired_in` and ignores
  `lifecycle_status`, so an AC written ahead of its evidence holds package readiness false **forever**.
  Write the AC and record its verdict in the SAME batch, or do not write the AC.
- **A part-verified requirement gets a `DW-` row, not an AC.** If you cannot evidence the WHOLE criterion
  today, a deferred-work row with a real activation trigger is the honest instrument — and say so IN the
  row, so the next session does not "helpfully" add the missing criterion.
- **Say what you are NOT claiming.** Every verdict this session names clauses it deliberately does not
  cover. A verdict that quietly covers less than its criterion is the failure the programme existed to end.
- ⚠ **COUNT THE REQUIREMENT'S CLAUSES BEFORE YOU COUNT YOUR FINDINGS.** Twice in three batches, strong
  evidence covered two-thirds of a three-part requirement and nearly carried a `Met`: `NFR-037` (31
  locale-aware date sites — and **exactly two** `Intl.NumberFormat` sites, while the text says *"date,
  time, **and number**"*) and `NFR-033` (a genuinely good contrast gate covering **one of two thresholds
  and none of four states**). **Evidence that is strong is not evidence that is complete.**
- ⚠ **A surface that DOES NOT EXIST can be excluded BY NAME in a `Met` verdict; a surface that EXISTS but
  is unproven CANNOT.** That single line decided every borderline call this session.
- ⚠ **A COUNT OF THE ENFORCING MECHANISM IS NOT A MEASURE OF THE PROPERTY.** An `NFR-021` census read as a
  38-command validation hole; all four commands carrying scalar input were guarded in the domain instead.
  An `NFR-039` census produced **76** Arabic divergences — but **Arabic adjectives agree in gender**, so 55
  were morphologically correct and the remaining 21 were candidates, not defects.
- ⚠ **Before offering the operator a disposition, sweep `adrs`, `decisions` AND `open_questions` — by
  KEYWORD as well as by id** (`LL-005`, `LL-008`). ⚠ And **sweep BEFORE the interview, not after**: the
  `NFR-023` question went to the operator before its sweep, and the sweep would have changed the question —
  the register already held two answers nobody had connected to it.

✅ **BRANCHING IS SETTLED — stop asking.** Operator decision, 2026-08-20: **split by content.** Package,
prompt and memory writes go **straight to `main`** (both CI workflows path-ignore `tamheed-package/**` and
`.claude/**`, so they cannot redden anything). **Anything touching CODE goes branch → PR → green CI →
squash-merge.** Batch 13 followed this and it worked; PRs #296 and #297 are the examples.

## §6 — THE CARRIED LIST. This section IS the list; nothing is carried in conversation.

**Reconcile this section whenever you close one — a list nobody maintains is worse than no list.**

### ⚠ WHAT THE 2026-08-20 DISPOSITION SESSION DID — context, not the next action (that is above)

**Item 1 of the old list — the deferred-work disposition — IS DONE, and it went differently than the
bucket table predicted.** The whole slate is a published artifact carrying the full canonical text of every
record it cites; the register carries `PE-556`, `PE-558`, `DEC-067`, `SC-028` and `SC-029`.

⚠ **The batch-21 bucket table that used to sit here was DELETED, not updated.** Re-deriving the grouping
produced a different split on **twelve** of its ids, because the right axis is **who adjudicates**, not
where the work lives — the demand-triggered rows are adjudicable *immediately*, by the operator, and
belong nowhere near a bucket labelled "no". Re-derive again if you need it; do not restore a table.

**WHAT THE OPERATOR DECIDED, and the two that matter most are counter-intuitive:**
- ⚠⚠ **ALL TWELVE demand-triggered rows are `Activated`** (`DEC-067` / `SC-029`) — `DW-028 032 033 035 036
  038 039 040 061 063 068 069`. **This was AGAINST my recommendation to carry them**, and the rows record
  it as an override, not agreement. **v1 is materially larger than it was.** Three requirements returned
  `Deferred`→`Approved` in the same delta (`FR-032`, `FR-154`, `FR-155`) — mandatory, not tidying, see below.
- **`DW-037` was already activated a day earlier and nobody applied it** — `DEC-064` d2, Approved
  2026-08-19, reads *"DW-037 is ACTIVATED… It becomes real work"*, and the row still read `Open`. `SC-028`
  applied it and returned `FR-035` to `Approved`.

⚠⚠ **THE STRUCTURAL LESSON, and it will fire again: A REQUIREMENT'S `lifecycle_status` AND ITS `DW-` ROW'S
`lifecycle_status` ARE UNRELATED COLUMNS AND NOTHING COMPARES THEM.** No gate, no readiness rule, no view.
So a requirement reading `Deferred` (= not in v1) beside an `Activated` row for the same work is invisible
and simply sits. Found once, then found three more times in the same session by checking BEFORE writing.
**Whenever you activate a `DW-` row, check its requirement's status in the same breath.**

⚠ **`deferred-work-reviewed` CANNOT GO GREEN FROM REVIEWING** — read the predicate, don't assume it: it
selects `Open` **`Activated`** and `Scheduled` alike, with no "reviewed" field. Only *closing* removes a
row. Activating twelve left the advisory listing the same 53. **A review's deliverable is the recorded
judgement, not a colour change**, and the advisory staying red afterwards is correct.

**Item 2 — the blind controls — is HALF done.**
- ✅ **`assumptions-current` now DISCRIMINATES.** Twelve rows dated; it moved `indeterminate` → `fail`,
  naming **`ASM-011`** alone — the urgency SLA thresholds whose own text says they must be validated with
  the committee after the PH-1 pilot, which shipped long ago. ⚠ **The field is a FUTURE re-validation DUE
  date** (read the rule's SQL, not its description), so more will go red as dates pass. **That is the
  control working — do NOT clear dates to restore the amber.** Nine rows correctly carry no date.
- ⛔ **`DEF-087` IS UNTOUCHED and still needs its own clean context.** Its own row warns the obvious
  mechanical fix closes `WBS-20.4`, the email adapter, against a hard constraint — the `DEF-012`/`DEC-055`
  trap. **Do not fold it into a broad session.**

**ALSO SETTLED IN THE SAME INTERVIEW — all three flagged items were dispositioned, not carried:**
1. ✅ **`ASM-001` → `Superseded`.** Its statement is struck through and reads *"RESOLVED — FALSE
   (`ADR-0015`)"*; it had been sitting at `Approved` — a settled question wearing a live assumption's status.
2. ✅ **`DEF-092` widened AND the assumption half REPAIRED.** ⚠⚠ **I told the operator "four" and it was
   EIGHT** (`ASM-001 004 006 008 011 014 015 016`). **I measured inside the twelve rows I happened to be
   editing instead of across the register — measuring inside the set you are already holding is not
   measuring the register.** All eight titles restored from their statements after verifying the
   precondition (every statement starts with its truncated title; 8 undamaged rows already have
   title == statement). ⚠ `ASM-017` deliberately untouched — its 73-char title is real prose, and a
   mechanical "title differs from statement" rule would have destroyed it. **The requirement half is
   unchanged:** `NFR-006`, `FR-032`, `FR-153` have title AND statement cut, so no intact source exists.
3. ✅ **`DW-029` → `Done`**, under `DEC-064` d1's precedent (closed because the work SHIPPED). ⚠ Its
   trigger now states what Done does NOT mean: criteria were never written for all 162, it ran to the
   operator's end condition, and **its structural point still stands** — requirement status measures
   whether anyone WROTE a criterion. Making that authoritative needs a NEW row, not reopening this one.
4. ✅ **`LL-011` Approved and PINNED**; `lessons-confirmed` passes. ⚠ **`LL-007` AND `LL-012` ARE NOW
   `Superseded` BY `LL-013`** (operator, 2026-08-21: merge them). The store REFUSES an in-place edit of an
   approved lesson — *"approved/promoted lessons are immutable: supersede, never edit"* — so a merge is
   always a new row superseding the old ones. **Count the binding lessons, do not quote a number.**
   ✅ **`DEF-082` carried, NOT reconstructed** (operator's call): a plausible reconstruction from
   second-hand narrative reads exactly like a record. `DEF-101` documents the honest gap.
5. ✅ **Tiers one and three CONFIRMED by the operator — all 41 rows carried.** With the twelve activated,
   **all 53 open rows carry a recorded human judgement for the first time.**

### ▶▶ `WBS-23.3` IS SHIPPED (2026-08-21). THE NEXT BUILD ITEM IS `WBS-23.4`.

**`PE-578`** carries the CSP spike, **`PE-579`** the build. `AC-142` is Met (`AV-220`) against `ada5fe2`
(PR #304). **Do not re-run the spike and do not re-open the dependency question.** This block replaced a
`▶▶ DO THIS FIRST` that told a fresh session to run a spike that was finished and to install a package
that does not work here.

**THE THREE THINGS WORTH CARRYING, none of which is a task:**

- ✅ **DOM → `foreignObject` → data-URL SVG → canvas → PNG is CLEAN under `style-src 'self'`.** A parent
  document's `style-src` does not reach inside an SVG rendered as an image. **The "if it needs
  `unsafe-inline`, STOP and ask" condition NEVER AROSE** — `DW-022`'s finding stands and both nginx
  templates are untouched. If a later session reads `DW-038`'s brief and finds the prediction that inline
  styles in a foreignObject would be blocked: **that prediction is false and was measured to be false.**
- ⛔ **`modern-screenshot` 4.7.0 cannot run here** — `embedWebFont` appends a `<style>` to a scratch
  `createHTMLDocument()` that inherits the page CSP, then dereferences the null `.sheet`. Its `font:false`
  escape silently breaks the **Arabic** layout. ✅ **`html-to-image` 1.11.13 is what shipped** (`DEC-069`).
  ⚠ It has a catch path that `insertRule`s into the app's **live** stylesheet when a sheet's `cssRules`
  read throws — inert today because every stylesheet here is same-origin, live the moment one is not.
  ⚠ For a future capture, compute the font CSS ONCE with `getFontEmbedCSS` and pass `fontEmbedCSS`; the
  ar payload is ~1.63 MB per call otherwise. Deliberately not done yet — no measurement says it hurts.
- ⚠⚠ **`LL-014` is Approved and PINNED: registry metadata cannot rank correctness.** The carried-in
  dependency table ranked four packages on size, deps, downloads and freshness and marked the pick. Every
  number was right; the ranking **inverted** against the only axis that decided anything. **"The technique
  works here" never transfers to "this package works here."**

⚠⚠ **THE PRE-BUILD REGISTER SWEEP EARNED ITS KEEP AND YOU SHOULD RUN IT THE SAME WAY.** Sweeping
`adrs`, `decisions` and `open_questions` **BY KEYWORD** before writing a line found `DEF-105`:
`ADR-0022` clause 4 still read *"Export = client-side CSV only in v1"* and `WBS-14.5` marked `FR-142`
`(PH-2)`, against three later Approved decisions ordering the build. **An identifier sweep returns
nothing — `FR-142` and `ADR-0022` never name each other.** Resolved by `ADR-0044` + `SC-030`; `DEF-105`
is Fixed. ⚠ **The operator's instruction was "a new ADR superseding clause 4", and taking it literally
would have marked the `ADR-0022` ROW Superseded** — invalidating an id `DEF-102` and two source comments
depend on. The clause-only shape was put back to them and confirmed **before** it was applied. **When an
instruction names a clause, check whether the row or the clause is the thing that can be superseded.**

⚠ **Not measured, and do not let a later session assume otherwise:** Chromium only (149 and 151 — the
result reproduced across two builds), **no Firefox, no WebKit**; no whole-page multi-card export; no
memory measurement on the on-prem hardware. `AC-142` names all four exclusions in its own text.

### ▶ WHERE THE BUILD WORK STANDS (2026-08-21)

**`PH-7` → `SL-032`**, four small activated rows in `DEC-068` d1's order. **Measure the statuses; do not
trust this list** — `readiness_check(scope="slice", id="SL-032")`.

- ✅ **`WBS-23.1` / `DW-061`** — mobile notice. PR #301 → `78b5ca2`, `AC-140` Met (`AV-218`).
- ✅ **`WBS-23.2` / `DW-040`** — drag-to-reprioritize. PR #302 → `145d9bf`, `AC-141` Met (`AV-219`).
- ✅ **`WBS-23.3` / `DW-038`** — PNG chart export. PR #304 → `ada5fe2`, `AC-142` Met (`AV-220`).
  Needed `ADR-0044` + `SC-030` first; see the block above. `DW-038` is `Done`.
- ▶▶ **`WBS-23.4` / `DW-032` — THE LAST *LEAF* IN THIS SLICE.** Reach `Topic.Reclassify`; the domain
  method exists and is deliberately unreachable, and its guard is disjoint from Convert's so no topic can
  be a candidate for both. ⚠ **Read the code before believing that sizing** — it is the fourth `DW-` row
  in this slice whose brief may not survive contact, and the previous three did not.
  ⚠⚠ **CLOSING `23.4` WILL NOT MAKE `SL-032` READY BY ITSELF.** Slice-scope `wbs-done` names **`WBS-23`,
  the PARENT, as well as `WBS-23.4`** — check it yourself with
  `readiness_check(scope="slice", id="SL-032")`. The parent row needs its own transition once every leaf
  is `Implemented`, so expect `ready:false` after `23.4` merges and do not read that as a real blocker.

⭐⭐ **`DEF-087`'s fix-forward rule IS WORKING AND YOU CAN WATCH IT.** Slice-scope `wbs-done` named
**five** items when `SL-032` was created, then **four**, **three** and now **two** as `23.1`, `23.2`
and `23.3` closed. The same rule
returns **zero rows for all 28 closed slices**. First slice-scope rule here that has ever tracked
progress instead of passing over an empty set — **keep new work items carrying `slice_id`.**

⚠⚠ **TWO ROWS IN THIS SLICE HAD WRONG SIZING, AND BOTH SAID "only the gesture / blocked on nothing".**
`DW-040` needed a backend change (the operation was a SWAP — indistinguishable from a move at ±1, wrong
for any longer drag) *and* a new addressing mode (the kanban renders the **filtered, sorted, page-
truncated** backlog, so a client-computed position addresses a different sequence — it sends the
TARGET'S IDENTITY and the server resolves both ends; **a test exists whose whole purpose is to fail if
anyone "simplifies" that back to a delta**). `DW-038`'s is above. **Read the code before believing a
row's sizing** — that habit paid three times in this slice.

⚠ **Both acceptance criteria name what they do NOT cover, deliberately.** `AC-140` covers `NFR-063`'s
NOTICE clause only, not *no-broken-layout* across 52 routes. `AC-141` does not claim every topic is
reachable by the gesture. Do not read either as wider than it says.

⚠ **Watch the backend applock test.** The first CI run on #301 failed `AuditAtomicityTests.
Concurrent_commands_all_commit_without_forking_the_audit_chain` with *"audit-chain applock not
acquired"*; the re-run passed on **identical backend code**. Two observations of one commit range
disagreeing with itself. **If it recurs, file a defect** — the audit chain is hash-linked and therefore
inherently serialising, which `DW-053` names as the shape where throughput surprises you. **Do not
label it flaky on the strength of one more green.**

⚠ **New defects from this slice:** `DEF-103` **Fixed** (the kanban rendered a silent 25-row prefix;
now `KANBAN_PAGE_SIZE = 500` plus an actionable notice — the residual ceiling is named, not hidden).
`DEF-104` **Open, low** — **twelve** paged reads accept an unbounded caller page size and **two** cap
it, so the remedy already exists in-repo (`GetNotifications` uses `Math.Clamp` to a `MaxPageSize`
constant; search uses `Math.Min(take, MaxTakePerType)`). ⚠ Its row records that the first sweep keyed
on the identifier `PageSize` and returned **ten** — blind to the audit endpoint and `GetDecisions`,
which page with locals named `size` and `n`. **Not** bundled into `SL-032`: twelve modules plus tests
is a slice of its own.

### Open, and the operator's alone

- **`DW-066`** — migrate api and worker to an alpine/distroless base (`NFR-054`'s minimal-base clause,
  KEPT after the operator rejected relaxing it). ⚠ **The edit is two `FROM` lines; the RISK is not the
  edit** — alpine is musl, and this app does SQL Server native interop plus Arabic/English culture-aware
  work. **Full e2e leg, and verify Arabic FREETEXT end to end.** A green unit suite proves nothing here.
- **`DW-074` + `DEF-100`** — `NFR-019` mandates TLS on three internal hops; app↔Keycloak and nginx↔api run
  plaintext on the Docker network. **The operator KEPT the requirement rather than narrowing it**
  (`DEC-066`), so `NFR-019` stays `Approved` and correctly has **no AC**, and `DEF-100` stays **open
  deliberately**. ⚠ Not a config edit: service-to-service TLS needs a certificate story, and the public
  certbot flow does not extend to services addressing each other by compose name.
- **`NFR-018`** — the only remaining requirement real work could close, and it needs an **external OWASP
  ASVS 5.0 Level 2 assessment**. Preparable, not closable: an evidence pack mapping existing controls to
  the L2 chapters would stop an assessor starting cold.
- **The running-stack group** — `DW-065` (span PARENTING across modules, still unobserved), the ops group
  (`NFR-015 017 044 052 062`, `PE-485`), and much of `DW-043`…`DW-060`, several of which are measured FROM
  trace data. ⚠ **`DEF-099` is fixed, so traces now actually arrive** — that blocker is gone.
- **`release-close-out.md`** exists in the prompt library and has never been run. With every phase closed
  and production live, it is the ceremony that would formally end v1.

### ⚠ HOW TO RUN A STACK HERE — batch 17 proved this and the next stack batch should copy it exactly

`docker ps` showing no ACMP containers does **not** mean it is safe. **Five populated volumes exist**, and
`scripts/dev-up.sh` is `up -d --build` — the documented breaker, because SQL Server keeps the VOLUME's
original SA password and ignores the env value, so a recreate fails its healthcheck and the recorded
recovery is `down -v`, destroying all five.

1. Use an **isolated compose project on FRESH volumes**, same compose and env file, so the config under
   test is the shipped one while the dev stack's data is **structurally unreachable rather than avoided**.
2. **Tag an existing CI image** to the name compose expects, or it rebuilds the 3.62 GB FTS image.
3. Bring up `sqlserver` + `seq` **alone** and confirm healthy **before** the api.
4. Tear down `down -v` (yours, not the dev stack's) and **remove the tag**, so a later `up` cannot silently
   reuse a stale image. Then verify all five dev volumes are still there.

### ⚠⚠ TWO THINGS FROM THE FIRST `SL-032` BUILD — both cost a CI cycle, both generalise

**1. COMMITTING TO `main` IS NOT PUBLISHING TO `main`.** The branching rule says package writes go
straight to `main`, and I followed it faithfully for **ten commits** — and never pushed. So `main` was
ten ahead locally, the feature branch inherited all ten, and `gh pr merge --squash` folded **every
package commit plus the web change into ONE commit** titled after the web feature. No content was lost
(verified by stat); ten commit messages were. ⚠ **After a package commit on `main`, PUSH.** Check with
`git rev-list --left-right --count HEAD...origin/main` before branching — a non-zero left number means
the next branch will carry work that is not its own.

⚠ Trap 25 also fired on that merge exactly as written: merged **remotely**, aborted **locally**,
working tree left looking like pre-feature `main`. The documented recovery worked — verify the content
is in `origin/main` **by content, not ancestry**, then reconcile. ⚠ And back up `tamheed-package/data`
first: C31 means uncommitted package writes die to `reset --hard`, and there were some.

**2. ⚠⚠ ARABIC MORPHOLOGY BITES TEST ASSERTIONS, NOT JUST UI COPY — AND THE FAILURE MESSAGE HIDES IT.**
A Playwright assertion looked for a literal Arabic substring. CI reported it **missing from a received
string that visibly appears to contain it**. It genuinely was absent: the preposition `لـ` absorbs the
alef of the definite article `ال`, so the phrase renders contracted and the standalone spelling occurs
nowhere. `DEC-032` already records this rule — **Arabic morphology is a RULE, not a string
substitution** — but records it about *renaming UI copy*. A test assertion is a nastier venue, because
the diff output makes it look like a tooling fault. **Never assert a hand-picked Arabic fragment.
Assert the PROPERTY** — a run of `[؀-ۿ]` proves the bundle resolved, survives every
rewording, and pairs with a key-echo check. Verify any such regex discriminates: it must match AR, and
**not** match EN, and **not** match the literal key path.

### ⚠ NEW LESSON, and it changes how you write for the operator

**`LL-011` (Proposed — needs the operator's confirmation interview): AN IDENTIFIER IS A POINTER, NOT A
REFERENCE.** The first version of the disposition slate cited ~40 records by id alone and asked the
operator to rule on them. **They refused the interview on exactly that ground.** An id is an index into a
store the reader may not have open, so citing one hands the retrieval work to the person the artifact
exists to serve, at the moment they are deciding. Ids *read* as precision and are genuinely traceable —
for the agent, which can resolve every one in a tool call. The asymmetry is invisible from this side.
**Any artifact the operator reads to DECIDE must carry each cited record's own text where it cites it**,
quoted from the JSONL by a generator, never paraphrased and never re-typed. The test is mechanical: could
a reader who has never opened this package adjudicate every question using only the artifact?

⭐ **The remediation found a defect no gate can see.** Making the generator resolve every identifier in
every quoted record and fail loudly on the unresolvable turned up **`DEF-082`, which does not exist** —
yet two defect rows and a progress entry cite it as a real, diagnosed, fixed defect and restate its root
causes. The register runs 1–100 with **82 the only gap**. `G-IDS` passes because it checks foreign keys
and the entity index, **not identifiers embedded in prose**. Filed as `DEF-101`; the row is NOT
reconstructed, because writing a defect after the fact from second-hand narrative is close enough to
manufacturing a status that it is the operator's call. **Closing the reference graph over an artifact is a
cheap instrument no gate provides, and it found this on its first run.**

### Closed 2026-08-20 (later session) — do not re-carry these

The deferred-work disposition (`PE-556`, `PE-558`) · `DW-037`'s unapplied activation (`SC-028`) · the
`assumptions-current` blind control (12 dates, `PE-557`) · the twelve demand rows (`DEC-067`, `SC-029`).
**New rows to know:** `DEF-101` (missing `DEF-082`), `DEF-102` (`NFR-013` mandates a columnstore that
`ADR-0022` removed — operator chose *record it, change nothing*; the cluster also includes `DEC-020`,
`ADR-0003` and `OQ-040`, which all still assume it), `LL-011`, `SC-028`, `SC-029`, `DEC-067`.

⚠ **`DW-052`'s premise is WRONG and its "closeable today" half is not closeable** — the upload options
carry **50 MB** and **2 GB**, not `NFR-011`'s 100 MB, and nothing overrides either. An AC over the
validators would verify the wrong numbers.
⚠ **`DW-037`'s data claim was half wrong**: `Topic.Schedule` does **not** persist the meeting id — it
raises `TopicScheduledEvent`, which has **zero consumers**, and `Topic` has no such column. The calendar
work is genuinely unblocked but **must read from the Meetings API** (`MeetingDetailDto.ScheduledStart` +
`AgendaItemDto.TopicId`), never from the Topics API the row names.

### Closed earlier 2026-08-20 — do not re-carry these

`DEF-093` · `DEF-095` (#298) · `DEF-097` (#299) · `DEF-098` (by reconciliation, `SC-025`) · `DEF-099`
(#300 — the OTLP export defect) · `DW-026`, `DW-027`, `DW-059`, `DW-062`, `DW-064` · `OQ-074` ·
`NFR-023`, `NFR-026`, `NFR-027`, `NFR-028`, `NFR-034`, `NFR-050` · the `SL-031` programme itself and the
`PH-6` phase gate · risk owners (0 of 23 lack one) · the three customized prompts' hand-merge (nothing to
merge) · `prompts/README.md`'s stale version (refreshed via `handoff_emit(refresh_stock=true)` — it was
classified **stale-stock**, so the TOOL fixed it; a hand edit would have opted it out of every future
refresh, permanently and silently).

Report the state and your plan before writing, then proceed.

=====
