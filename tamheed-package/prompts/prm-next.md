# Kickoff prompt — the durable one

Paste everything between the two `=====` lines into a fresh session. This file is durably named:
when the work changes, **edit it — do not create a new `prm-*.md`**.

=====

Read `tamheed-package/prompts/README.md` (the operator guide) and `AGENTS.md` before anything else,
then orient:

```
server_info()                      # expect tamheed 4.2.1, root = C:\Users\ahammo\Repos\acmp
package_open("tamheed-package")
gate_run()                         # expect 7/7 ready:true
readiness_check("package")         # expect ready:FALSE — correct, not a bug (see §2)
```

**If `package_open` refuses on a `.lock`, do NOT clear it reflexively — but do not assume it is real
either.** A plugin reload orphans the lock every time, so this is common. Both discriminators
(`prompts/README.md`): the named pid must be a LIVE process that plausibly IS an agent session,
**and** it must have started BEFORE the lock's `taken_at`. Either failing proves staleness. In
practice the pid simply does not exist — check that first, and other live processes are irrelevant
because a lock is held by *the pid it names*.

⚠ **Do not trust any tally written into a prompt, including this one.** Read the live numbers:
`gate_run()`, `readiness_check("package")`, `entity_query("defect", status="Open")`. A hard-coded
count is stale on the first new write, and one in this very file already was.

---

## §1 — What is true right now

`main` is green at **`f51fb0e`** plus the commit carrying this rewrite, clean and pushed.
(`git log --oneline -3` is the authority.)

- **The store is Tamheed v4** (server 4.2.1). See §5 for what that changed under you.
- **`ADR-0043` stream scope is COMPLETE**, and so is everything the `DEC-046`/`047` queue asked for
  except `DEF-039`. Do not redo any of it.
- **Production runs `65e45d4` and is UNUSED** — zero topics, one of 26 members has ever signed in.
  **Nothing in this stream is deployed.**
- **9 open defects**: `DEF-012`, `DEF-038`, `DEF-039`, `DEF-056`, `DEF-065`, `DEF-067`, and three
  opened 2026-08-15 — `DEF-073` (fixed, awaiting merge), `DEF-074` (fixed, awaiting merge),
  `DEF-075` (**START HERE**, see §3).
- **5 unmet ACs**: `AC-003`, `AC-006`, `AC-011`, `AC-041`, `AC-048`.
- `DW-026` is **Activated** (approved to build), `DW-028` is new and Open.

### ⭐ Where the 2026-08-15 session stopped, and why

**`AC-011` is BUILT and its PR is RED on one spec — deliberately left that way.** PR **#278**
(`feat/ac011-keycloak-admin-e2e`) turns `KEYCLOAK_ADMIN_ENABLED` on in CI (`DEC-042`) and adds the
live guest-presenter leg. Everything else in CI is green; `e2e` fails in that spec's `beforeAll` on
**`DEF-075`**, a real product defect the leg exposed. **Do not "fix" the spec to make it pass.**

