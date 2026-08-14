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
(`prompts/README.md` §"One session at a time"): the named pid must be a LIVE process that plausibly
IS an agent session, **and** it must have started BEFORE the lock's `taken_at`. Either one failing
proves staleness — a process younger than the lock cannot hold it.

## Where things stand

`main` is green at **`618c568`** plus the commit carrying this rewrite, clean and pushed. Gates
**7/7**. (`git log --oneline -3` is the authority; a sha written into a prompt is stale by one commit
the moment the prompt itself is committed.)

⚠ **Do not trust any tally written into a prompt, including this one.** Read the live numbers:
`gate_run()` for gates and the audit_evidence split, `entity_query("defect", status="Open")` for the
register, `readiness_check("package")` for blocking lists. A hard-coded count is stale on the first
new verdict, and one in this very file already was.

**`ADR-0043` (stream scope) is COMPLETE — all 8 steps. Do not redo it.** `DEF-057` is closed: the
control is wired, runs, and is evidenced. `AC-010` and `AC-034` are Met. The eight commits are
`9be9415` · `06e1e6f`/`61ed33a` · `71283ad` · `af98621` · `0464979` · `ca3bf05` · `1f0cbcd` ·
`d94f154`.

**Production runs `65e45d4` and is UNUSED** — zero topics, one of 26 members has ever signed in.
**Nothing in this stream is deployed.**

## THE MAIN WORK — the operator's queue (`DEC-046`, `DEC-047`)

Every item below was decided by the operator on 2026-08-14. Read `DEC-046` and `DEC-047` before
starting any of them; the rejected options and their reasons are on those rows.

### 1. The `DEF-065` reconciliation — the ONLY thing blocking a deploy

~25 Keycloak accounts have never signed in, so they have no `committee_members` row. The step-5
backfill cannot reach a row that does not exist, and once stream scope deploys each of them is
refused every guarded write from their first login. `DEC-046` chose to **reconcile Keycloak accounts
into `committee_members`** rather than rely on `ADR-0043` clause (2)'s roster backstop.

⚠⚠ **READ `DEF-071` FIRST — the decision is right and its mechanism is one clause short.**
`Program.cs:133` applies migrations **at boot**, before the first request. Reconciliation is an
application feature, so it runs on a host that is already up: the deploy carrying it runs the
backfill FIRST, against the same ~1 row. Reconciling afterwards creates ~25 rows holding **zero
streams** — `DEF-065`'s exact outcome by another route.

So the command **must assign the wildcard to the rows it creates**, doing what the backfill would
have done had they existed — and **only** to those. An administrator may have deliberately narrowed
someone to one stream, and widening that on a reconciliation run would silently hand them universal
write access. The step-5 backfill already encodes this asymmetry with `NOT EXISTS`; do the same.

⚠ **`IIdentityProvider` has NO read operation.** Its four methods are all writes, and its own comment
says the narrowness is deliberate — *"no general-purpose call-Keycloak escape hatch, so the blast
radius of the service-account credential is bounded by this interface"*. Listing realm users is a
FIFTH method on an `ADR-0038`-governed port. A bounded read is defensible (a read cannot mutate the
identity provider) but it is a real widening of a surface narrowed on purpose — record it, don't slip
it in.

This also answers **`DEF-038`** by a route that row never offered: reconciling creates the rows, so
the roster shows them as ordinary members and no parallel Keycloak listing is needed.

### 2. `DEF-041` — voting eligibility

`DEC-046`: **Chairman or Secretary** may change it — *not* Administrator, which would cross SoD-5.

⚠ The toggle on `/admin/users` renders but is **absent from the accessibility tree entirely** — it is
not a disabled control, it is not a control. So this is a real control + a capability gate + an audit
row, not enabling what is already drawn. `CommitteeMember.SetVotingEligibility` exists in the domain.

### 3. `DEF-039` — the System Health object-store tile

`DEC-047`: **bind it to what the environment actually runs** — MinIO on-prem, S3 on cloud
(`ADR-0035`). Not omitted on cloud, not merely relabelled. ⚠ This needs an environment-aware **probe**,
not a config flag: the check itself differs between MinIO's `/minio/health/live` and S3.

## Also open, independent of that queue

- **`DEF-056`** — a refused mutation is NOT audited. Every write 403s at the ASP.NET policy layer
  *before* MediatR, and `AuthorizationBehavior:39` is the only emitter. Build the
  `IAuthorizationMiddlewareResultHandler`; emit **only** on `Forbidden`, never `Challenged`. A
  deliberate `test.fail()` in `role-matrix.spec.ts` goes RED the day it is fixed — delete that line
  and flip **`AC-006`**.
- **`AC-011`** — turn `KEYCLOAK_ADMIN_ENABLED=true` on in CI's e2e stack. The secret is already
  mounted in `docker-compose.yml` (an earlier prompt cited lines 163/183/338 — **re-locate it rather
  than trusting those numbers**, they were never re-verified and line numbers drift). ⚠ This is also
  what would give the `ADR-0038` invite path its first e2e coverage: it has none today, which is how
  `DEF-066` survived two whole steps of a slice built on top of it.
- **`AC-041`** — operator OVERRULED my recommendation: **add the Edge project** to
  `playwright.config.ts`, which has only chromium today, so the AC's "Chrome and Edge" has never been
  true. Roughly doubles e2e runtime.
- **`AC-003`** — a cheap live test. **`AC-048`** — also unmet; read it before assuming scope.
- **`DEF-067`** — `DecisionPage.test.tsx` fails intermittently under `test:cov` only (passes alone,
  passes under plain `vitest run`). Pre-existing — measured by re-running with all source changes
  STASHED. Fix by awaiting the settled state, never by a retry or a timeout.
