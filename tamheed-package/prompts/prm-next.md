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

`main` is green, clean and pushed. (`git log --oneline -5` is the authority — no hash is quoted here
on purpose, because the last one written into this file went stale the same day.)

- **The store is Tamheed v4** (server 4.2.1). See §5 for what that changed under you.
- **`ADR-0043` stream scope is COMPLETE**, and so is everything the `DEC-046`/`047` queue asked for
  except `DEF-039`. Do not redo any of it.
- **Production runs `65e45d4` and is UNUSED** — zero topics, one of 26 members has ever signed in.
  **Nothing in this stream is deployed.**
- **6 open defects**: `DEF-012`, `DEF-038`, `DEF-039`, `DEF-056`, `DEF-065`, `DEF-067`. Four more were
  opened AND fixed on 2026-08-15 — `DEF-073`, `DEF-074`, `DEF-075`, `DEF-076` — all merged.
- **4 unmet ACs**: `AC-003`, `AC-006`, `AC-041`, `AC-048`. ✅ **`AC-011` is MET** (`AV-163`).
- `DW-026` is **Activated** (approved to build), `DW-028` is new and Open.

### ⭐ Where the 2026-08-15 session stopped, and why

**`AC-011` IS MET (`AV-163`) — proven live, both clauses measured.** Everything below is DONE and
merged; do not redo any of it. What is left is `DEC-052` d4's approved queue, in §3.

Turning ONE flag on (`KEYCLOAK_ADMIN_ENABLED`, `DEC-042`) exposed **four defects nothing else could
see**, all now Fixed and merged:

| | | |
|---|---|---|
| `DEF-073` | a guest could read **every meeting in the committee** — this AC's own second clause answering 200 | #278 `567da16` |
| `DEF-074` | the e2e failure dump could not reach the failure it explains | #278 `567da16` |
| `DEF-075` | JIT provisioning check-then-insert race — `POST /api/members/me` was not idempotent | #279 `3a27fef` |
| `DEF-076` | **high** — an invited guest got NO Keycloak realm role, so FR-159's guest could never use the surface built for them | #280 `fc824a5` (`SC-012`) |

The leg itself merged as #281 `1598ac0`; `AC-011` flipped on CI run `31895092054`.

⚠ **Two near-misses worth carrying, both an assertion passing for the wrong reason.** (1) The expiry
test first passed its `401` check while measuring nothing — `page.request` does **not** attach the
`Authorization` header, so the call was merely *unauthenticated*, and only the
`X-Acmp-Auth-Reason: access_expired` assertion told the two apart. Asserting the status alone would
have flipped this AC on a test that never exercised the expiry path. (2) A mutant appeared to
**survive** and was not believed: the patch targeted a string that does not exist in the file, so it
never applied. **A surviving mutant you have not verified is not a test result.**

### ⏸ `DEF-056` + `AC-003` are STARTED, on `feat/def-056-refusal-audit`, and NOT merged

Both emitters are written and building; **neither is proven**, so the branch is parked rather than
merged — an unproven audit emitter is worse than none, because it makes a reader believe refusals are
recorded. Pick it up there rather than starting over.

- `AuditingAuthorizationResultHandler` records the policy-layer 403s that short-circuit before
  MediatR. **Forbidden only, never Challenged.** `CancellationToken.None` deliberately, so a client
  hanging up mid-refusal still leaves the record.
- `ProvisionCurrentUser` emits before its own `ForbiddenAccessException` — the middleware handler
  **structurally cannot** catch that one, because the request *passes* authorization and is refused
  inside the handler.
- `RefusalAuditTests` asserts both as ROWS, plus two controls (unauthenticated → no row; allowed
  caller → no row).

