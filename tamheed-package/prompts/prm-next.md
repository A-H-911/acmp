# Kickoff prompt — the durable one

Paste everything between the two `=====` lines into a fresh session. This file is durably named:
when the work changes, **edit it — do not create a new `prm-*.md`**.

=====

Read `tamheed-package/prompts/README.md` (the operator guide) and `AGENTS.md` before anything else,
then orient:

```
server_info()                      # expect tamheed 4.1.0, root = C:\Users\ahammo\Repos\acmp
package_open("tamheed-package")
gate_run()                         # expect 7/7 ready:true  (G-REL is now a real gate, and passes)
readiness_check("package")         # expect ready:FALSE — correct, not a bug (see below)
```

⚠⚠ **THE STORE IS v4 NOW** (migrated `7123a1b`; backup = git `967e75d`, `data-v3-backup/` gitignored).
What changed under you:

- **`defects` and `deferred_work` no longer have `status` — it is `lifecycle_status`.** So is
  `open_questions`. `stakeholders.name` is now `title`.
- **`entity_upsert` requires FULL rows** — a partial update is refused outright (`NOT NULL
  constraint failed … INSERT evaluates NOT NULL before conflict resolution`). ⚠ **And the store
  holds TRUNCATED risk titles** (exactly 200 chars, residual v2.3 damage), so rebuilding a row from
  what `entity_query` just returned **re-commits the truncation and calls it a fix**. Read
  `data/*.jsonl` when a field may be damaged. ⚠ Omitting `custom_attributes` still PRESERVES the
  `v1` blob — verified, not assumed.
- **New obligations** in the tool-owned note (root `CLAUDE.md` now *imports*
  `tamheed-package/CLAUDE.md` rather than restating it): an `OQ-` row for genuine ambiguity,
  `WVR-` waivers that are **operator-only — you never author one**, `lifecycle_status` Review
  (done-claimed) vs Implemented (verified), and **typed `progress_update` with a `correction`
  event** — which is the thing `DEF-072` needed and could not have.
- **`readiness_check("package")` is ready:false and that is the honest state**, not a regression:
  5 unmet ACs and `DEF-065` block a close. `gate_run` 7/7 and readiness ready:false are not in
  conflict — the gates are mechanical, readiness is lifecycle.
- Risk `probability`/`impact` are **NULL** (v3 M/H/L stashed in `custom_attributes.v3_*`); the v4
  scale was never established, so they were left null rather than invented.

**Two liveness sweeps are done** (`ae9291f`, then the 4.2.0 repairs). Read **`findings_18.md`** first,
then `findings_17.md`. Three of findings_17's items are now CLOSED — the risk scale (C3), the
truncated titles (A4) and the dropped milestone statuses (A5) — all recovered from data the
migration had already stashed, none of it judged.

⚠⚠ **Two traps from those sweeps, and both are about trusting your own instruments:**

1. **A generated payload must be PASTED, not RE-TYPED.** Building the upsert from `data/*.jsonl`
   (rather than `entity_query`) is necessary and was done — then I transcribed the generator's
   output by hand and flipped one risk's probability. **The hand is the untrusted transport.** What
   caught it was not care — care missed it — but a verifier that re-read the JSONL afterwards and
   re-derived every value from the stash. **End any N-row repair with that re-read.**
2. **A hollow `pass` is worse than an `indeterminate`.** `risk-liveness` "passed" last session only
   because `probability`/`impact` were null, so no row could satisfy its *high-X* predicate. With
   the scale recovered it correctly **fails**, naming six rows. An `indeterminate` announces itself;
   a rule that cannot discriminate reports green.

**If `package_open` refuses on a `.lock`, do NOT clear it reflexively.** Use both discriminators
(`prompts/README.md` §"One session at a time"): the named pid must be a LIVE process that plausibly
IS an agent session, **and** it must have started BEFORE the lock's `taken_at`. Either one failing
proves staleness — a process younger than the lock cannot hold it.

## Where things stand

`main` is green at **`bede9bf`** plus the commit carrying this rewrite, clean and pushed. Gates
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