- **`DEF-012`** — `v_backlog` residue; unevidenced WBS items stay Approved by design.

## Standing traps — every one of these has cost real time here

1. **Read the implementation before calling it a defect — and that applies to REGISTER ROWS.** Rows
   read as pre-checked; they are not. `DEF-062`, `DEF-061`, `DEF-065` and `DEF-071` were each wrong
   or incomplete in my own hand, and each was corrected only by reading the code underneath.
2. **⚠ THE PROVIDER YOU TEST ON DECIDES WHAT CAN PASS.** `DEF-066`: assigning a stream to a member had
   **never worked against a real database** — `member_streams.StreamId` was an IDENTITY column — and
   two shipped steps of the same slice were built on it. `Acmp.Api.Tests` runs InMemory (no identity
   columns, no filtered indexes, no FK behaviour), e2e never exercised the path, domain tests use no
   database. **Four green suites over a feature that could not work once.** Before trusting an EF
   write path, ask: *has this ever run against SQL Server?* — and grep the integration suite for a
   write to that TABLE, not just for tests of the handler.
3. **A requirement typed to a RESOURCE is invisible wherever that resource type is absent** —
   endpoint-level evaluation (no resource), a call site passing a different aggregate, and a test
   stub. `DEF-068`: adding `StreamScopeRequirement` to a policy made it refuse the **Chairman**. It
   fails CLOSED, which is safe but is a 403 no message explains.
   `AuthorizationRegistration.StreamScoped` carries the rule for adding a policy to that set.
4. **A migration's blast radius is ROWS EXISTING AT MIGRATION TIME.** In any environment built fresh
   that set is EMPTY; in production it is far smaller than the ACCOUNT set. Reasoning about
   environments instead of rows produced `DEF-062`, `DEF-065` and `DEF-071`.
5. **A measurement that indicts known-good code is measuring itself.** The coverage gate kept
   reporting 50% on a file after its code had been MOVED OUT (`DEF-069`, now fixed: newest report per
   project). ⚠ And my first fix for it was itself wrong — a hand-written `/[\/]/` separator class
   matches forward slashes only, while node's `join()` produces BACKSLASHES on Windows, so every key
   collapsed and the gate reported a FALSE 84.70%. Its own log line is what caught it. **Prefer
   `dirname` over a hand-written separator.**
6. **Mojibake is now GUARDED but not extinct.** `check-i18n.mjs` fails on the signature in a VALUE
   (`DEF-070`). ⚠ Every branch cut before `4c1b356` still carries the broken `ar.json`, so an old
   branch merged today will now FAIL the gate loudly instead of shipping silently — that is the guard
   working. Resolve by keeping **main's** Arabic and RETYPING new keys as literal UTF-8; copying from
   the conflict's branch half propagates the corruption even in a careful resolution.
7. **A green exit code can come from a build or a run that checked nothing.** Confirm a mutant
   actually COMPILED, and **reconcile test COUNTS**: step 8's e2e reported 69 passed against a
   previous 66, which is 3 for 2 added tests until you notice the earlier run also had **1 flaky**.
8. **The test must fail without the change.** Every guard in this slice is mutation-checked with the
   attribution named — which test fails, and which correctly stays green.
9. **Find the CALLER, not the definition.** Coverage catches unread state but never an uncalled
   method. `PUT /api/topics/{id}` had NO SPA caller at all until `ca3bf05`.
10. **`.cs` files need a UTF-8 BOM and LF**; the Write tool adds neither, `dotnet ef` rewrites the
    model snapshot as CRLF, and the format gate catches both. ⚠ `gh pr create --body` and
    `git commit -m` with backticks break under PowerShell — always `--body-file` / `-F <file>`.

## Definition of done (applies even though this prompt does not restate it)

Unit + integration tests, each guard proven by FORCING its refusal and verified to FAIL without the
change · flip AC verdicts via `audit_record` with evidence, and say plainly when something is
ANALYSIS rather than a measurement · authorization enforced server-side, `AuditEvent`s asserted as
ROWS · no hardcoded strings, EN + AR together, RTL verified · no secrets, never print a live
credential · `progress_update` + `work_bind`, then `gate_run()` and `export_html()`; **write the
package only from `main` and commit immediately** (`tamheed-package/data` is git-tracked) ·
conventional commits, small and reviewable · branch → PR → green CI → squash-merge · **register every
finding as a Tamheed row AS YOU GO — including findings against your own work, and corrections to
evidence you yourself recorded.**

Report the state and your plan before writing, then proceed.

=====

## Not part of the paste — notes for the operator

**The deploy is gated on one thing.** `ADR-0043` is complete and proven, but *stream scope being
correct, running and evidenced is not the same as production being safe to deploy into*. `DEF-065`
plus `DEF-071` are that gap. Nothing else in the queue blocks a deploy.

**The tamheed acceptance series (`findings_13`–`findings_16`) is still open on §6 only.** The
CLAUDE.md Recording-obligations note has been proven to TRANSFER into fresh contexts; what is
unproven is whether a fresh session DISCHARGES it — actually writes the rows — rather than listing
it. ⚠ **A session started from THIS prompt cannot close §6**, because the prompt instructs recording
and so contaminates the experiment. To close it, paste a small real task into a normal Claude Code
window, say nothing about recording, and see whether the `DEF-`/`progress_update`/`work_bind` rows
appear.

**Stale branches.** Several pre-date `4c1b356` and carry the broken `ar.json`
(`chore/design-update-round2`, `chore/docs-v8-local-design`, `feat/budget-notification-observer`,
`feat/p13-webex-integration`, `scaffold/ph0-p1-foundation`, others). They are now guarded rather than
dangerous — merging one fails `check-i18n` loudly. Rebasing or deleting them would remove the hazard
at the source.