⚠ **THE BLOCKER, measured not guessed.** Both positive tests fail with an **empty** audit table. A
temporary `if (true)` probe in the handler still produced no row → **it never runs in that harness**.
The refused response has an **empty body** — the framework's bare forbid, not ProblemDetails — so the
authorization middleware genuinely refused. **Next thread to pull:**
`IAuthorizationMiddlewareResultHandler` does not resolve from `Program.cs`'s top-level statements NOR
from `Acmp.Api.Tests`, yet resolves fine in a file beside the implementation in the same project.
Registration currently routes through an extension method for that reason. Until that is understood,
whether the handler is registered at all is **unverified**.

⚠ Consider that the real proof for `DEF-056` may be **e2e**, not the API harness: the defect was
found by the live leg, and `PermissionMatrixTests` never goes through HTTP.

⚠ `DEC-051` settled `AC-048` (supersede, narrow to the mechanism) and `AC-041` (property-level
detectors + the Edge project, **not** pixel baselines). `AC-048` also needs slice binding, so do the
narrowing and the binding in **ONE** supersession — otherwise its successor id is minted twice.

⚠ `AC-003` needs **its own emitter**: `AV-159` shows its 403 is thrown in the HANDLER, so the
`IAuthorizationMiddlewareResultHandler` that `DEF-056` will register **cannot** catch it.

⚠ `role-matrix.spec.ts`'s deliberate `test.fail()` for `AC-006` flipped to *"Expected to fail, but
passed"* while `DEF-076` was open, because a role-less guest's refusals DO reach
`AuthorizationBehavior`. That marker is meant to flip when **`DEF-056`** lands. **Do not read a green
`AC-006` as `DEF-056` being done** — verify the emitter exists.

## §2 — Why `readiness_check` says `ready:false`, and why that is right

`gate_run` 7/7 and readiness `false` are not in conflict: the gates are **mechanical**, readiness is
**lifecycle**. It is blocked on `acs-met` (the four above), `defects-closed` (`DEF-065` — the deploy
tracker, and the ONLY high/critical one left) and `risks-discharged`. That is the honest state of a package whose work is not finished — do not "fix"
it by closing rows.

⚠ **`risks-discharged` is blocking AND cannot discriminate** (0 of 23 rows have `discharged_by`), so
it lists every open risk by construction. `DEC-050` d2 decided a full traceability pass. **Read that
row first — it is a risk REVIEW, not data entry.**

## §3 — ⭐ THE QUEUE. Start here. (`DEC-052` d4 approved ALL of it.)

All operator-decided. **Read the `DEC-` row before starting an item** — the rejected options and the
reasons are on it. Do not re-litigate. This is a **multi-session** queue and was recorded as one
rather than attempted in one sitting.

### 1. `DEF-056` + `AC-003` — STARTED, PARKED, NOT MERGED. Pick it up, do not restart it.

Branch **`feat/def-056-refusal-audit`**. Both emitters are written and building; **neither is proven**,
which is exactly why it is not merged — an unproven audit emitter is worse than none, because it makes
a reader believe refusals are recorded. The full state is in §1; the one-line summary is that the
handler **never runs** in the API test harness and the reason is not yet understood.

**Start by settling the resolution boundary** (§1 has the measurements): if
`IAuthorizationMiddlewareResultHandler` cannot be named from `Program.cs` or the test project, then
whether the registration takes effect at all is unverified, and every other question is downstream of
that. ⚠ Strongly consider that the real proof is **e2e**, not the API harness: this defect was found
by the live leg, `PermissionMatrixTests` never goes through HTTP, and `role-matrix.spec.ts` already
carries the `test.fail()` marker that flips the day it lands. **Delete that line when it does.**

Flips **`AC-006`**, and `AC-003` rides with it — but note they need **two** emitters, not one: see §1.

### 2. `AC-041` — property detectors + the Edge project (`DEC-051`)

Two gaps, not one. `e2e/vr-sweep.spec.ts` is **capture-only** — `page.screenshot()` into `vr-out/`, no
`toHaveScreenshot`, no baseline, **no assertion of any kind** — so the AC's verb "detected" is
performed by a human opening PNGs. `DEC-051` chose **property-level detectors** in the style of
`src/test/rtl-logical-css.test.ts` over pixel baselines, on this project's own measured experience
that the property guard catches real defects while the capture sweep has caught none.