### 1. The `DEF-065` reconciliation — ✅ BUILT AND MERGED (#275, `9508eef`). **DO NOT REBUILD IT.**

`ReconcileIdentityAccountsCommand` + `POST /api/members/reconcile` (Administrator only) creates a
`committee_members` row for every identity account holding none **and grants it the wildcard in the
same operation** — `DEF-071`'s missing clause, and only for rows that run creates. `SC-011` records
the port widening: `IIdentityProvider` gained `ListUsersAsync`, its first READ. `DEF-071` is **Fixed**.

⚠⚠ **`DEF-065` AND `DEF-038` ARE STILL OPEN ON PURPOSE, AND THE DEPLOY IS STILL BLOCKED.** The code
existing is not the fix; the command **running against production** is. Closing them on merge would
repeat this stream's own recorded trap — *correct, proven and evidenced is not the same as
production being safe to deploy into*. **`DEF-065` is the deploy-blocker tracker.** Its row carries
the ordered discharge steps; the short version:

1. `KEYCLOAK_ADMIN_ENABLED=true` **and** a real `KEYCLOAK_ADMIN_CLIENT_SECRET` — two variables, not
   one (`DEC-047` d3), and `09-put-env.sh` refuses a placeholder.
2. Deploy. **No Keycloak grant change is needed** — see below.
3. `POST /api/members/reconcile` as an Administrator **immediately** after, and **read the returned
   partition** (`identityAccounts = created + alreadyProvisioned + skippedDisabled +
   skippedNoCommitteeRole + skippedDuplicateEmail`) rather than assuming it ran.

⚠ **The Administrator token has no CLI path** — the service account is not a committee member, so its
token cannot satisfy `Policies.AdminUsers`. Sign in to the SPA as an Administrator and take the
bearer token from devtools.

⚠ **The residual, deliberate:** anyone who signs in *between* steps 2 and 3 gets a JIT row with no
streams that the command will not touch, and falls back to `ADR-0043` clause (2). Prod has ONE
sign-in in its whole history, so the window is minutes wide. `Reconcile_leaves_a_pre_existing_member_holding_NO_streams_alone`
pins that as intended — **do not "fix" it** to wildcard any zero-stream member: an administrator can
clear streams deliberately and `member_streams` has no provenance column to tell the two apart.

⚠ **`ListUsersAsync` has NO live coverage** — e2e runs with `KEYCLOAK_ADMIN_ENABLED=false`, so the
adapter is proven against a stub transport and one hand-run probe, never by CI. **`AC-011` is what
would change that**, and it is the same gap that let `DEF-066` survive two whole steps.

### 2. `DEF-041` — voting eligibility — ✅ FIXED AND MERGED (#276, `8f200f1`)

Chairman or Secretary, Administrator excluded under SoD-5. `SetVotingEligibilityCommand` +
`PUT /api/members/{publicId}/voting-eligibility` + an operable button in the directory row + a
`Membership.VotingEligibilityChanged` audit row + a refusal for a non-Active member (`AC-058`).

⚠ Two corrections worth carrying. The row's claim that the toggle was "absent from the accessibility
tree entirely" was **wrong** — it rendered as a `span` with `role="switch"` and `aria-disabled`, i.e.
a *disabled switch*: inoperable, but present. And **the design contradicted the reasonable guess
about placement**: `UsersMembership.tsx`'s own header says editing lands in the user detail (which is
how `ADR-0042` step 3 placed stream assignment), but the `.dc.html` draws the toggle as an operable
button **in the directory row**, and defines a `voteEligible` label in the user-detail strings that
nothing renders. Following the code comment would have been an `INV-014` deviation reached by
careful reasoning from a stale comment.

⚠⚠ **AND THE SUITE COULD NOT SEE THE REGRESSION IT SHIPPED** (`#277`, `e31f7ac`). Turning the `span`
into a `button` left `.adm-switch`'s hard-coded `cursor: not-allowed` in place — telling *exactly the
two roles allowed to use the control* that it is forbidden. Component tests, axe and CI all run in
**JSDOM, which does not render**. **If a change is visual, look at it**: a throwaway page importing
only the real route's stylesheets, served over http (`file:` is blocked), measured in the browser.
⚠ The full-page screenshot then showed a stray shape that the DOM proved absent — **a screenshot is
evidence about pixels, not elements; when they disagree, measure.**

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

