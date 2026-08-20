# Kickoff prompt — the durable one

Paste everything between the two `=====` lines into a fresh session. This file is durably named:
when the work changes, **edit it — do not create a new `prm-*.md`.**

=====

Read `tamheed-package/prompts/README.md` (the operator guide) and `AGENTS.md` before anything else.

```
server_info()                                # expect tamheed 4.4.1, root = C:\Users\ahammo\Repos\acmp
package_open("tamheed-package")
gate_run()                                   # 7/7 is the NORM again (tamheed >= 4.4.2). A red gate is a
                                             # REAL finding - read its failure list, it names the token.
readiness_check("package")                   # blocking rules should all pass; advisories will fail and
                                             # that is normal. ready:FALSE = a real blocker, go read it.
git status --porcelain -uall                 # expect clean; you are on `main`, everything is merged
```

⚠ **Do not trust any tally written into a prompt, including this one.** Read the live numbers. This
file has carried a stale tally **eight** times — and **three of those were on 2026-08-19, written and
then invalidated within the SAME session** by the very work the session was doing: it said
`readiness ready:TRUE` and `gate 7/7` hours after `DEF-093` made both false, and its requirement tally
went stale the moment batch 13 recorded two verdicts. A prompt that restates a number is a prompt that
will lie to you. **Point at the live check, do not quote it.** If you find this section wrong again,
**fix it in the same session and bump this count.**

⚠ The eighth fix (batch 13) stopped patching the numbers and **deleted them**, replacing each with the
command that measures it. A tally you can re-type is a tally that will go stale again; a command cannot.

---

## §1 — What is true right now (2026-08-20)

**THE BUILD LADDER IS FINISHED. THE ACTIVE WORK IS A REGISTER PROGRAMME, NOT A FEATURE.** `P1`–`P19`
is complete; every phase is closed except `PH-3`, frozen deliberately (`WBS-20.4` is the email adapter
against a hard constraint — **do not "repair" it**). `PH-6` (`DEC-060`) holds the 2026-08-18 activations:
its two build slices are `Implemented`, and **`SL-031` is `Approved` and IN PROGRESS** — that is §2b,
and it is where you start.

**You are on `main`, clean, nothing unpushed, and CI on `main` is green.** There is no feature branch —
batch 13's two PRs both merged: **#296 → `57e019d`** and **#297 → `a6261bf`**, each verified on `origin/main`
BY CONTENT rather than by ancestry (trap 25).

### What batch 13 landed (2026-08-19/20) — reference, do not redo

- **Four ACs Met**: `AC-131` NFR-021 (server-side validation, no concatenated SQL, gating SAST), `AC-132`
  NFR-049 (happy + failure path per canonical workflow), `AC-133` NFR-043 (all four span kinds instrumented),
  `AC-134` NFR-053 (config externalization, as narrowed).
- **`SC-023` Merged** — `NFR-053` was NARROWED to match `ADR-0037`. Its text now excludes the SPA bundle's
  OIDC build args BY NAME. `DW-064` closed by reconciliation, not construction; nothing was built.
- **PR #296 (`57e019d`)** — DB / outbound-HTTP / job-dispatch spans + the worker's first-ever OpenTelemetry
  registration. **PR #297 (`a6261bf`)** — Hangfire job-EXECUTION spans via a hand-rolled `IServerFilter`.
  ⚠ That second one satisfies **no requirement clause** — `NFR-043` covers dispatch only and was already Met.
  It exists because the operator chose it over the prerelease instrumentation package. Do not look for an AC.
- **`findings_21.md`** — the `DEF-093` maintainer report. It found a SECOND defect: the `corrects` column is
  written by `progress_update` and rendered by `export_html` and **read by nothing else**, so a correction
  entry changes no gate, no readiness rule and no view. That is why appending `PE-470` never cleared anything.
- **New rows**: `DW-062` Done · `DW-063` (NFR-010 not configuration-driven) · `DW-065` (no trace ever
  OBSERVED) · `DEF-095` (worker csproj cites a base image it does not use) · `DEF-096` (see below).