- ✅ **`DEF-075` IS FIXED AND MERGED** (`3a27fef`, PR #279, ten checks green). ⚠ My analysis on that
  row was **wrong and is corrected there**: a unique violation aborts the STATEMENT, not the
  transaction, and SQL Server blocks the loser until the winner COMMITS — so the fix is a small local
  recovery, not a retry above `TransactionBehavior`.
- ⭐ **`DEF-076` IS NOW THE BLOCKER, and it is the best thing this leg produced.** With the guest
  finally able to sign in, `GET /api/session/me` answered **403 to the one principal
  `GetMySessionQuery` explicitly allows**. `MemberInvitation.InviteAsync` calls `CreateUserAsync` and
  nothing else — **it never assigns a Keycloak realm role** — so an invited guest exists in Keycloak
  as nobody while `committee_members` says Guest with a timed window. FR-159's guest presenter has
  never been able to use the surface built for them, and only a live login could show it.
  ⚠ **It needs an operator decision plus an `SC-` row, not a line of code**: assigning the role for
  every invite would change FR-156, whose inert-until-assigned design is what `AssignRoles` exists
  for. Option (a) on the row — assign on the GUEST path only — is the narrow reading of `ADR-0040`.
  ⚠ It also flipped `role-matrix.spec.ts`'s deliberate `test.fail()` for `AC-006` to
  **"Expected to fail, but passed"** — a role-less guest's refusals DO reach `AuthorizationBehavior`.
  That marker is meant to flip when `DEF-056` lands; do not read this run as `DEF-056` being done.
- `DEC-051` settled `AC-048` (supersede, narrow to the mechanism) and `AC-041` (property-level
  detectors + the Edge project, **not** pixel baselines). ⚠ `AC-048` also needs slice binding, so do
  the narrowing and the binding in **ONE** supersession — otherwise its successor id is minted twice.
- ⚠ `AC-003` needs **its own emitter**: `AV-159` shows its 403 is thrown in the HANDLER, so the
  `IAuthorizationMiddlewareResultHandler` that `DEF-056` will register **cannot** catch it.

## §2 — Why `readiness_check` says `ready:false`, and why that is right

`gate_run` 7/7 and readiness `false` are not in conflict: the gates are **mechanical**, readiness is
**lifecycle**. It is blocked on `acs-met` (the five above), `defects-closed` (`DEF-065`) and
`risks-discharged`. That is the honest state of a package whose work is not finished — do not "fix"
it by closing rows.

⚠ **`risks-discharged` is blocking AND cannot discriminate** (0 of 23 rows have `discharged_by`), so
it lists every open risk by construction. `DEC-050` d2 decided a full traceability pass. **Read that
row first — it is a risk REVIEW, not data entry.**

## §3 — ⭐ THE QUEUE (`DEC-049`, `DEC-050`). Start here.

All operator-decided. **Read the `DEC-` row before starting an item**; the rejected options and their
reasons are on it. Do not re-litigate.

### Build track — in this order, and the order is reasoned

1. ~~**`AC-011`** — `KEYCLOAK_ADMIN_ENABLED=true` in CI's e2e stack.~~ **DONE, in PR #278, and it
   did exactly what `DEC-049` said it would: it broke something.** The flag is on at job level in
   `e2e.yml` (the narrower option; verified by rendering `docker compose config`, not assumed), the
   live guest leg is written, and turning the path on immediately exposed **two** defects nothing
   else could see — `DEF-073` (a guest presenter could read EVERY meeting; fixed in the same PR, all
   five guards mutation-proven) and **`DEF-075`** (JIT provisioning check-then-insert race; **now the
   first item — see §1**). ⚠ The verdict is **NOT** flipped: it flips on a green run, not a merge.
2. **`AC-041`** — add the Edge project to `playwright.config.ts` (chromium-only today, so the AC's
   "Chrome and Edge" has never been true), **and** the property-level detectors `DEC-051` chose over
   pixel baselines — `e2e/vr-sweep.spec.ts` is capture-only with **no assertion of any kind**, so the
   AC's verb "detected" is currently performed by a human opening PNGs. ⚠ Adding the Edge project is
   not one line: `e2e.yml` installs **chromium only**, so without `msedge` added there every Edge
   test dies at launch. · **`AC-003`** — cheap, but needs its own emitter (see §1). · **`AC-048`** —
   scope now KNOWN: **not a build item at all**, supersede it (`DEC-051`), folded into the binding
   pass.
3. **`DEF-039`** — the System Health object-store tile. `DEC-047`: bind it to what the environment
   actually runs (MinIO on-prem, S3 on cloud). ⚠ Needs an environment-aware **probe**, not a config
   flag — the check itself differs between MinIO's `/minio/health/live` and S3.
4. **`DEF-056`** — a refused mutation is NOT audited. Every write 403s at the ASP.NET policy layer
   *before* MediatR, and `AuthorizationBehavior:39` is the only emitter. Build the
   `IAuthorizationMiddlewareResultHandler`; emit **only** on `Forbidden`, never `Challenged`. Flips
   **`AC-006`**. ⚠ A deliberate `test.fail()` in `role-matrix.spec.ts` goes RED the day it lands —
   delete that line.
5. **`DW-026`** — the architecture test (a public aggregate method with no production caller).
   **LAST, deliberately:** its allowlist must be seeded with the gaps that exist *when it is
   written*, and every build above may add or remove uncalled methods. **Start with the narrow
   policy-coverage check** — every `IAuthorizationRequirement` appears in at least one registered
   policy. A handful of lines, and it guards the only fails-open case.

Plus **`DEF-067`** (⚠ fix by awaiting the settled state — **never** a timeout or a retry, both hide a
real regression the day one occurs) and **`DEF-012`**.

### Package track — parallel, no CI

- **Full AC binding, all 20** (`DEC-050` d1... see `acs-slice-bound`). ⚠ An Approved AC's content is
  **immutable**, so any needing a different binding must be **superseded** — which mints new ids and
  moves verdict history onto rows that did not earn it. Row by row; supersede only where the binding
  genuinely changes scope.
- **`risks-discharged`, all 23** (`DEC-050` d2). ⚠ Several PH-0 risks need a judgement about whether
  they are still live at all — `RISK-005`'s subject was deferred indefinitely by `DEC-028`. Where
  nothing genuinely discharges a risk, **retire or accept it explicitly**; pointing it at a
  convenient AC to clear the rule is the manufactured-status failure recorded as `DEF-010`.

## §4 — The deploy: the only thing blocking production, and it is OPERATOR ACTION

`DEF-065` and `DEF-038` stay Open on purpose. **The code existing is not the fix; the command
RUNNING is.** Everything needed is built and merged (#275) and proven, but *correct, proven and
evidenced is not the same as production being safe to deploy into.*

Follow **`deploy/RECONCILE-RUNBOOK.md`**. Three steps: two env vars (`DEC-047` d3 — enabling the
feature is two variables, not one) → deploy → one authenticated `POST /api/members/reconcile`
**immediately after**. Then **`node scripts/verify-reconcile.mjs`**, which re-reads the database
rather than trusting the response and reports **INCOMPLETE** rather than "verified" if you did not
give it a connection string.

⚠ The Administrator token has **no CLI path** — the service account is not a committee member, so it
can never satisfy `Policies.AdminUsers`. Take it from a signed-in admin's devtools.
⚠ Anyone who signs in *between* the deploy and the run gets a zero-stream JIT row the command will
not touch (it is no longer a row that run creates) and falls back to `ADR-0043` clause (2).

## §5 — What the v4 store changed under you

- `defects`, `deferred_work`, `open_questions`: **`status` → `lifecycle_status`**.
  `stakeholders.name` → `title`.
- **`entity_upsert` requires FULL rows** — a partial update is refused outright.
- **Some columns that look like free text are FOREIGN KEYS** — `defect.fixed_by`,
  `open_question.resolved_by`. `resolved_by` takes the **deciding row's id** (a `DEC-`), not a
  person's name.
- New: `OQ-` rows for genuine ambiguity, **`WVR-` waivers that are operator-only — never author
  one**, `lifecycle_status` **Review** (done-claimed) vs **Implemented** (verified), and typed
  `progress_update` with a **`correction` event** — correct via a new entry, never by editing.
- Root `CLAUDE.md` now **imports** `tamheed-package/CLAUDE.md` (tool-owned, auto-refreshed) instead
  of restating it.
- ⚠ Three stock prompts are **customised** and therefore **never auto-refreshed**:
  `orient-resume.md`, `integrity-check.md`, `slice-review.md`. `handoff_emit` now reports
  `stock_last_changed` per file — if it names a release later than your customisation, hand-merge
  from `prompts/stock-history.json`.

## §6 — Traps. Every one of these has cost real time here.

### A — Before you call something broken

1. **Read the implementation first — and that applies to REGISTER ROWS.** Rows read as pre-checked;
   they are not. `DEF-062`, `DEF-061`, `DEF-065`, `DEF-071` and `DEF-041` were each wrong or
   imprecise in my own hand, corrected only by reading the code underneath.
2. **A measurement that indicts known-good code is measuring itself.** The coverage gate scored a
   file after its code had moved out (`DEF-069`).
3. **The LSP diagnostics panel can be stale** — it reported a duplicate migration and a missing test
   helper; neither existed. Check the file before believing the tool.

### B — What your tests structurally cannot see

4. **THE PROVIDER YOU TEST ON DECIDES WHAT CAN PASS.** `DEF-066`: assigning a stream had **never**
   worked on a real database — four green suites over a feature that could not work once. Ask: *has
   this ever run against SQL Server?* and grep the integration suite for a write to that TABLE.
5. **JSDOM DOES NOT RENDER.** Component tests, axe and CI cannot see a visual regression — #276
   shipped a forbidden cursor on the control's own users. If a change is visual, **look at it**:
   throwaway page importing only the real route's stylesheets, served over **http** (`file:` is
   blocked), measured in-browser. ⚠ And a screenshot is evidence about **pixels, not elements** —
   when they disagree, measure the DOM.
6. **Coverage catches unread state but never an uncalled method** — that is `DW-026`'s whole subject,
   and it has now fired five times.
7. **A requirement typed to a RESOURCE is invisible wherever that resource type is absent**
   (`DEF-068`) — endpoint-level evaluation, a different aggregate, a test stub.
8. **An endpoint policy over a command that already carries `AllowedRoles` proves NOTHING in a
   test** — deleting it left all nine API tests green. It also means every per-endpoint policy ships
   a **403 nobody audits** until `DEF-056` lands.

### C — Proving things

9. **The test must fail without the change.** Mutation-check every guard and name which test fails.
10. **A mutation nothing catches is a decision nobody recorded.** Ask of every deliberate asymmetry:
    *which test fails if someone later "fixes" it?*
11. **A HOLLOW PASS IS WORSE THAN AN `indeterminate`.** `risk-liveness` passed only because its
    high-X predicate could not be evaluated. A rule that cannot fail is not a green light.
12. **A green exit code can come from a run that checked nothing.** Confirm a mutant actually
    COMPILED, and **reconcile test COUNTS** — 69 vs 66 is not +3 if the earlier run had a flake.

### D — Writing to the package

13. **Build payloads from `data/*.jsonl`, never from `entity_query` output** — v4 needs FULL rows and
    the store holds **truncated** ones, so rebuilding a row from a query re-commits the damage.
14. ⚠ **A GENERATED payload must be PASTED, not RE-TYPED.** The generator was right; I retyped its
    output and flipped a value. **The hand is the untrusted transport.** End any N-row repair with an
    independent re-read that re-derives each value from its source — care will not catch it.
15. **Omitting `custom_attributes` PRESERVES it; sending it REPLACES the whole blob.** Merge, never
    overwrite.
16. **Run `gate_run()` AFTER writing, not only before.** Quoting a design file's template syntax into
    a progress entry trips `G-COMPLETE`; **code-span quoted fragments**. ⚠ `progress_entries` is
    **append-only** — two bad rows could not be edited, and the repair was a whole-file git rollback.

### E — Environment

17. **`.cs` files need a UTF-8 BOM and LF**; the Write tool adds neither and `dotnet ef` writes CRLF.
    ⚠ `gh pr create --body` and `git commit -m` with backticks break under PowerShell — use
    `--body-file` / `-F`.
18. **Measure a Keycloak grant, never assume it.** Under exactly `{manage-users}`: `GET /users` 200,
    `/users/{id}/role-mappings/realm` 200, but `GET /roles/{name}/users` **403** — the *clever* shape
    is the one the minimal grant refuses, and it would have failed in production only.
19. **Never write Arabic as unicode escapes** (`DEF-064`) — write literal UTF-8. `check-i18n.mjs`
    compares KEY SETS, not values, so "parity OK" can be true and meaningless.
20. **Ancestry is the wrong test for "is this branch's work already shipped."** `git branch -d`
    refuses a branch whose commits are unreachable even when its TREE is identical — the
    squash-merge signature. Use the three-dot tree diff.
21. **`rm -rf tests/*/TestResults` before trusting a local coverage run** (`DEF-069`; CI is fine).
22. ⚠ **`gh pr checks --watch` AND `gh run watch` BOTH REPORTED SUCCESS ON RUNS THAT HAD NOT
    FINISHED** — the first exited **0** after a TLS handshake timeout while `e2e` was still pending,
    the second returned while the run was still `in_progress`. Believing either would have flipped an
    AC verdict on a run that never completed. **Poll the `status` field until it reads `completed`,
    then read `conclusion`** — this is trap 12 arriving through the tooling rather than the tests.
23. ⚠ **NEVER `git checkout -- .` WITH UNCOMMITTED WORK IN THE TREE — including during mutation
    testing.** Reverting each mutant that way destroyed the implementation it was testing, because
    the implementation had not been committed yet. **Commit first, then mutate against the commit.**
    It is the same lesson the package data already carries (C31), and source is no different.
24. ⚠ **A LOG TAIL IS THE WRONG INSTRUMENT FOR A FAILURE WHOSE DISTANCE YOU DO NOT KNOW**
    (`DEF-074`). `--tail 200` across nine services covered ~15 seconds for a failure six minutes
    back, and printed healthy 200s — misleading rather than absent. Raising it to 4000 was the same
    mistake with a bigger number. **Dump the api log with no tail at all.**

## §7 — Definition of done

Unit + integration tests, each guard proven by FORCING its refusal and verified to FAIL without the
change · flip AC verdicts via `audit_record` with evidence, and say plainly when something is
ANALYSIS rather than a measurement · authorization enforced server-side, `AuditEvent`s asserted as
ROWS · no hardcoded strings, EN + AR together, RTL verified · **if it is visual, look at it** · no
secrets, never print a live credential · `progress_update` + `work_bind`, then `gate_run()` and
`export_html()`; **write the package only from `main` and commit immediately** (`tamheed-package/data`
is git-tracked) · conventional commits, small and reviewable · branch → PR → green CI →
squash-merge · **register every finding as a Tamheed row AS YOU GO — including findings against your
own work, and corrections to evidence you yourself recorded.**

Report the state and your plan before writing, then proceed.

=====

## Not part of the paste — notes for the operator

**Three interview rounds are now recorded** as `DEC-046`/`047` (the first queue), `DEC-048` (the four
reserved items), and `DEC-049`/`050` (build all four candidates, the full AC binding, the full risk
traceability pass, both minor defects, and the branch audit). Nothing is carried in anyone's head.

**Field reports:** `findings_17.md` (the v4 migration + first liveness sweep) and `findings_18.md`
(the 4.2.0 repairs, the row I corrupted by re-typing, and the hollow-pass finding). Read 18 first.

**Stale branches — audited 2026-08-15.** `scaffold/ph0-p1-foundation` is deleted (0 unique commits,
empty tree diff). The other six all hold unique commits and were deliberately NOT deleted:
`chore/design-update-round2` (1), `chore/docs-v8-local-design` (1), `docs/defer-p14-tarseem` (1),
`feat/audit-adr` (2), `feat/budget-notification-observer` (2), `feat/p13-webex-integration` (**13
commits, 124 files**). They still carry `DEF-064`'s broken `ar.json`, but that is guarded now —
merging one fails `check-i18n` loudly. p13-webex is probably content-duplicated by the P13 PRs that
shipped; 124 differing files is too many to assume, so it needs a content check before deletion.

**The tamheed acceptance series (`findings_13`–`findings_16`) is still open on §6 only** — whether a
fresh session DISCHARGES the recording obligation rather than merely listing it. ⚠ **A session
started from THIS prompt cannot close it**, because the prompt instructs recording and so
contaminates the experiment. To close it, paste a small real task into a normal window, say nothing
about recording, and see whether the rows appear.