00. **⚠ RUN `gate_run()` AFTER WRITING TO THE PACKAGE, NOT ONLY BEFORE** (`DEF-072`, mine). Quoting a
   `.dc.html`'s template syntax verbatim into a progress entry trips `G-COMPLETE` — its
   unfinished-work screen includes an empty-mustache pattern, so a faithfully quoted design fragment
   and an unfinished document are the same thing to it. **Code-span every quoted fragment**; the
   screen strips code spans first, and that escape is documented in the server source. ⚠ The bite:
   `progress_entries` is **append-only**, so two of the three failing rows could not be edited — the
   repair was `package_close` → a whole-file `git checkout` of `progress_entries.jsonl` → `package_open`
   (the store is a fresh in-memory SQLite rebuilt from JSONL) → re-append. Had the session closed on
   the merge, `main` would carry a red critical gate with immutable failing rows. ⚠ And the first
   version of `DEF-072`'s own title spelled out the marker words it described and was caught by the
   same screen: **describing a pattern counts as writing it.**

0. **⚠ MEASURE THE PERMISSION BEFORE YOU DESIGN AROUND IT.** Scoping the reconciliation, the tempting
   shape was to read roles by listing users per role (`GET /roles/{name}/users`) — six calls instead
   of one per user. A throwaway Keycloak 26.0, granted **exactly** `admin-client.env`'s
   `{manage-users}` and nothing else, answered in ninety seconds: `GET /users` → **200**,
   `GET /users/{id}/role-mappings/realm` → **200**, `GET /roles/{name}/users` → **403**. The clever
   shape is the one the minimal grant refuses, and it would have failed **in production only** —
   `DEF-066`'s class exactly. Standing up a throwaway realm is cheap; assuming a grant is not.
   `probe-keycloak-grant.mjs` call 9 keeps that claim re-verifiable rather than asserted in a comment.

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
9b. **An endpoint policy on a command that already carries `AllowedRoles` proves NOTHING in a test.**
   Removing `.RequireAuthorization(Policies.AdminUsers)` from the new reconcile endpoint left all
   nine API tests **green** — `AuthorizationBehavior` enforces the same matrix. What such a test
   proves is the RULE, not the policy. That is `DEF-056` seen from the other side, and it means every
   per-endpoint policy added today ships a **403 nobody audits** until `DEF-056` lands.
9c. **A mutation nothing catches is a decision nobody recorded.** The reconciliation's "only rows it
   creates" rule survived four mutants, but the *plausible* wrong version — wildcard any member
   holding zero streams — was caught by nothing, because no test carried a pre-existing zero-stream
   member. Ask of every deliberate asymmetry: **which test fails if someone later "fixes" it?**
9d. **⚠ THE LSP DIAGNOSTICS PANEL CAN BE STALE.** Mid-session it reported a duplicate migration file
   and a missing test helper — neither existed on disk, `git status` was clean, and the helper was at
   `AcmpWebApplicationFactory.cs:174`. Same shape as trap 5: check the file before believing the tool.
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

**The deploy is gated on one thing, and it is now an OPERATOR ACTION rather than unwritten code.**
`DEF-071` is Fixed and the reconciliation is merged (#275) with its guards mutation-proven and a leg
on real SQL Server. What remains is running it: two environment variables, a deploy, and one
authenticated POST — the three steps on the `DEF-065` row. Until that happens `DEF-065` and `DEF-038`
stay Open and the deploy stays blocked, which is the honest state rather than a bookkeeping one.

**No UI was built for it, deliberately.** `ACMP Administration.dc.html` gives that screen exactly one
primary action ("Invite user") and no sync control, and this is a one-time pre-deploy remediation —
a permanent button would be an `INV-014` deviation on a screen that HAS a reference, for a need that
does not recur (new members arrive through the invite flow, which already requires a stream). If you
would rather have the button, that is a scope call and needs a `DEC-`.

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
