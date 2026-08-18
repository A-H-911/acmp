# Kickoff prompt — the durable one

Paste everything between the two `=====` lines into a fresh session. This file is durably named:
when the work changes, **edit it — do not create a new `prm-*.md`.**

=====

Read `tamheed-package/prompts/README.md` (the operator guide) and `AGENTS.md` before anything else,
then orient:

```
server_info()                      # expect tamheed 4.4.1, root = C:\Users\ahammo\Repos\acmp
package_open("tamheed-package")
gate_run()                         # expect 7/7 ready:true
readiness_check("package")         # expect ready:TRUE — every BLOCKING rule passes
```

⚠ **"Ready" means BLOCKING only.** TWO advisories fail on purpose and neither is a task:
`deferred-work-reviewed` (16 `DW-` rows whose triggers have not fired) and `acs-slice-bound`
(`AC-109`–`AC-112`, accepted by `DEC-058 d3`). `lessons-confirmed` now PASSES — all five lessons are
Approved and pinned.

⚠ **Do not trust any tally written into a prompt, including this one.** Read the live numbers:
`gate_run()`, `readiness_check("package")`, `entity_query("defect", lifecycle_status="Open")`. A
hard-coded count is stale on the first new write, and this file has now carried a stale one **five**
times — the fourth time it also told the reader to expect `ready:false` when readiness had been
`true` for a day. If you find this section wrong again, **fix the file in the same session**.

**If `package_open` refuses on a `.lock`, do NOT clear it reflexively — but do not assume it is real
either.** A plugin reload orphans the lock every time. Both discriminators (`prompts/README.md`): the
named pid must be a LIVE process that plausibly IS an agent session, **and** it must have started
BEFORE the lock's `taken_at`. Either failing proves staleness. In practice the pid simply does not
exist — check that first.

---

## §1 — What is true right now (2026-08-18)

`main` is green, clean and pushed. (`git log --oneline -5` is the authority — no hash is quoted here
on purpose, because the last one written into this file went stale the same day.)

- **THE ENGINEERING QUEUE IS EMPTY.** Everything raised in the 2026-08-17/18 stream has been built,
  merged and recorded. There is no pending work item, no parked branch, and nothing waiting on the
  operator. **If you are here to "continue", the honest first move is to ASK what to work on** —
  do not invent a task from the registers.
- **The ladder is COMPLETE.** `P1`–`P19` shipped; all 28 slices are `Implemented` except `SL-014`
  (P14 Tarseem, `Deferred` by `DEC-028` — operator-reopen only). **There is no active slice**, so
  `slice-kickoff.md` applies to nothing on this list.
- **Every phase is closed except `PH-3`, and that one is closed-by-refusal on purpose.** `PH-5` was
  closed 2026-08-17. ⚠ `PH-3` stays `Approved` because `wbs-done` fails on **`WBS-20`**, open
  deliberately: `WBS-20.4` is the email adapter and *no email in v1* is a hard constraint
  (`DEC-055`). **Do not "repair" PH-3 to make the statuses look uniform** — that is the manufactured
  -status move `DEF-010` records.
- **PRODUCTION IS DEPLOYED AND RECONCILED.** `/readyz` returns 200 Healthy on all four checks;
  `committee_members` went 1 → 27. ⚠ **The adoption clock (`RISK-007`) started 2026-08-17** — the old
  "0 topics, 1 sign-in" statistic measured a platform that could not admit its users.
- **ZERO open defects of any severity.** `defects-closed` and `defects-minor` both pass.
- **Five lessons are Approved and PINNED** (`LL-001`…`LL-005`) and bind every session via the
  tool-owned note in `tamheed-package/CLAUDE.md`. Read them; they are short and each cost real time.
- **The store is Tamheed v4** (server 4.4.1). See §4 for what v4 changed under you.

### What shipped in the last stream, so you do not redo it

**`DEC-057` — `DEF-084`'s eight unreachable aggregate methods were THREE different problems**, which
only became visible by reading all eight in source rather than trusting the row:

