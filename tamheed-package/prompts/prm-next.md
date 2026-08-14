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

`main` is green at **`1a95697`**, clean, pushed. Gates **7/7**. Read the live numbers — `gate_run()`
for gates and the audit_evidence split, `readiness_check("package")` for the blocking lists. A
hard-coded tally in a prompt is stale on the first new verdict, and one in this very file already was.

**Production runs `65e45d4` and is UNUSED** — zero topics, one of 26 members has ever signed in.
**Nothing merged in this stream is deployed.**

⚠ **`DEF-065`: the step-5 backfill reaches about ONE production row, not 26.** The 26 are KEYCLOAK
accounts; a `committee_members` row is written only by first login or an app invite, and `DEF-038`
measured the live roster listing 1 of 26. The other ~25 arrive later with zero streams and would be
refused the day step 7 deploys. **A migration cannot see Keycloak** — three options on the row, and
the choice is the operator's, at the step-7 DEPLOY.

## THE MAIN WORK — `ADR-0043`, steps 5–8 of 8

⚠ **Read `ADR-0043`, not `ADR-0042`.** 0042 is **Superseded**: its context claimed Guest is
stream-bounded, which `permission-role-matrix` E.1 contradicts. All seven decision clauses carried
over verbatim.

Stream-scope authorization is **specified, registered in DI, unit-tested, and in NO policy — so it
fails open today.** `DEF-057`. The slice closes that. **The order is load-bearing**; the operator has
already decided every open question (`DEC-043`, `DEC-044`).

**Shipped: 1** seed taxonomy (`9be9415`) · **2** topic picker, server + UI (`06e1e6f`, `61ed33a`) ·
**3** assignment UI (`71283ad`) · **4** invite requires a stream (`af98621`).

### Steps 5 and 6 are DONE — do not redo them

**Step 5** (`0464979`, PR #266) — the backfill migration, idempotent, and it **throws when no wildcard
row exists**, because the ADR-0042 seed skips a pre-existing `all-streams` code and a silent no-op
backfill *is* the lockout. It carries `Membership_MemberStreamsNoIdentity_DEF066` immediately before
it: **`DEF-066` — stream assignment had NEVER worked against a real database**, because
`member_streams.StreamId` shipped as an IDENTITY column. That made steps 3 and 4 non-functional as
shipped. EF cannot scaffold the fix, so the table is rebuilt by hand; the rebuild also **refuses to
carry a row whose StreamId matches no stream** (no FK exists, and a wrong scope is worse than none).

**Step 6** (`ca3bf05`, PR #267) — `DEF-058`, both halves of `DEF-059`, `DEF-060`'s positive scoped
set, and the **AC-034 edit flow** the operator added by `DEC-045`/`SC-010`. `AC-034` is now **Met**
(`AV-161`, live HTTP leg in `d21cb9f` / PR #268).

### Step 7 — wire the requirement ⚠ READ `DEF-068` FIRST

⚠⚠ **`DEF-062`'s blast-radius warning is WRONG, and `DEF-068` measured why.** It predicted the whole
e2e core loop would go red. It will not: **every e2e caller of a `TopicEdit`-gated route is a
Secretary**, who bypasses stream scope. The fixture-streams work `DEF-062` called mandatory is not
needed to keep the suite green — only to evidence `AC-010`. `DEF-062` reasoned from ROLES; the answer
was in CALL SITES.

⚠ **The real landmine is `PermissionMatrixTests`.** It evaluates every policy against
`StubTopic : ITopicScopedResource`, which does **not** implement `IStreamScopedResource` — and
ASP.NET never invokes a two-parameter handler when the resource is not of its type. The moment
`TopicEdit` carries the requirement, **every `TopicEdit` cell flips to Deny, Chairman included**.
**Do not fix that by making the stub implement the interface.** The stub is the messenger: the same
shape refuses any call site passing a non-stream-scoped resource. Decide the general rule.

⚠ **The wildcard is still not read** (`DW-026`). `UserStreamProvider` returns stream CODES, so a
member holding the wildcard the step-5 backfill just assigned would be REFUSED — the opposite of what
the backfill was for. `Stream.IsWildcard` is one column away in the same query, so prefer widening the
existing call over adding a second round-trip. Never match on the code (`ADR-0043` clause 3).

⚠ **`DEF-060`: the scoped set is already positive** (`Member`/`Reviewer`/`Submitter`) — keep it that
way. And `Policies.TopicSubmit` is endpoint-level with no resource, so submitting against an unheld
stream stays unscoped; decide whether that matters rather than assuming.

### Step 8 — evidence `AC-010`

⚠ **The discrimination is reachable through `PrepareTopic` and essentially nowhere else** (`DEF-068`).
`UpdateTopic` pre-Accept skips authorization for the submitter and refuses a non-owner Member on the
CAPABILITY check *before* streams are consulted; post-Accept it uses `TopicTriage`, not `TopicEdit`.
`PrepareTopic` is `TopicEdit` on an ACCEPTED topic, where grant-on-accept makes `CapabilityRequirement`
succeed for an owning Member — leaving stream scope as the only thing that can decide the outcome.

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

## Also open

- **`DEF-067`** — `DecisionPage.test.tsx` fails intermittently under `test:cov` (passes alone, passes
  under plain `vitest run`). Pre-existing: re-run with all source changes STASHED and it still failed.
  Fix by awaiting the settled state, never by a retry or a timeout.

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