⚠ Adding the Edge project is **not one line**: `e2e.yml` installs **chromium only**, so without
`msedge` added there every Edge test dies at launch rather than failing informatively.

### 3. Package track — no CI cycles at all, and the cheapest after a long session

- **Full AC binding, all 20** (`DEC-050` d1 — see `acs-slice-bound`). ⚠ An Approved AC's content is
  **immutable**, so any needing a different binding must be **superseded**, which mints new ids and
  moves verdict history onto rows that did not earn it. Row by row; supersede only where the binding
  genuinely changes scope.
- ⚠ **`AC-048` folds in here.** `DEC-051` settled it: **not a build item at all** — its Partial is
  final as written (no automation can prove a browser showed a human a dialog), so it is **superseded
  and narrowed to the mechanism**. It also needs slice binding, so do the narrowing and the binding in
  **ONE** supersession, or its successor id is minted twice.
- **`risks-discharged`, all 23** (`DEC-050` d2). ⚠ It is a risk **REVIEW**, not data entry. Several
  PH-0 risks need a judgement about whether they are still live — `RISK-005`'s subject was deferred
  indefinitely by `DEC-028`. Where nothing genuinely discharges a risk, **retire or accept it
  explicitly**; pointing it at a convenient AC to clear the rule is the manufactured-status failure
  recorded as `DEF-010`.

### 4. `DEF-039` + the two minor defects

- **`DEF-039`** — the System Health object-store tile. `DEC-047`: bind it to what the environment
  actually runs (MinIO on-prem, S3 on cloud). ⚠ Needs an environment-aware **probe**, not a config
  flag — the check itself differs between MinIO's `/minio/health/live` and S3.
- **`DEF-067`** — `DecisionPage.test.tsx` fails intermittently under `test:cov` only. ⚠ Fix by
  **awaiting the settled state** (`findBy*`/`waitFor`) — **never** a timeout or a retry, both of which
  hide a genuine regression the day one occurs.
- **`DEF-012`** — `v_backlog` residue; a derived-view artifact of the v2.3 import.

### 5. `DW-026` — the architecture test. LAST, deliberately.

A public aggregate method with no production caller. Its allowlist must be seeded with the gaps that
exist **when it is written**, and every build above may add or remove uncalled methods — seeding it
first guarantees a stale allowlist on the day it merges. **Start with the narrow policy-coverage
check**: every `IAuthorizationRequirement` appears in at least one registered policy. A handful of
lines, and it guards the only fails-open case. (It has now fired **five** times.)

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
- ⚠ **THE CHECK CONSTRAINTS REJECT PLAUSIBLE VALUES, AND THE ERROR IS THE ONLY DOCUMENTATION.** Three
  upserts were rejected in a row on 2026-08-15 for guessing these; the store tells you the allowed set
  when it refuses, so read the error rather than trying another synonym:
  - `audit_record` → `verified_by` is **`human` | `agent` | `ci`** (not a person's or model's name),
    and `verification_method` is **`auto-test` | `manual` | `inspection`** (not a prose description —
    put that in `evidence`).
  - `defect.lifecycle_status` is **`Open` | `In-progress` | `Fixed` | `Won't-fix` | `Duplicate`** —
    **not** `Implemented`, which is the *work-item* vocabulary.
  - `progress_update.event_type` is **`work-done` | `verdict-recorded` | `transition` |
    `forced-override` | `gate-decision` | `escalation` | `correction` | `note`**.
  - A batch that violates one constraint **rolls back entirely** (`applied: 0`), so a rejected
    multi-row upsert leaves no partial state — re-send the whole batch once corrected.
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
25. ⚠⚠ **AN ASSERTION THAT PASSES FOR THE WRONG REASON IS THE MOST EXPENSIVE KIND OF GREEN**, and it
    happened TWICE on 2026-08-15. (a) The `AC-011` expiry test asserted `401` and passed — while
    measuring nothing, because `page.request` does **not** attach the `Authorization` header, so the
    call was merely *unauthenticated*, which also answers 401. Only the second assertion
    (`X-Acmp-Auth-Reason: access_expired`) told "refused because expired" from "refused because
    anonymous". **Asserting the status alone would have flipped an AC on a test that never exercised
    the path.** (b) A mutant appeared to **SURVIVE** — which would have meant the guard was
    worthless — and the patch had simply targeted a string that does not exist in the file, so it
    never applied. **A surviving mutant you have not verified is not a test result.** Check the
    mutation landed AND compiled before believing either outcome.