- **`NFR-054` MEASURED, not estimated** (`DW-059` Done, `DEF-096` open): web 51.1 MB, worker 245 MB, api
  257 MB, **sqlserver-fts 3.62 GB**. The 500 MB cap fails on the database image by **7.2x and cannot be made
  to pass** — its base alone is 1.67 GB. The minimal-base clause fails on a DIFFERENT set (api and worker are
  Debian `aspnet:8.0`, web genuinely is alpine). **Two clauses, disjoint image sets, one operator disposition
  needed.**

- **`SL-029` (FR-030 topic conversion)** — merged as `bcc3d00` (#293) + `b065a29` (#294), `AC-113` Met.
- **`SL-030` (Confidentiality ABAC + egress redaction, FR-163)** — `AC-114` Met (`AV-192`), slice and
  all of `WBS-22.*` `Implemented`, merged as **#295 → `1a52dba`**. §2 is the design record.
  **Do not reopen either slice.**

### ✅ THE MECHANICAL GUARANTEE IS BACK — the old "never report gates green" rule is RETIRED

**`gate_run()` returns 7/7 and `readiness_check("package")` is `ready:TRUE`** on tamheed **4.4.2**
(2026-08-20). `DEF-093` is **Fixed upstream, with ZERO data changes to this package** — `PE-469`'s text is
untouched and still quotes the token; the screen simply no longer reads it. `G-COMPLETE` now exempts the
append-only report columns (`progress_entries.entry`, `audit_verdicts.evidence`) and skips
`Superseded`/`Obsolete` rows.

⚠ **The old standing instruction — "never report gates green, reconcile the failure LIST instead" — is
RETIRED by operator decision.** It existed only because the guarantee was gone. **A red gate is now a real
finding again; report it as one.** Still reconcile the list when one IS red — that habit was never the
problem.

⚠⚠ **THE TOKEN RULE CHANGED IN ONE DIRECTION ONLY, and getting this backwards re-creates the bug.**
**Journal text is now EXEMPT** — a progress entry or verdict evidence may quote marker tokens freely, so
describing why a scan fired is finally safe. **ENTITY rows are still fully screened** — `title`,
`statement`, `description`, every live row of every family. There, name the concept or wrap the token in
backticks (`_strip_code` runs first, and the escape is verified working).

⚠ **This was VERIFIED, not taken on trust, and the check that mattered was not the green one.** An
exemption that turns a permanently-red gate green could equally have made it **vacuous** (`LL-007`).
Injecting a marker into a LIVE entity title made `G-COMPLETE` fail and **name the matched token**; the
backticked form passed; the row was restored and the whole package re-verified byte-identical. **If you
ever doubt a gate, that experiment is three tool calls and it is the only thing that separates "green
because fixed" from "green because blind".**

### The advisories — none is a task

`defects-minor` (just **`DEF-087`**), `deferred-work-reviewed` (**grows on purpose — the DW-029 programme
ADDS a row every time it finds a partial, and closing one to green it manufactures status; batch 13 alone
added three**), `acs-slice-bound` (`AC-109`–`AC-112`, accepted by `DEC-058 d3`). ⚠ **Read them live; the
row COUNT is deliberately not written here because it moves every batch.**

⚠ **Slice-scope `defects-closed` is `indeterminate`** — almost no defect row carries `found_in`
(`DEF-087`), so the slice-scope rule cannot discriminate. Package-scope `defects-closed` **passes** again
now that `DEF-093` is Fixed, so the superset argument works once more — but it only ever ruled out
**critical/high**. Open **medium/low** defects are real and sit in the `defects-minor` advisory: currently
`DEF-087`, `DEF-095`, `DEF-096`. **Read that advisory; do not read a green `defects-closed` as "nothing is
open".**

**Seven lessons are Approved and PINNED** (`LL-001`…`LL-007`) and bind every session via the tool-owned
note. `LL-007` — **a checker that reports success may have had nothing to check** — keeps firing on my OWN
instruments: a grep scoped to a directory that does not exist, a check swallowed by `|| true`, a scanner
regex that matched TypeScript `>=`, a cwd-relative gate path, a search for validator FILES that returned 1
where 78 exist, and a coverage check that SUMMED per-project reports and reported 20% for a file at 100%.
**Widen the instrument and prove it has a subject before believing ANY empty result — and when a measurement
indicts known-good code, suspect the measurement first.**

⚠⚠ **`LL-005` FIRED FROM AN ANGLE IT HAD NOT BEFORE, AND IT NEARLY COST AN ADR.** The lesson says to sweep
the register before asking the operator to dispose of a capability. Batch 13 swept the REQUIREMENT register
thoroughly and never swept the **decision** or **open-question** registers — then offered the operator a
"build it now" option for `NFR-053`. They took it. **`ADR-0037` (Approved, ratified) decides that exact thing
in its decision clause, and its consequences name the very fix as deferred under `OQ-061`.** One file-read
— noticing a bare ADR reference in a docker-compose COMMENT — separated that from implementing a change
that reverses an Approved ADR. **SWEEP `adrs`, `decisions` AND `open_questions`, NOT JUST `requirements`,
BEFORE WRITING A `DW-` ROW OR OFFERING THE OPERATOR AN OPTION.**

## §2 — What `SL-030` built (reference only — do NOT rebuild)

**All of it is mutation-proven.** Read `AV-192` for the full evidence.

| Piece | Where |
|---|---|
| `Topic.IsRestricted`, `Restrict`/`Declassify`, `TopicRestrictedEvent`, migration | `Topics.Domain/Topic.cs`, `Migrations/20260818193742_Topics_AddIsRestricted.cs` |
| `IConfidentialResource` contract (a **declared primitive**, ADR-0001/0021) | `Acmp.Shared/Authorization/Abac/AbacResources.cs` |
| `ConfidentialityRequirement` + handler, registered on **`TopicEdit` only** | `Abac/ConfidentialityRequirement.cs`, `AuthorizationRegistration.cs` |
| Read predicate + per-request scope resolver | `Topics.Application/Internal/TopicVisibilityQuery.cs`, `Abstractions/ITopicVisibility.cs` |
| Applied to `GetBacklog`, `GetTopicDetail` (**404 not 403**), `TopicSearchProvider` (before `.Take`), `TopicReader` ×3, `TopicStreamReader` | those files |
| Classify command + `PUT /api/topics/{id}/confidentiality` | `Features/SetTopicConfidentiality/`, `TopicEndpoints.cs` |
| SPA badge + segmented classify control, EN/AR | `TopicDetail.tsx`, `EditTopic.tsx`, `topics.css` |
| **Egress port** `ITopicConfidentiality` + `TopicConfidentialityReader` | `Acmp.Shared/Contracts/Topics/`, `Topics.Infrastructure/Persistence/` |
| **Agenda masking** (in place) | `Meetings.Application/Internal/MeetingMapping.cs`, `GetMeetingDetail` |
| **Edge dropping** (relationships + dependencies) | `GetArtifactRelationships`, `Dependencies.Application/Internal/DependencyVisibility.cs` ×3 handlers |
| **Localized redaction placeholder**, EN/AR + aria | `AgendaBuilder.tsx`, `MeetingWorkspace.tsx`, `meetings.restrictedKey`/`restrictedTitle` |

### The design decisions, so nobody re-litigates them

- **The port answers with the WHOLE hidden set, not "which of these ids".** `GetDependenciesRegister`
  pages and reports a total, so the filter must compose before `CountAsync`/`Skip` — the page does not
  exist yet when the question is asked. A `Guid[]` becomes an IN clause.
- **The hidden set is DERIVED from `VisibleTo` by subtraction**, so the visibility rule still has
  exactly one expression. `The_hidden_set_is_exactly_the_complement_of_VisibleTo` guards that.
- **Two shapes, for a structural reason.** Agenda items are **masked in place** (the slot means
  something without its topic — order, time-box, "item N of M"). Edges are **dropped** (an edge IS a
  pointer; a blanked endpoint enters `ImpactGraphComposer`'s BFS as a node keyed on an empty Guid).
- **Both endpoints are filtered, not just the far one.** That is what makes a hidden focus
  response-identical to a nonexistent id — there is **no separate focus guard** anywhere, by design.
- **`TopicId` survives masking on purpose.** The SPA keys agenda rows by it; blanking it collides two
  masked rows onto one React key. It leaks nothing — topics are read by KEY, which already 404s, and
  no read-by-guid route exists.
- **Key and title go out EMPTY, never the word "Restricted".** A server-side English string breaks the
  EN+AR guardrail. The SPA localizes the blank **and substitutes it into the aria-labels** — an empty
  accessible name is a WCAG failure no text query would surface.
- **`MoveTopicPriority` is deliberately NOT filtered** and says so in a comment: `BacklogPrioritize`
  admits Chairman/Secretary only, both committee-wide readers, and filtering would renumber the column
  as if hidden topics were absent — corrupting the order for the people entitled to see them.

### ⚠ THE ONE THING THAT NEEDS YOU: `SC-021`, Proposed

The 2026-08-19 sweep read each surface instead of trusting the list, and **the list was wrong in both
directions** (`LL-006`):

1. **Notification bodies are NOT a leak and nothing was built for them.** `TopicNotifications.cs`
   builds three messages and **none carries a topic TITLE** — only the KEY — and every recipient set
   is the Secretary roster or the topic's own submitter, i.e. committee-wide readers or the owner.
   `MeetingNotifications`/`MinutesNotifications` interpolate the **meeting** title. The earlier
   instruction that "the builders must take a restriction flag" was mistaken.
2. **The Dependencies module was a FOURTH surface nobody listed** (`DEF-090`). `Dependency.FromTitle`
   /`ToTitle` are the same create-time topic snapshots, read by three read-all handlers — and the
   **Reports** surface `AC-114` names is what loads that register. Built here on that reading.

**`SC-021` is APPROVED and Merged** (operator, 2026-08-19; both halves). ⚠ **THE DELTA LIVES IN THE
SC ROW, NOT IN `WBS-22.3`.** The operator was offered a variant that would rewrite `WBS-22.3`'s title
and note first and chose the plain approval instead, so **that row still reads "notification bodies"**
and `SC-021` plus its `scope_modifies` edges (→ `WBS-22.3`, → `AC-114`) are the record of the
correction. **Follow the edge; do not read the unedited row as drift**, and do not "tidy" it.

**Also fixed on the way:** `DEF-091` — the branch had been **RED since `ecfd63f`**, ten commits, with
`npm run build` failing on 13 TypeScript errors while `vitest` stayed green (it transpiles, it does
not typecheck). See trap 22b — this is what `LL-007` generalises.

## §2b — What is NEXT: the DW-029 acceptance-criterion programme

**`SL-031` is OPEN and this is the active work.** `PH-6`'s two build slices are done; this is the
register programme the operator activated on 2026-08-19 (`DEC-064 d5`).

**Live position — MEASURE IT, there is deliberately no number here.** The register moves every batch, so
this file no longer carries a copy. Read it with `entity_query("requirement", columns=["id","kind",
"priority","lifecycle_status"], limit=250)` and count by `lifecycle_status`: `Approved` means **no AC was
ever written** and is the programme's target set; `Implemented` means an AC exists and its verdict
carried the requirement forward; `Deferred` is out of scope. Cross-reference the `Approved` ids against
`entity_query("deferred-work")` to see which are already carried by a `DW-` row — the remainder is the
work.

### The method — follow it, it was expensive to learn

1. **Never leave a Pending or Partial AC.** `acs-met` counts by `retired_in` and IGNORES
   `lifecycle_status`, so an AC written ahead of its evidence holds package readiness false **forever**
   (trap 16c). Write the AC and record the verdict IN THE SAME BATCH.
2. **A part-verified requirement gets a `DW-` row, NOT an acceptance criterion.** This is settled and is
   why `DW-041`, `DW-042`, `DW-043`…`DW-061` exist. Do not "helpfully" add the missing ACs.
3. **Verify the WHOLE criterion, not the convenient part.** Both NFRs in batch 1 carried a `Target:`
   clause stronger than the tests that existed; the batch BUILT the missing control rather than writing
   an AC around what happened to be tested.
4. **Label the method honestly.** `auto-test` where a test proves it, `inspection` where you read
   config. Say what is NOT claimed — several verdicts here name a clause they deliberately do not cover.
5. **The cheap filter is exhausted.** Selecting candidates by grepping tests for requirement ids returns
   ZERO, because every requirement a test names already HAS an AC. Do not hunt for a cleverer one; each
   remaining requirement needs its source read.

### ⚠ The programme's real yield is PARTIALS, not verdicts

Nine requirements have been found built-on-one-side-only, every one invisible in the register **because
it had no AC to fail**: `FR-142` (`DW-038`), `FR-117` (`DW-039`), `FR-037` (`DW-040`), `NFR-030`
(`DW-041`), `NFR-035` (`DW-042`, since BUILT and closed), `NFR-063` (`DW-061`), and from batch 13
`NFR-043` (`DW-062`), `NFR-010` (`DW-063`) and `NFR-053` (`DW-064`). `NFR-025` was worse — a **Must**
security requirement divergent on both clauses, fixed under `DEF-094`.

⚠ **Batch 13's `DW-064` is the sharpest one yet, and the divergence was written in a COMMENT the whole
time.** `deploy/Dockerfile.web` bakes `VITE_OIDC_AUTHORITY` into the SPA bundle at build time and says so
in its own words, so promoting one web image between environments is impossible — the exact operation
`NFR-053` forbids. The same file templates the nginx CSP origins at container START via envsubst and
comments that this is so each environment supplies its own with no rebuild. **The principle was
understood and applied one layer down.** Nothing failed, because there was no AC to fail.

⚠ **Batch 13's most important result was a defect that was NOT filed** — the counterweight to the above.
A census for `NFR-021` read as a 38-command server-side-validation hole, the shape of a critical security
finding. Reading the four commands that actually carry scalar input showed all four guarded in the
DOMAIN instead: `SetTimebox` clamps to 5..120, `MoveItem` bounds-checks the target index,
`RecordActualMinutes` floors negatives at 0, `DeleteRecording` uses its string only as an EF equality
predicate. **The property held; only the MECHANISM was non-uniform.** Filing off the census alone would
have been wrong. Read the implementation — in BOTH directions.

### What is queued

- **The code-verifiable Must-priority NFRs are the highest-value remaining batches.** After batch 13:
  `NFR-018 019 023 026 027 028 031 032 033 034 037 038 039 050 061`.
  ⚠ **`NFR-028` HAS A HEAD START — see `PE-501`.** Its TRACE half is done: SQL query PARAMETER values are
  not emitted (the experimental flag is implicitly false and nothing opts in), but `db.query.text` IS on
  every DB span, which is safe only because `AC-131` established universal parameterization. **One residual
  is open and is honest:** EF Core can inline constants, so a value could in principle land in the statement
  text — unobservable without a captured trace (`DW-065`). The **Serilog half** (`SensitiveDataMaskingEnricher`,
  and the no-names/emails/vote-content claim) is UNREAD. Do not write a verdict covering only the trace half.
  ⚠ **`NFR-027` IS NOT A FRESH READ — see `PE-493`.** Three rows name it and NONE covers it: `SC-019` and
  `DEF-086` mention it only to DISTINGUISH it, and `DEF-094` fixed the ADJACENT `NFR-025` by narrowing the
  recording role gate and adding read auditing. **Its subject matter was actively changed**, so read the code
  at its current state, not at whatever a pre-`DEF-094` reading would have found. A mention is not a coverage.
  ⚠ **DERIVE THIS LIST, DO NOT TRUST IT — AND MIND THE SECOND SUBTRACTION.** The rule is: the `Approved`
  Must-priority non-functional ids, minus every id named in a `deferred-work` row, **minus the
  ops/runtime group `PE-485` names** (`NFR-015 017 044 052 062`), which is uncovered by any `DW-` row but
  is separately blocked on a RUNNING STACK and is not code-verifiable. The first subtraction alone yields
  **twenty**, not fifteen. ⚠ This correction was itself found by RUNNING the rule against the written
  list rather than trusting that the two agreed — they were authored in the same edit and did not. Four are known-hard and were excluded from batch 13
  on purpose: `NFR-018` needs a DAST scan and a penetration test, `NFR-019` needs a TLS scan against a
  running stack, `NFR-023`'s own text defers to an org security policy that has never been confirmed
  (operator-blocked), and `NFR-038` rides on `P14`, deferred indefinitely by `DEC-028`. `NFR-031`–`034`
  are the WCAG group and want a browser, not a reader.
- **Batch 13 closed five of the original twenty**: `NFR-021` (`AC-131`) and `NFR-049` (`AC-132`) Met;
  `NFR-043`, `NFR-010` and `NFR-053` partial, recorded as `DW-062`/`DW-063`/`DW-064`.
- **18 performance NFRs are already recorded** as `DW-043`…`DW-060`, one row each. ⚠ Three carry a
  WARNING rather than a sizing — `DW-057`, `DW-058`, `DW-054` — read those before measuring anything.
- **The ops/runtime group is unfinished.** The operator asked for it to be verified against a RUNNING
  STACK and **no stack was started** — see `PE-485` for exactly what is settled and what is not.
- `DW-041` (manual WCAG pass) and `DW-061` (mobile notice) are small and self-contained.

### ⚠ Two package facts that will bite you

- **`G-COMPLETE` IS GREEN AGAIN** (tamheed 4.4.2, `DEF-093` Fixed). Report a red gate as the real finding
  it now is. The old "never say green" rule is retired — see §1.
- **G-COMPLETE still screens EVERY LIVE ENTITY ROW** — `title`, `statement`, `description` — for
  placeholder markers, and now NAMES the matched token in the failure. **Journal text is exempt**
  (`progress_entries.entry`, `audit_verdicts.evidence`), so a progress note explaining why a scan fired is
  safe. In an entity row, name the concept or backtick the token.

## §3 — The two registers that will mislead you

- **Requirement status measures whether anyone WROTE an acceptance criterion, not whether the thing
  was built.** A requirement advances only via the AC auto-advance trigger, so one with no AC can
  never leave `Approved` however well it shipped. ⚠ **The three counts MOVE EVERY BATCH and are
  deliberately absent from this file — measure them (§2b says how), never quote a written one.**
  Since 2026-08-18 the three labels finally denote different things.
- ⚠ **The `mvp` / Phase attributes record the ORIGINAL scoping and the ladder outgrew them.** Of the
  53 `mvp=0` requirements once described as "deliberately not built", **about 30 are built and
  shipped**. `SC-020` reclassified only the **24 verified absent from source**. See `LL-006`.
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
   SHELLS** — present, routed, well-commented, and drawing nothing; their own headers say so. Check
   **both** directions: the sweep also found `FR-032` unbuilt inside the "presumed built" group.
   Requirement ids cited in source comments are strong evidence of being BUILT, but the instrument is
   **positive-only** and one citation was a **deferral note** (`InvariantStatus.cs:7`).
2. **A measurement that indicts known-good code is measuring itself.** ⚠ Fired again 2026-08-18:
   `grep -c "Convert/3"` returned 2 and nearly read a deleted allowlist entry as surviving — the hits
   were the **comment explaining the deletion**. Match the quoted entry, not the substring.
3. **The LSP diagnostics panel is stale constantly** — it fired **four** times last session, once for
   a file that did not exist and repeatedly for symbols added on the current branch. **Build before
   believing it.**

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
   `indeterminate` (0 of 88 defects carry `found_in`). Answer from the **superset** instead:
   package-scope `defects-closed` passes, so no open critical/high defect exists anywhere.
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

⚠ Before offering the operator a disposition for a capability, **sweep the requirement register
first** (`LL-005`). ⚠ Scope changes, waivers and `force` are the operator's alone, and the interview
runs **every** time (`LL-002`) — plan approval is not scope-change approval.

### ⚠ Rules specific to the DW-029 programme (§2b) — they override habit

- **Never leave a Pending or Partial acceptance criterion.** `acs-met` counts by `retired_in` and
  ignores `lifecycle_status`, so an AC written ahead of its evidence holds readiness false **forever**.
  Write the AC and record its verdict in the SAME batch, or do not write the AC.
- **A part-verified requirement gets a `DW-` row, not an AC.** Settled 2026-08-19. If you cannot
  evidence the WHOLE criterion today, a deferred-work row with a real activation trigger is the honest
  instrument — and say so IN the row, so the next session does not "helpfully" add the missing AC.
- **Say what you are NOT claiming.** Several verdicts here name a clause they deliberately do not cover
  (`NFR-051`'s 48-hour feed latency, `NFR-042`'s every-entity scope, `NFR-035`'s attribute strings).
  A verdict that quietly covers less than its criterion is the failure this programme exists to end.

✅ **BRANCHING IS SETTLED — stop asking.** Operator decision, 2026-08-20: **split by content.** Package,
prompt and memory writes go **straight to `main`** (both CI workflows path-ignore `tamheed-package/**` and
`.claude/**`, so they cannot redden anything). **Anything touching CODE goes branch → PR → green CI →
squash-merge.** Batch 13 followed this and it worked; PRs #296 and #297 are the examples.

## §6 — THE CARRIED LIST. This section IS the list; nothing is carried in conversation.

Operator decision, 2026-08-20: carried items live HERE, reconciled against the live register, because the
previous list existed only in chat and had gone **five-sixths stale** (`PE-500`). **Reconcile this section
whenever you close one — a list nobody maintains is worse than no list.**

**Open, and the operator's alone:**
- **`SC-024` — Proposed, awaiting verbatim text approval.** Narrows `NFR-054` so both clauses match what
  ships. ⚠ **It RELAXES a security-adjacent clause** — dropping the alpine/distroless prescription makes
  Debian `aspnet:8.0` compliant. The row spells the trade-off out and says to REJECT it if the stricter
  standard should be kept. Closes `DEF-096` when Merged.
- **`DEF-095`** — the worker csproj cites a base image it does not use. `DEF-096` established the intent was
  real, but the base that comment names still would not work (the worker resolves an AspNetCore framework
  reference transitively). Repair direction is the operator's. ⚠ **Do not change a base image to close
  anything here** — alpine is musl, and SQL Server client libs and globalization are what break.
- **`DW-065`** — `NFR-043` has never been OBSERVED as an actual trace. Needs a running stack, like the ops
  group in `PE-485`. ⚠ Several of the `DW-043`…`DW-060` performance rows are measured FROM that trace data.
- **`DEF-087`** — the third row in the `defects-minor` advisory: almost no defect row carries `found_in`, so
  slice-scope `defects-closed` is permanently `indeterminate`.

**Closed 2026-08-20 — do not re-carry these** (`PE-500`, each verified individually):
`DEF-093` (fixed upstream in 4.4.2; `findings_21.md` is the worked example of reporting a tool defect
precisely enough to get it fixed) · `DW-026`, `DW-027` (Done) · `OQ-074` (resolved 2026-08-15, `DEC-048
d4`) · risk owners (**0 of 23** risks lack one; the advisory passes) · "the 20 unbound ACs" (**no reading
gives 20** — slice-unbound is 4, never-`work_bind`'d is 79, no-trace-edge is 33) · the three customized
prompts' hand-merge (**nothing to merge**: 4.4.x touched none of them; `orient-resume` is 100% and
`slice-review` 99% identical to stock once the `{package}` placeholder is normalised, and `integrity-check`
is a strict SUPERSET of 4.2.1 stock).

Report the state and your plan before writing, then proceed.

=====
