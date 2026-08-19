# Kickoff prompt — the durable one

Paste everything between the two `=====` lines into a fresh session. This file is durably named:
when the work changes, **edit it — do not create a new `prm-*.md`.**

=====

Read `tamheed-package/prompts/README.md` (the operator guide) and `AGENTS.md` before anything else.

```
server_info()                                # expect tamheed 4.4.1, root = C:\Users\ahammo\Repos\acmp
package_open("tamheed-package")
gate_run()                                   # expect 7/7
readiness_check("package")                   # expect ready:FALSE - DEF-093, see §1
```

⚠ **Do not trust any tally written into a prompt, including this one.** Read the live numbers. This
file has carried a stale one **five** times. If you find this section wrong again, **fix it in the
same session.**

---

## §1 — What is true right now (2026-08-19)

**`PH-6`'S TWO BUILD SLICES ARE DONE; `SL-031` IS OPEN AND IS THE ACTIVE WORK** (§2b).
The build ladder is finished — what remains is the register programme, not a feature. `P1`–`P19` is complete and every phase is closed
except `PH-3`, frozen deliberately (`WBS-20.4` is the email adapter against a hard constraint —
**do not "repair" it**). `DEC-060` created `PH-6` for the work the operator activated on 2026-08-18;
both of its slices are now `Implemented`.

- **`SL-029` (FR-030 topic conversion)** — merged as `bcc3d00` (#293) + `b065a29` (#294), `AC-113` Met.
- **`SL-030` (Confidentiality ABAC + egress redaction, FR-163)** — `AC-114` Met (`AV-192`), slice and
  all of `WBS-22.*` `Implemented`, shipped in **#295**. §2 is the design record. **Do not reopen either.**
- ⚠⚠ **`gate_run()` IS NOT 7/7 AND CANNOT BE.** `G-COMPLETE` fails permanently on exactly
  `[{id: PE-469, column: entry}]` — `DEF-093`, an append-only journal entry that quotes a placeholder
  marker while documenting the rule about them. No tool an agent has can clear it. **Never report
  "gates green"; reconcile the failure LIST instead** — an unexpected id in it is a real finding hiding
  behind a known one.
- ⚠⚠ **`readiness_check("package")` IS `ready:FALSE`, AND THAT IS CORRECT.** `defects-closed` fails on
  **`DEF-093`** — the same gate deadlock, recorded at HIGH severity because a mechanical gate that can
  never go green defeats the gate. An open high defect blocks readiness BY DESIGN, so this is the
  register working, not a regression. It clears when `DEF-093` does. Everything else blocking passes,
  including `acs-met`.
- **Three advisories fail and NONE is a task** — `defects-minor` (now just **`DEF-087`**, the vacuous
  slice-scope `wbs-done`), `deferred-work-reviewed` (**43 rows**, up from 22 — the DW-029 programme
  deliberately ADDS rows, and closing one to green it manufactures status), and `acs-slice-bound`
  (`AC-109`–`AC-112`, accepted by `DEC-058 d3`). ⚠ **Read these live** — the numbers move every batch.
- ⚠ **Slice-scope `defects-closed` is `indeterminate`, not a pass** — 0 of 90 defect rows carry
  `found_in` (`DEF-087`). Answer from the **superset**: package-scope `defects-closed` passes, so no
  open critical/high defect exists anywhere.
- **Seven lessons are Approved and PINNED** (`LL-001`…`LL-007`), binding every session via the
  tool-owned note. `LL-007` is the newest — **a checker that reports success may have had nothing to
  check** — and it fired **five more times** on 2026-08-19, every time on my own instrument: a grep
  scoped to the wrong directory, an inline check swallowed by `|| true`, a scanner whose regex matched
  TypeScript comparison operators, and a gate whose path was cwd-relative so CI would have scanned
  nothing. **Widen the instrument and prove it has a subject before believing any empty result.**

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

**Live position (read it, do not trust this line):** 226 requirements — **82 Implemented, 119 Approved
with no AC, 25 Deferred**. The 119 are the target. Of the Must-priority remainder, **47** are left.

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

Six requirements were found built-on-one-side-only, every one invisible in the register **because it had
no AC to fail**: `FR-142` (`DW-038`), `FR-117` (`DW-039`), `FR-037` (`DW-040`), `NFR-030` (`DW-041`),
`NFR-035` (`DW-042`, since BUILT and closed) and `NFR-063` (`DW-061`). `NFR-025` was worse — a **Must**
security requirement divergent on both clauses, fixed under `DEF-094`.

### What is queued

- **22 code-verifiable Must-priority NFRs** — the highest-value remaining batches.
- **18 performance NFRs are already recorded** as `DW-043`…`DW-060`, one row each. ⚠ Three carry a
  WARNING rather than a sizing — `DW-057`, `DW-058`, `DW-054` — read those before measuring anything.
- **The ops/runtime group is unfinished.** The operator asked for it to be verified against a RUNNING
  STACK and **no stack was started** — see `PE-485` for exactly what is settled and what is not.
- `DW-041` (manual WCAG pass) and `DW-061` (mobile notice) are small and self-contained.

### ⚠ Two package facts that will bite you

- **`G-COMPLETE` IS PERMANENTLY RED on `[PE-469]`** (`DEF-093`) and cannot be cleared by any tool an
  agent has — the journal is append-only and a correction does not satisfy the scan. **Do not report
  "7/7" or "gates green".** Reconcile the failure list every run; an id you did not expect in it is a
  REAL finding hiding behind a known one. Disposition: raise with the tamheed maintainer.
- **G-COMPLETE scans text for placeholder markers**, in entity `title` AND progress `entry`. Describing
  placeholder-shaped work will trip it. Name the concept, never the token — that is how `PE-469` became
  unfixable.

## §3 — The two registers that will mislead you

- **Requirement status measures whether anyone WROTE an acceptance criterion, not whether the thing
  was built.** A requirement advances only via the AC auto-advance trigger, so one with no AC can
  never leave `Approved` however well it shipped. Live: **64 `Implemented` / 137 `Approved` /
  24 `Deferred`** — and since 2026-08-18 those three finally denote different things.
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

Report the state and your plan before writing, then proceed.

=====
