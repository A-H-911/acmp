# Kickoff prompt — the durable one

Paste everything between the two `=====` lines into a fresh session. This file is durably named:
when the work changes, **edit it — do not create a new `prm-*.md`**.

=====

Read `tamheed-package/prompts/README.md` (the operator guide) and `AGENTS.md` before anything else,
then orient:

```
server_info()                      # expect tamheed 3.2.1, schema 4, root = C:\Users\ahammo\Repos\acmp
package_open("tamheed-package")
gate_run()                         # expect 7/7 ready:true
```

**If `package_open` refuses on a `.lock`, do NOT clear it reflexively.** Use both discriminators
(`prompts/README.md` §"One session at a time" encodes them): the named pid must be a LIVE process that
plausibly IS an agent session, **and** it must have started BEFORE the lock's `taken_at`. Either one
failing proves staleness — a process younger than the lock cannot hold it. This has fired on every
plugin reload; once the pid was alive but was `conhost.exe` started a day after the lock, so a naive
"is it alive?" check would have stopped the session dead.

## Where things stand

`main` is green at **`0421b76`**, clean, pushed. Gates **7/7**. **12 open defects.** Six ACs are not
Met: `AC-003`, `AC-006`, `AC-010`, `AC-011`, `AC-041`, `AC-048`.

**Production runs `65e45d4` and is UNUSED** — zero topics, zero streams held by members, one of 26
members has ever signed in. **Nothing merged in this stream is deployed.** `e9b2155` (30-minute idle
sign-out, `AC-004`) is published but NOT deployed — `DEC-044`, deliberately.

⚠ **Do not trust any tally written into a prompt or a note.** Read the live numbers: `gate_run()` for
gates and the audit_evidence split, `readiness_check("package")` for the blocking lists,
`review.html#execution` for both. A hard-coded count is stale on the first new verdict, and one in
this very file already was.

## THE MAIN WORK — `ADR-0043`, steps 5–8 of 8

⚠ **Read `ADR-0043`, not `ADR-0042`.** 0042 is **Superseded**: its context claimed Guest is
stream-bounded, which `permission-role-matrix` E.1 contradicts. All seven decision clauses carried
over verbatim.

Stream-scope authorization is **specified, registered in DI, unit-tested, and in NO policy — so it
fails open today.** `DEF-057`. The slice closes that. **The order is load-bearing**; the operator has
already decided every open question (`DEC-043`, `DEC-044`).

**Shipped: 1** seed taxonomy (`9be9415`) · **2** topic picker, server + UI (`06e1e6f`, `61ed33a`) ·
**3** assignment UI (`71283ad`) · **4** invite requires a stream (`af98621`).

### Step 5 — the backfill migration ⚠ THE IRREVERSIBLE ONE

An **EF data migration** (`DEC-044`) assigning the **wildcard** to every member holding no streams.
Idempotent. This is the step `ADR-0043` marks as the *certain* negative consequence: skip or botch it
and the whole committee is locked out the day step 7 lands.

⚠⚠ **`DEF-062` — my own recorded warning about this was INVERTED, read the row.** The migration is a
**no-op in every fresh environment**, because a migration only touches rows existing when it runs:
`Api.Tests` uses InMemory and never migrates; `Integration.Tests` migrates an empty DB; e2e migrates
fresh then JIT-provisions members *afterwards*, and `ProvisionCurrentUser` assigns no streams. Only
prod/UAT (which already hold 26 rows) are reached. **Consequence: at step 7 every e2e fixture is
stream-bounded with zero streams and will be refused every guarded write — the whole core loop, not
one test.** Give fixtures real streams at step 7 (where `apiHelpers`/`roleSession` provision the
member), and **NOT the wildcard**, which would restore the vacuous-pass problem by another route.

### Step 6 — `DEF-058` + `DEF-059`

`DEF-058`: `Topic.SetScope` has no caller — add `Scope` to `UpdateTopicCommand` and surface it in
triage, so `Platform`/`OrgWide` stop being unreachable enum values. `DeriveScope()` already preserves
an elevated scope; that guard is why elevation is safe to add.

`DEF-059` (operator chose BOTH halves): guard `Topic.AssignStreams` so a live topic can never be
emptied, **plus** `NotEmpty` on `UpdateTopicCommand.Streams` — the guard goes in the **shared aggregate
method**, since patching only the named caller leaves its siblings broken. And make
`StreamScopeHandler` require an explicit `AffectsAllStreams` declaration rather than inferring
"unscoped" from an empty list; that second half is what stops Actions/Risks/ADRs inheriting a
universal grant when they implement `IStreamScopedResource`.

⚠ `AffectsAllStreams` must be a **primitive bool on the port**, never the `TopicScope` enum —
`IStreamScopedResource` lives in `Acmp.Shared.Contracts`, the enum in `Topics.Domain` (`ADR-0001`;
`ADR-0021` is the pattern).

### Step 7 — wire the requirement

Put `StreamScopeRequirement` into the stream-bounded write policies.

⚠ **`DEF-060`: express the scoped set POSITIVELY** — `Member`/`Reviewer`/`Submitter`, per E.1 — never
as "everyone not in the `CommitteeWide` bypass list". Treating a *bypass* list's complement as the
scoped set is what wrongly swept Guest in, and it would refuse FR-159 guest presenters their one write
capability (`DiagramAttach`). E.3 bounds a guest by a **time window**, not by streams. Expressing it
positively removes the whole class, not just this instance.

