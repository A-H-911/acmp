# Kickoff prompt — the durable one

Paste everything between the two `=====` lines into a fresh session. This file is durably named:
when the work changes, **edit it — do not create a new `prm-*.md`.**

=====

Read `tamheed-package/prompts/README.md` (the operator guide) and `AGENTS.md` before anything else.

⚠⚠ **ORIENT IN THIS ORDER — THE BRANCH BEFORE THE PACKAGE. THIS IS NOT THE USUAL ORDER AND THE
REASON IS NEW.** There is unmerged work on a feature branch, and **the package rows for it live on
that branch, not on `main`** (`AC-114` is absent from `main`). The package data is git-tracked (C31),
so the store loads whatever the working tree holds. Open the package while on `main`, then check out
the branch, and the next `entity_upsert` **refuses the whole batch** with *"data/ changed on disk
since this session loaded it"*. That refusal is the tool working; it happened on 2026-08-18 and cost
a close/merge/reopen cycle.

```
git checkout feat/sl-030-confidentiality     # FIRST — see the warning above
server_info()                                # expect tamheed 4.4.1, root = C:\Users\ahammo\Repos\acmp
package_open("tamheed-package")
gate_run()                                   # expect 7/7
readiness_check("package")                   # expect ready:FALSE — see §1, this is CORRECT
```

⚠ **Do not trust any tally written into a prompt, including this one.** Read the live numbers. This
file has carried a stale one **five** times. If you find this section wrong again, **fix it in the
same session.**

---

## §1 — What is true right now (2026-08-19)

**`PH-6` IS OPEN AND THERE IS ACTIVE, UNFINISHED WORK.** The `P1`–`P19` ladder is complete and every
earlier phase is closed except `PH-3` (frozen deliberately — `WBS-20.4` is the email adapter against
a hard constraint; **do not "repair" it**). On 2026-08-18 the operator activated `DW-020`, `DW-029`
and `DW-030`; `DEC-060` created `PH-6` to hold the work.

- **`SL-029` (FR-030 topic conversion) is DONE** — merged as `bcc3d00` (#293) + `b065a29` (#294),
  `AC-113` Met, slice `Implemented`. Do not reopen it.
- **`SL-030` (Confidentiality ABAC, FR-163) is HALF BUILT on `feat/sl-030-confidentiality`** —
  10 commits, pushed, **no PR yet**. §2 is the remaining work.
- **`readiness_check("package")` returns `ready:false` ON PURPOSE.** `acs-met` fails on **exactly
  `AC-114`** and nothing else. That is the documented build-window state: an acceptance criterion
  written before its evidence is a readiness liability by design. ⚠ **Never resolve it by upgrading
  the verdict.** Reconcile the failing id list against your batch — an id you did not expect is a
  real finding.
- **`defects-minor` now fails on `DEF-086`–`DEF-089`.** All four came from the DW-029 sweep and are
  low/medium. Carrying them is legal; silence is not. They are not blockers.
- **`deferred-work-reviewed` is at 22 rows** and `acs-slice-bound` still lists `AC-109`–`AC-112`
  (accepted by `DEC-058 d3`). Both fail deliberately and neither is a task.
- **Six lessons are Approved and PINNED** (`LL-001`…`LL-006`) and bind every session via the
  tool-owned note. `LL-006` is new and is the theme of the last session — read it first.

## §2 — The active work: finish `SL-030`

**What is BUILT and mutation-proven** (do not rebuild any of it):

| Piece | Where |
|---|---|
| `Topic.IsRestricted`, `Restrict`/`Declassify`, `TopicRestrictedEvent`, migration | `Topics.Domain/Topic.cs`, `Migrations/20260818193742_Topics_AddIsRestricted.cs` |
| `IConfidentialResource` contract (a **declared primitive**, ADR-0001/0021) | `Acmp.Shared/Authorization/Abac/AbacResources.cs` |
| `ConfidentialityRequirement` + handler, registered on **`TopicEdit` only** | `Abac/ConfidentialityRequirement.cs`, `AuthorizationRegistration.cs` |
| Read predicate + per-request scope resolver | `Topics.Application/Internal/TopicVisibilityQuery.cs`, `Abstractions/ITopicVisibility.cs` |
| Applied to: `GetBacklog`, `GetTopicDetail` (**404 not 403**), `TopicSearchProvider` (before `.Take`), `TopicReader` ×3, `TopicStreamReader` | those files |
| Classify command + `PUT /api/topics/{id}/confidentiality` | `Features/SetTopicConfidentiality/`, `TopicEndpoints.cs` |
| SPA badge + segmented classify control, EN/AR | `TopicDetail.tsx`, `EditTopic.tsx`, `topics.css` |

**WHAT IS NOT BUILT — the egress redaction, and it is a real leak, not a formality.**
A Restricted topic placed on an agenda **still leaks its title to every member who reads that
meeting.** Data already copied out of the topic is untouched by the predicate:

1. **`AgendaItem.TopicKey` / `TopicTitle`** — frozen into the Meetings schema at agenda-build time
   (`Meetings.Domain/AgendaItem.cs:18-19`). The projection choke point is
   `Meetings.Application/Internal/MeetingMapping.cs`; also check `GetMySession` and the published
   agenda/minutes paths.
2. **`Relationship.SourceTitle` / `TargetTitle`** — frozen into Traceability; read side is
   `GetArtifactRelationships` and `GetImpactGraph`.
3. **Notification bodies** — `Topics.Application/Internal/TopicNotifications.cs`. ⚠ These are
   persisted at **publish** time, not read time, so the builders must take a restriction flag; there
   is nothing to redact later.

⚠⚠ **THE RULE IS ALREADY SETTLED AND MUST NOT BE RELAXED: REDACT AT PROJECTION TIME, NEVER BY
MUTATING A STORED SNAPSHOT.** `INV-005` makes published minutes and issued decisions immutable, and
`AgendaItem` freezes its snapshot by design. Rewriting those rows would break the immutability the
audit design rests on.

⚠ Meetings must not read Topics' tables (ADR-0001). Add a **read port** (Topics implements, Meetings
consumes) returning restriction per key, and **batch it per meeting** — not per agenda item.

**Then:** flip `AC-114` to `Met` with evidence, `readiness_check("slice", "SL-030")`, set the slice
`Implemented`, `work_bind`, `gate_run()`, `export_html()`, PR → green CI → squash-merge.

**Also still open, and NOT part of this slice:** `DW-029`'s acceptance-criterion programme —
108 v1 requirements with no AC. Batched, multi-session, and it needs its own conversation.

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