26. **WHEN YOU FORK A HELPER, FORK ITS POST-CONDITIONS.** `loginWithTemporaryPassword` was modelled on
    `loginAs` but waited only to leave Keycloak's origin, not for the SPA to be signed in — so the
    next navigation raced `ProtectedRoute`, was bounced to `/login`, and issued no authenticated
    request at all. That surfaced two functions away as `waitForRequest timed out`, saying nothing
    about the login. `loginAs` asserts the CTA is gone for exactly that reason; the copy dropped the
    assertion that made the original correct.
27. **A TIMEOUT IS A SYMPTOM; THE CALL LOG SAYS WHICH.** Two failures wore the identical face
    (`"beforeAll" hook timeout`) with completely different causes. Playwright prints *what it was
    waiting for* and *which line* — and because the lines ABOVE had already succeeded, one ruled
    itself out as slowness and pointed at a single wrong selector. ⚠ Raising the timeout first would
    have bought a slower failure in the same place and confirmed the wrong story.
28. **TWO WRONG GUESSES AT THE SAME ELEMENT MEANS STOP GUESSING.** Two runs were spent on Keycloak's
    update-password submit control (`button[type=submit]`, then `input[type=submit]`); both matched
    nothing. Pressing **Enter** in a text input submits the owning form natively — one assumption (the
    form exists, already proven by the fills above) instead of three (element kind, container id,
    label). **The right answer to a selector that matches nothing is often to stop needing it.**
29. ⚠ **NOTHING A LATER SESSION MUST READ MAY LIVE IN THE SCRATCHPAD.** It is session-scoped, so a
    pointer into it is a dangling reference the moment the session ends — and it does not fail loudly,
    it is simply a path that is not there. Four defect rows were closed citing scratchpad snapshots;
    the durable copies are now `handoff/def-title-snapshots/` (corrected in `PE-357`, not by editing
    the rows). Repository or package, never scratchpad.
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

**Five interview rounds are now recorded** — `DEC-046`/`047` (the first queue), `DEC-048` (the four
reserved items), `DEC-049`/`050` (build all four candidates, the full AC binding, the full risk
traceability pass, both minor defects, the branch audit), `DEC-051` (the two items that reading the
verdict rows proved were NOT build items), and `DEC-052` (`DEF-076` guest-path-only + `SC-012`, the
PR split, the live-expiry standard for `AC-011`, and approval of the whole remaining queue). Nothing
is carried in anyone's head.

⚠ **On two of those the operator OVERRULED my recommendation, and both are marked as such on the
row** — `DEC-049` d3 (the full AC binding, over accepting package-scope-only verification) and
`DEC-052` d3 (a live expiry for `AC-011`'s window, over composing the verdict from two proofs). The
second one was right in a way worth remembering: building it is what exposed the 401-that-meant-
unauthenticated (trap 25a).

**A branch is parked, deliberately: `feat/def-056-refusal-audit`.** It is NOT abandoned work and NOT
mergeable as-is — see §3 item 1. An unproven audit emitter is worse than none.

**Field reports:** `findings_17.md` (the v4 migration + first liveness sweep) and `findings_18.md`
(the 4.2.0 repairs, the row I corrupted by re-typing, and the hollow-pass finding). Read 18 first.

**Stale branches — audited 2026-08-15, unchanged since.** `scaffold/ph0-p1-foundation` is deleted (0
unique commits, empty tree diff). The other six all hold unique commits and were deliberately NOT
deleted:
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