### Step 8 — evidence `AC-010`

⚠ Against a member assigned to a **DIFFERENT stream than the topic** — never an unassigned one, whose
refusal proves "a member with no streams is denied", a different claim. `topic-scope.spec.ts` is where
it belongs; its header already carries this warning.

## Independent of the slice

- **`DEF-056`** — a refused mutation is NOT audited. Every write 403s at the ASP.NET policy layer
  *before* MediatR, and `AuthorizationBehavior:39` is the only emitter. Build the
  `IAuthorizationMiddlewareResultHandler`; emit **only** on `Forbidden`, never `Challenged`. A
  deliberate `test.fail()` in `role-matrix.spec.ts` goes RED the day it is fixed — delete that line and
  flip `AC-006`.
- **`AC-011`** — turn `KEYCLOAK_ADMIN_ENABLED=true` on in CI's e2e stack (the secret is already
  mounted; `docker-compose.yml` 163/183/338).
- **`AC-041`** — operator OVERRULED my recommendation: **add the Edge project** to
  `playwright.config.ts`, which has only chromium today, so the AC's "Chrome and Edge" has never been
  true. Scoped to browser coverage; property guards still beat pixel baselines.
- **`AC-003`** — a cheap live test.

## Two unmerged branches and a stash

- **`fix/e2e-agenda-publish-race`** (`2e91b9d`) — fixes `DEF-061`'s race by awaiting the presenter
  POST. ⚠ **NOT type-checked or run** (the headless session that wrote it had `tsc` and e2e blocked).
  Verify, then merge.
- **`fix/admin-streams-tab-live`** (`0dff8ec`) — makes Administration → Streams read the seeded
  taxonomy instead of the now-false "No streams configured" empty state. Its author chose a
  **partial-fidelity composition** (2 of the design's 5 columns, because the endpoint sources only
  one) and declared it — that call is worth your review before merge. A `stash@{0}` from this branch
  still exists; check it before dropping.

## Standing traps — each of these has cost real time here

1. **Find the CALLER, not the definition.** Four aggregate capabilities were correct, unit-tested, and
   wired into nothing (`DW-026`). The compiler doesn't care; the unit test calls it directly, which is
   *why* it passes; coverage says it's covered. ⚠ Coverage catches unread **state** but never an
   uncalled **method**.
2. **Read the implementation before calling it a defect — and that applies to REGISTER ROWS too.**
   Rows read as pre-checked. They are not: `DEF-062` and `DEF-061` were both wrong in my own hand.
3. **A measurement that indicts known-good code is measuring itself.** A computed 1px border indicted
   two pre-existing rules; a hover probe reported "0 dimmed" from the wrong selector *and* wrong
   property. Read the CSS/predicate before believing the probe.
4. **A green exit code can come from a build that checked nothing.** A mutation that fails to COMPILE
   runs zero tests and looks exactly like "the test doesn't discriminate". Confirm the mutant built.
5. **The test must fail without the change.** Every guard here is mutation-checked, with the *attribution*
   named — which tests fail, and which correctly stay green.
6. **⚠ NEVER write Arabic via `\uXXXX` + `.encode().decode('unicode_escape')`** — that reads UTF-8 as
   Latin-1 and destroys it, while leaving the English half perfect so nothing looks wrong (`DEF-064`,
   7 values shipped broken to main). Write literal UTF-8. And `check-i18n.mjs` compares **key sets, not
   values**, so "parity OK" is true and meaningless — no gate can see mojibake.
7. **`.cs` files need a UTF-8 BOM and LF**; the Write tool adds neither, and Python round-trips strip
   the BOM. The format gate catches it — don't suppress its output, which is how a failing check reads
   as a passing one.
8. **One tool's negative is not proof of absence** — a grep returned nothing for a pattern that was
   there.

## Definition of done (applies even though this prompt does not restate it)

Unit + integration tests, each guard proven by FORCING its refusal and verified to FAIL without the
change · flip AC verdicts via `audit_record` with evidence, and say plainly when something is ANALYSIS
rather than a measurement · authorization enforced server-side, `AuditEvent`s asserted as ROWS · no
hardcoded strings, EN + AR together, RTL verified in a browser · no secrets, never print a live
credential · `progress_update` + `work_bind`, then `gate_run()` and `export_html()`; **write the
package only from `main` and commit immediately** (`tamheed-package/data` is git-tracked) ·
conventional commits, small and reviewable · branch → PR → green CI → squash-merge · **register every
finding as a Tamheed row AS YOU GO — including findings against your own work, and corrections to
evidence you yourself recorded.**

Report the state and your plan before writing, then proceed.

=====

## Not part of the paste — one open item for the operator

**The tamheed acceptance series (`findings_13`–`findings_16`) is complete except §6.** The
CLAUDE.md Recording-obligations note has been proven to **transfer** into two independent fresh
contexts — both paraphrased it correctly and surfaced the obligation unprompted. What is still
unproven is whether a fresh session **discharges** it (actually writes the rows) rather than listing
it.

Neither instrument I can run settles it: a delegated agent defers package writes to its parent, and a
headless `claude -p` run cannot reach the MCP tools under any permission mode I am willing to use.
**Only an interactive fresh session can close §6** — paste a small real task into a normal Claude Code
window, say nothing about recording, and see whether the `DEF-`/`progress_update`/`work_bind` rows
appear.