1. **Four wired** (PR #289): `Topic::Close`/`Reactivate`/`Reopen` + `CommitteeMember::Reactivate`
   (`FR-160`/`161`/`162` + the long-approved `FR-045`; `AC-109`–`AC-112` Met). ⚠ **The allowlist
   REMOVAL is the deliverable, not the code** — the `DW-026` guard now FAILS if a handler stops
   calling its transition.
2. **Two deferred** as `DW-030` — `FR-030` is Approved and traced, so "deliberately unexposed" would
   have retired an approved requirement silently.
3. **Two are uncallable by construction** and stay allowlisted: both enforcement points evaluate the
   window inside an EF `Where`/`AnyAsync`. `PredicateAgreementTests` guards the duplication.

⚠ **`DEF-085` was the one that mattered**: a disabled member was **permanently locked out** —
`Deactivate()` had two callers incl. the hourly guest sweep, `Reactivate()` had none, re-invite
throws on the duplicate email, and delete is forbidden. `SC-017` widened `IIdentityProvider` with a
fifth write (`EnableUserAsync`), recorded BEFORE the code as `SC-011` was.

**`DEC-058` / `DW-017`** (PR #290): owned-child audit rows carried **empty before/after** —
`Finding`/`Recommendation` were `BaseEntity` and `AuditCapture` only walks
`Entries<AuditableEntity>()`. INV-005 held, so every naive check passed for months.

**`DW-031`** (PR #291) and **`DEC-059` / `DW-009`** (PR #292): both were CHECKS that found real bugs —
see traps A-1c and E-28.

## §2 — What is actually left

**Nothing that is agent work.** Two advisories fail, both deliberately, and neither is a task:

| Advisory | Why it fails, and why that is correct |
|---|---|
| `deferred-work-reviewed` (16 rows) | Every remaining `DW-` has a trigger that has **not fired** — Phase 2/3 features, a disproven CSP rationale, an indefinitely-deferred P14. ⚠ **Do not close one to make the advisory green**; the rule is advisory precisely because a human judges prose triggers, and a register holding legitimate deferred work fails it forever. |
| `acs-slice-bound` (`AC-109`–`AC-112`) | Accepted by `DEC-058 d3`. They verify at package scope; the only loss is visibility in phase/slice exit views, and no phase remains to exit. Creating a slice would need a new phase (the NOT-NULL FK that made `DEC-029` create `PH-4`). |

**The one genuinely large thing on the horizon is `DW-029`** — 162 requirements with no acceptance
criterion. It is not a chore: it is why requirement status measures whether anyone *wrote* an AC
rather than whether something was *built* (§3). It is sized like the original AC-authoring effort and
deserves its own planning conversation, not a tail-end pickup.

## §3 — The two registers that will mislead you

- **Requirement status measures whether anyone WROTE an acceptance criterion, not whether the thing
  was built.** Of 222 requirements, exactly the 60 with an AC are `Implemented` and exactly the 162
  without one are `Approved` — because a requirement advances ONLY via the AC auto-advance trigger.
  Everything downstream inherits that, including `v_backlog` listing all 155 `wbs_items` as open
  (`DEF-012`, closed Won't-fix under `DEC-055`). Remediation is carried as **`DW-029`**.
- **`DEF-012`'s "obvious" fix was UNSOUND and was tried.** *A leaf closes when every requirement it
  names is Implemented* closes `WBS-20.4` — the email adapter — on the strength of the Implemented
  `FR-130`, while *no email in v1* is a hard constraint. **No filter repairs that**, so zero of the
  155 rows were written.

## §4 — What the v4 store changed under you

- `defects`, `deferred_work`, `open_questions`: **`status` → `lifecycle_status`**.
  `stakeholders.name` → `title`.
- **`entity_upsert` requires FULL rows** — a partial update is refused outright.
- ⚠ **SOME COLUMNS THAT LOOK LIKE FREE TEXT ARE FOREIGN KEYS.** Known: `defect.fixed_by`,
  `open_question.resolved_by` (takes the deciding `DEC-` id, not a person), and — found the hard way
  on 2026-08-17 — **`deferred_work.invariant_at_stake`, which references `invariants.id`**. That one
  is the nastiest of the three because the column NAME reads like a description field and because
  `DW-027`/`DW-028` both put their invariant prose in the TITLE, so the register offers no example of
  the column used correctly. Put prose in `custom_attributes`.
- **`WVR-` waivers are operator-only — never author one.** `lifecycle_status` **Review**
  (done-claimed) vs **Implemented** (verified). Typed `progress_update` has a **`correction`** event —
  correct via a new entry, never by editing.
- ⚠ **THE CHECK CONSTRAINTS REJECT PLAUSIBLE VALUES, AND THE ERROR IS THE ONLY DOCUMENTATION.**
  - `audit_record` → `verified_by` is **`human` | `agent` | `ci`**, `verification_method` is
    **`auto-test` | `manual` | `inspection`** (prose goes in `evidence`).
  - `defect.lifecycle_status` is **`Open` | `In-progress` | `Fixed` | `Won't-fix` | `Duplicate`**.
  - `progress_update.event_type` is **`work-done` | `verdict-recorded` | `transition` |
    `forced-override` | `gate-decision` | `escalation` | `correction` | `note`**.
  - A batch that violates one constraint **rolls back entirely** (`applied: 0`) — re-send the WHOLE
    batch corrected, never a patch of the failing row.
- ⚠ Three stock prompts are **customised** and never auto-refreshed: `orient-resume.md`,
  `integrity-check.md`, `slice-review.md`.

## §5 — Traps. Every one of these has cost real time here.

### A — Before you call something broken

1. **Read the implementation first — and that applies to REGISTER ROWS.** Rows read as pre-checked;
   they are not. `DEF-062`, `DEF-061`, `DEF-065`, `DEF-071`, `DEF-041` and `AV-159` were each wrong
   or imprecise in my own hand, corrected only by reading the code underneath. ⚠ `AV-159`'s shape is
   the one to memorise: **"I searched for X and found none" rules out X, never the family.** It
   searched for emitters of `Authorization.Forbidden`, found none, and concluded nothing recorded a
   role-less login — while `Authentication.NoRoleClaim` had been emitting one all along.
   ⚠ Same family, 2026-08-17: `DEF-084` reported eight methods as one finding; reading all eight
   showed **three different root causes** with three different correct dispositions.
1b. ⚠⚠ **AN ABSENCE IS ONLY EVIDENCE IF THE INSTRUMENT IS PROVEN PRESENT — AND THIS ONE COST A WHOLE
   SESSION.** A parked branch handed over a confident blocker: *"both positive tests fail with an
   empty audit table."* Every word was false. The rows were being written the whole time, and the
   failing run's **own log printed them**. The helper read `AuditEvent.Action`, **null on the lean v1
   rows**. Its two `NotContain` **controls passed VACUOUSLY** — a collection of nulls contains no
   string whatever the code does. **Inherit a blocker as a hypothesis, never as data.**
2. **A measurement that indicts known-good code is measuring itself.** The coverage gate scored a
   file after its code had moved out (`DEF-069`).
1c. ⚠⚠ **"IT IS ONLY DISPLAY COPY" IS A HYPOTHESIS, AND IT WAS WRONG.** The topic upload hint read
   *"up to 25 MB"* while `TopicAttachmentOptions` allowed **50 MB**, and a source comment even called
   the 25 the design's display copy — so it was reported to the operator as a cosmetic one-line i18n
   fix with **"no behaviour change"**. It was not: `SubmitTopic.tsx:44` held an **ENFORCED** client cap
   and rejected larger files BEFORE upload, so `AC-049`'s 50 MB default was **unreachable through the
   UI**. Changing only the text would have made the copy promise more than the app accepts — a lie in
   the opposite direction. **Grep for the number before calling a string cosmetic** (`DEC-059`).

3. **The LSP diagnostics panel can be stale** — phantom `CS0103`s while the same build succeeded.
   Check the file, or just build, before believing the tool.

### B — What your tests structurally cannot see

4. **THE PROVIDER YOU TEST ON DECIDES WHAT CAN PASS.** `DEF-066`: assigning a stream had **never**
   worked on a real database — four green suites over a feature that could not work once. Ask: *has
   this ever run against SQL Server?*
5. **JSDOM DOES NOT RENDER.** Component tests, axe and CI cannot see a visual regression. If a change
   is visual, **look at it**: throwaway page importing only the real route's stylesheets, served over
   **http** (`file:` is blocked). ⚠ A screenshot is evidence about **pixels, not elements**.
   (`DW-031` is this trap, open, right now.)
6. **Coverage catches unread state but never an uncalled method** — `DW-026`'s whole subject, now
   fired six times.
7. **A requirement typed to a RESOURCE is invisible wherever that resource type is absent**
   (`DEF-068`).
8. **An endpoint policy over a command that already carries `AllowedRoles` proves NOTHING in a test.**

### C — Proving things

9. **The test must fail without the change.** Mutation-check every guard and name which test fails.
10. **A mutation nothing catches is a decision nobody recorded.**
11. **A HOLLOW PASS IS WORSE THAN AN `indeterminate`.** A rule that cannot fail is not a green light.
    ⚠ The constructive version, 2026-08-17: `defects-closed` was `indeterminate` at PH-5 scope
    (0 of 84 defects carry `found_in`). It was not treated as a pass — the question was answered from
    the SUPERSET instead: package-scope `defects-closed` and `defects-minor` both pass, so zero open
    defects exist anywhere, and a rule that cannot discriminate cannot hide one in an empty set.
12. **A green exit code can come from a run that checked nothing.** Confirm a mutant actually
    COMPILED, and **reconcile test COUNTS**.

### D — Writing to the package

13. **Build payloads from `data/*.jsonl`, never from `entity_query` output** — v4 needs FULL rows and
    the store holds **truncated** ones.
14. ⚠ **A GENERATED payload must be PASTED, not RE-TYPED** (`LL-001`). End any N-row repair with an
    independent re-read that re-derives each value from its source. ⚠ Where a paste is structurally
    impossible — an MCP call whose arguments must be composed — the verifier is MANDATORY: hash a
    pre-image before the write and assert byte-identity after. Done for `DEF-084`/`DEF-085` on
    2026-08-17 (2393 and 2391 chars, both identical).
15. **Omitting `custom_attributes` PRESERVES it; sending it REPLACES the whole blob.**
16. **Run `gate_run()` AFTER writing, not only before.** ⚠ **Creating an acceptance criterion ahead of
    its build turns `G-PROGRESS` RED**: the view (`db/schema.sql:867`) fails any active AC with **no
    verdict at all**, so the gate fires precisely when you do the right thing and write criteria
    before code. Record a **`Pending`** verdict carrying the measured not-built state. It does NOT
    make `acs-met` pass, and must not be "fixed" by upgrading the verdict.
16b. ⚠ **`MEMORY.md` HAS A READ LIMIT AND TRUNCATES SILENTLY.** Past ~17KB everything after the cut
    is dropped when the index loads, and **nothing announces it** — entries at the end are simply
    invisible to every future session. It reached 27KB before being caught. Keep it one line per
    entry with detail in topic files; an over-long index is strictly worse than a short one.

17. ⚠ `progress_entries` is **append-only** — two bad rows could not be edited and the repair was a
    whole-file git rollback.

### E — Environment

18. **`.cs` files need a UTF-8 BOM and LF**; the Write tool adds neither. ⚠ `git status --porcelain`
    **collapses untracked directories**, so a BOM-fixer that reads it will silently skip every new
    file in a new folder — use `-uall`.
19. ⚠ **`gh pr create --body` and `git commit -m` with backticks break under PowerShell** — use
    `--body-file` / `-F`.
20. **Never write Arabic as unicode escapes** (`DEF-064`) — write literal UTF-8. `check-i18n.mjs`
    compares KEY SETS, not values.
21. **Ancestry is the wrong test for "is this branch's work already shipped."** Use the three-dot
    tree diff.
22. **`rm -rf tests/*/TestResults` before trusting a local coverage run** (`DEF-069`; CI is fine).
    ⚠ The coverage gate is **per-file ≥95%**, and the line new handlers most often miss is the
    **validator** — a feature whose test never constructs `XValidator` lands at ~94%.
23. ⚠ **`gh pr checks --watch` AND `gh run watch` BOTH REPORTED SUCCESS ON RUNS THAT HAD NOT
    FINISHED.** Poll the `status` field until it reads `completed`, then read `conclusion`.
    ⚠ GitHub's API also returns intermittent **503**s — a polling loop must distinguish "API failed"
    from "checks settled", or it will read an empty result as success.
23b. ⚠⚠ **`$?` AFTER A PIPE IS THE EXIT CODE OF THE LAST COMMAND IN THE PIPE, NOT YOUR GATE'S.**
    `dotnet format --verify-no-changes | tail -3; echo "EXIT=$?"` printed **EXIT=0** while the real
    exit was **2** — an `IDE0161` failure on an EF-generated migration that CI would have rejected.
    The pipe had reported `tail`'s success as the gate's. Redirect to a file and read `$?` on the
    bare command (`cmd > /tmp/out 2>&1; echo $?`), or use `PIPESTATUS`. This is trap 12 ("a green
    exit code can come from a run that checked nothing") arriving through the SHELL rather than the
    test runner, and it is the same family as trap 23 above — trusting a wrapper's verdict instead
    of the thing it wrapped.
24. ⚠ **NEVER `git checkout -- .` WITH UNCOMMITTED WORK IN THE TREE**, including during mutation
    testing. **Commit first, then mutate against the commit.**
25. ⚠ **`gh pr merge --delete-branch` can MERGE REMOTELY AND STILL FAIL LOCALLY.** On 2026-08-17 it
    squash-merged #289, then aborted its local step with *"Not possible to fast-forward"* because
    local `main` held a commit that origin/main had never seen. The working tree silently reverted to
    pre-feature `main`, which LOOKS like the work was lost. **Check `gh pr view --json state` before
    reacting**; then verify content is present in `origin/main` and `git reset --hard origin/main`.
28. ⚠⚠ **AN UNDEFINED CSS CUSTOM PROPERTY FAILS SILENTLY — AND THE FIX IS USUALLY A CLASS THAT
    ALREADY EXISTS.** `border-radius: var(--radius-2)` shipped square corners against a uniformly
    rounded system, because **`--radius-2` does not exist** (the token is `--control-radius`) and an
    unknown custom property does not warn — it falls back to the initial value. The same hand-rolled
    field also collapsed to **34px** inside a flex dialog for want of `min-inline-size: 0`. Both
    vanished by deleting the bespoke CSS and using `.field-label` / `.textarea`, which already carried
    the radius, the `min-inline-size`, AND focus + invalid states the hand-rolled pair lacked.
    **Grep `styles/` for an existing class before writing a new one**, and grep `tokens.css` for a
    variable before using it (`DW-031`).

26. **A LOG TAIL IS THE WRONG INSTRUMENT FOR A FAILURE WHOSE DISTANCE YOU DO NOT KNOW** (`DEF-074`).
    Dump the api log with no tail at all.
27. ⚠ **NOTHING A LATER SESSION MUST READ MAY LIVE IN THE SCRATCHPAD.** It is session-scoped, so a
    pointer into it is a dangling reference the moment the session ends. Repository or package, never
    scratchpad.

## §6 — Definition of done

Unit + integration tests, each guard proven by FORCING its refusal and verified to FAIL without the
change · flip AC verdicts via `audit_record` with evidence, and say plainly when something is
ANALYSIS rather than a measurement · authorization enforced server-side, `AuditEvent`s asserted as
ROWS · no hardcoded strings, EN + AR together, RTL verified · **if it is visual, look at it** · no
secrets, never print a live credential · `progress_update` + `work_bind`, then `gate_run()` and
`export_html()`; **commit `tamheed-package/data` immediately** (it is git-tracked, and a branch
operation destroys uncommitted package writes exactly like source) · conventional commits, small and
reviewable · branch → PR → green CI → squash-merge · **register every finding as a Tamheed row AS YOU
GO — including findings against your own work, and corrections to evidence you yourself recorded.**

⚠ **Before offering the operator a disposition for a capability, sweep the requirement register for
it first** (`LL-005`). "Record it as deliberately unexposed" silently retires an Approved requirement
when one exists, and the operator cannot see that unless you checked.

Report the state and your plan before writing, then proceed.

=====
