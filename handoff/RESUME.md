# RESUME — ACMP

**The single entry point. Rewritten whole 2026-08-13 at session end.** Every `handoff/RESUME-*.md`
and every `handoff/prm-*.md` is ⛔ superseded history. **Tamheed v3.0.0 moved prompts into the
package**, so the kickoff prompt now lives at `tamheed-package/prompts/prm-next.md` — edit it, never
add another `prm-*.md`.
This file is durably named. The paste-able kickoff prompt is **`handoff/prm-next.md`** — edit it,
never add another `prm-*.md`.

---

## 0. Orient (2 minutes, do not skip)

```
server_info() · package_open("tamheed-package") · gate_run()
```

⚠ **If `package_open` fails on `.lock`**, check the PID *properly* before removing it — the lock holds
a bare PID and "is it alive?" **lies** under PID reuse. Confirm the process does not exist, or that
its identity and `StartTime` do not match the lock's mtime, then delete
`tamheed-package/data/.lock`. Never remove it reflexively.

Then read **§2** (rules), **§3** (the finding), **§4** (the work). `SC-003`…`SC-009` record nine
places an approved document and the code diverged and *why the code was right* — `SC-009` is newest
and records **two approved ACs contradicting each other**.

---

## 1. State

| | |
|---|---|
| `main` | **green** · gates **7/7** · tree clean, nothing unpushed ⚠ (no sha here on purpose — the commit that writes this file changes it) |
| Verdicts | **87 Met · 6 Partial · 0 Pending** over 93 ACs — **148 evidenced / 12 narrated** |
| **Production** | live, always-on · `i-04d9717feea79204b` · https://acmp.anas7ammo.dev · runs **`65e45d4`** |
| ⚠ **Prod is UNUSED** | **0 topics · 0 streams · 0 member-stream links · 1 of 26 members has ever logged in.** The stack serves; the *product* has not been used. |
| **UAT** | **stopped** · `i-07ac28ac2fedab921` — start from `deploy/runbooks/cloud-operations.md` §1 |
| In-app user management | **ENABLED on both** (`KEYCLOAK_ADMIN_ENABLED=true`, pinned 32-char secret) |
| Open defects | `DEF-057`(high) `DEF-058` `DEF-056` `DEF-012` `DEF-038` `DEF-039` `DEF-041` `DEF-055` (**8 of 58**) |
| Open questions | **`OQ-074` only** |
| Deferred work | **`DW-026`** — the wiring guard (§3; the biggest generalizable finding here) |

**Phases `P1`–`P19` are COMPLETE.** `P14` deferred indefinitely (`DEC-028`). What remains is **not a
new slice** — it is §4.

### ⚠ Deployment: prod is behind by exactly ONE product change

Prod runs `65e45d4`. The newest sha **with ECR images** is **`85068c9`**. Everything between them is
tests, governance or docs **except one commit**:

> **`e9b2155` — the 30-minute idle sign-out (`AC-004`)**: `AuthProvider.tsx`, `authStatus.ts`,
> `useIdleSignOut.ts`, `LoginPage.tsx`, `en.json`, `ar.json`.

Deploying is a small, well-scoped decision — not a 56-commit release like last time. ⚠ **Never trust
a sha written in this file.** `ci.yml` `paths-ignore` skips `*.md`, `docs/`, `.claude/`,
`tamheed-package/`, so governance commits publish nothing. Ask ECR — and `web` is a **separate,
environment-suffixed** tag (`ADR-0037`, `DEF-019`); `08-bootstrap-box.sh` refuses unless **both**
pins resolve:

```bash
for r in api worker sqlserver-fts; do
  aws ecr describe-images --region us-east-1 --repository-name "acmp/$r" \
    --query 'sort_by(imageDetails,&imagePushedAt)[-1].{tag:imageTags[0],at:imagePushedAt}' --output text
done
aws ecr describe-images --region us-east-1 --repository-name acmp/web \
  --query 'sort_by(imageDetails,&imagePushedAt)[-2:].{tag:imageTags[0],at:imagePushedAt}' --output text
```

---

## 2. ⚠ Rules this project has paid for. Read before writing code.

**A. Read the implementation before calling something a defect.** Eleven instances; none caught by a
gate. It has made defects vanish (`DW-025`), smaller, and *bigger* (`DEF-057`). ⚠ **It applies to
REGISTER ROWS too** — `DEF-045`'s stale "cause 3 NOT FIXED" was repeated to the operator without
reading the two specs it described. Both were already fixed.

**B. An ADR/AC citation in a test name is load-bearing, and no gate reads it** (`SC-004`, `SC-007`).

**C. When an ADR names a seam, check the harness can reach it before approving** (`SC-005`).

**D. Check whether it is already built.** ⚠ The operator's recollection of a topic-side "all streams"
concept was **correct** (`TopicScope.OrgWide`) — checking it produced `DEF-058`.

**E. A green suite is not a look.** Render new screens in a browser, in **both** directions. The
throwaway harness must import **only** the stylesheets the real route imports.

**F. Prove, don't assume.** `OQ-070`'s answer (`manage-users` alone) contradicted the written
candidate, and no gate would have caught the wider grant.

**G. Verify the DEPLOYED state, not the file describing it.** Prod was 56 commits behind while the
handoff called enabling a flag "one variable".

**H. A measurement that indicts known-good code is measuring itself.** `grep -c $'\r'` lost its
quoting, degraded to `grep -c ''`, and reported CRLF for every `.sh` including one CI runs green
daily. Use `tr -cd '\r' | wc -c`. ⚠ **Never accept one tool's negative as proof of absence** — `Grep`
returned "No files found" for a string PowerShell found instantly.

**I. ⚠ A green exit code from a build that checked nothing.** `tsc -b` reported **exit 0 and zero e2e
files** even after wiring them in; only `--force` showed the real 34. Incremental builds skip
up-to-date projects. **Verify a guard covers what you think by counting what it looked at.**

**J. ⚠ PowerShell 5.1 `Get-Content` reads ANSI; `WriteAllText` writes UTF-8.** A read-modify-write
round-trip **corrupted `MEMORY.md`** (46 mojibake markers), and the console output is
indistinguishable from a harmless rendering artefact. Check bytes: `grep -c 'Ã\|â€\|â˜'`. Splice in
Bash, or pass `-Encoding UTF8` at both ends. Commit before any bulk rewrite.

**K. ⚠ The test must fail without the change.** Everything this session was mutation-checked:
reverting `App.tsx` failed exactly the 5 denied roles; neutering `assert-oneshot.sh` failed exactly
the 2 dependent cases; removing `stopSilentRenew` **and** reversing its order each failed one test.

---

## 3. ★ The finding to act on: FOUR unwired capabilities (`DW-026`)

Found in one session, none by any gate:

| | what | consequence |
|---|---|---|
| `Topic.Reopen` | correct aggregate method, **no endpoint** | `AC-009`'s positive clause unreachable → `SC-009` narrowed an approved AC |
| `Stream.Create` | factory, **no caller anywhere** | no stream can exist — half of `DEF-057` |
| `StreamScopeHandler` | in DI, unit-tested 4 ways, **in no policy** | never evaluated, **FAILS OPEN** — `DEF-057`, high |
| `Topic.SetScope` | **no caller**, no `Scope` on the command | `TopicScope.Platform`/`OrgWide` unreachable — `DEF-058` |

**Every one presents identically:** the method exists, is correct, its comment explains its purpose,
and two are unit-tested and passing. Ask *"is this implemented?"* and every signal says yes. **The
only thing missing is the wiring, and nothing in the build looks at wiring** — not the compiler, not
the unit tests (they call it directly, which is *why* they pass), not coverage (the method **is**
covered, by its own test).

⚠ **Cheapest first (`DW-026`):** assert every `IAuthorizationRequirement` type appears in at least one
registered policy. A handful of lines, and it guards the fails-open case.

---

## 4. Everything left, in order

### 1. ★ `DEF-057` + `DEF-058` — approved design, **eight-step slice**, nothing built

**`ADR-0042` Approved** (supersedes `ADR-0041`, Rejected-and-kept). Reasoning: `PE-293`, `DEC-042`.
⚠ **Treating this as one change lands the seed without the parts that make it safe.**

✓ **No design needed for two things.** `CommitteeWide = { Chairman, Secretary, Auditor, Administrator }`
**already bypasses** stream scope — the Chairman/Secretary worry is handled by existing code.
Stream-bounded = Member, Reviewer, Submitter, Guest (~26 of 27 prod users).

**Posture: FAIL-CLOSED with mandatory assignment.** No empty-set special case — forgetting to assign
**refuses** rather than permits. "Unrestricted" is stated with a wildcard.

**THE ORDER IS LOAD-BEARING. Any other order produces an outage or a control that cannot work.**

1. **Seed 5 streams + a wildcard row.** `Stream.IsWildcard` is a **BOOLEAN COLUMN, never a magic
   code** — a renamed stream would silently break the bypass, or a future one collide into universal
   access. Operator-confirmed:
   `core`/Core/الأساسي · `communications`/Communications/الاتصالات ·
   `smart-cities`/Smart Cities/المدن الذكية · `government`/Government/الحكومي ·
   `shared-services`/Shared Services/الخدمات المشتركة.
   ⚠ For the wildcard reuse the committee's **own** wording — i18n already has
   `reports.filter.allStreams` = "All streams" / "كل المسارات".
2. **Topics PICK from the taxonomy**; the validator rejects free text. Without this the control
   **cannot work** — the provider returns `Stream.Code` (lowercased) while topics carry typed strings,
   so `"Smart Cities"` never matches `smart-cities`. ⚠ **Do it now: prod has 0 topics so there is no
   migration — and that stops being true on first use.** Touches `SubmitTopicCommand`, its validator,
   `SubmitTopic.tsx`, `UpdateTopicCommand`, and **every test passing `streams: ['Platform']`,
   including `apiCreateTopic`, which every e2e fixture uses.**
3. **Build the assignment UI** — it does not exist. `UsersMembership.tsx` displays streams; its own
   header says assignment is **INERT** pending BL-024. Only Administrator-only
   `PUT /api/members/{publicId}/streams` sits behind it.
4. **`FR-156`'s invite gains a REQUIRED stream field**, plus a loud "No stream assigned" roster state
   and a self-explaining refusal as the backstop.
5. **⚠⚠ BACKFILL ALL 26 EXISTING MEMBERS.** They were seeded straight into Keycloak, so step 4 does
   **not** cover them. **Miss this and the whole committee is locked out the day the check lands.**
6. **Fix `DEF-058`** — add `Scope` to `UpdateTopicCommand` (metadata, already Secretary/Chairman-gated
   post-Accept) + triage UI — and expose the OrgWide fact to authz as a **primitive `bool` on
   `IStreamScopedResource`**. ⚠ **Not** the `TopicScope` enum: the contract lives in
   `Shared.Contracts`, the enum in `Topics.Domain` (`ADR-0001`; `ADR-0021` is the pattern).
   Platform/OrgWide topics then bypass stream scope.
7. **Wire `StreamScopeRequirement`** into the stream-bounded write policies.
8. **Evidence `AC-010`** ⚠ against a member assigned to a **different** stream than the topic — never
   an unassigned one, whose refusal proves nothing about stream scope.

⚠ **Accepted escalation path:** a Secretary can widen write access by elevating a topic's scope.
Deliberate, Secretary/Chairman-gated, audited.

### 2. `DEF-056` — build the `IAuthorizationMiddlewareResultHandler` (decided, `DEC-042`)

A refused mutation leaves **no audit trace**: every write endpoint 403s at the ASP.NET policy layer
**before** MediatR, and `AuthorizationBehavior:39` is the only emitter of `Authorization.Forbidden`.
`SqlAuditSink` is innocent (it saves immediately).

⚠ **Two constraints, established before any code:** recover the failing policy name from
`policy.Requirements` (`CapabilityRequirement` carries `PolicyName` — confirm **one**, not a merged
set), and emit **only** on `authorizeResult.Forbidden` — never `Challenged` (the 401 path) and never
success. Resolve the sink from `context.RequestServices`.

**When it works, the `test.fail()` case in `e2e/role-matrix.spec.ts` goes RED on purpose.** Delete
that line and flip **`AC-006`** to Met with it as the evidence.

### 3. `AC-011` — turn `KEYCLOAK_ADMIN_ENABLED` on in CI's e2e stack (decided, `DEC-042`)

Unblocks far more than one AC: the **entire** ADR-0038 write path is registered only when the flag is
on, so CI today runs seven services with a real Keycloak and **never constructs `IIdentityProvider`**.
✓ **Verified safe:** `deploy/docker-compose.yml` already mounts `KeycloakAdmin__ClientSecret` into
`keycloak-config`, `api` and `worker` (163/183/338) and declares the file (433). Prefer `e2e.yml`'s
env (CI only) over `deploy/.env.example` (which changes dev too).

### 4. `AC-003` — the cheap live test (defaulted, no decision needed)

Reading settled the behaviour: `PrimaryRole(...) ?? throw new ForbiddenAccessException(...)` — a
role-less token is **denied**, fail-closed, which is the AC's first branch. What is missing is a
measurement and the audit clause. Seed an e2e user with **no** committee realm role (`seedUser`
requires one, so `realmRole` must become optional), log in for real, assert `POST /members/me` → 403
and look for an audit row. ⚠ Its audit clause rides on `DEF-056`. ⚠ **Do not confuse this with the
UAT observation that an *invited* member reads `Guest`** — that is the roster DTO for an existing
row, a different code path.

### 5. `AC-041` — decide the instrument (defaulted; overrule if you disagree)

`vr-sweep.spec.ts` is **capture-only** (`page.screenshot({path})`, no baseline, **no assertion**), so
"detected" means a human opening PNGs; and `playwright.config.ts` has **one project, `chromium`**, so
"Chrome *and Edge*" never ran. ⚠ RTL is **not** unguarded — `rtl-a11y` asserts `dir`/`lang` + axe, and
`rtl-logical-css.test.ts` mechanically fails the build on asymmetric corner radii (the `wiki.css`
defect only an Arabic reader sees). **Default: property-level guards over pixel baselines, Edge
unrun** — property guards here have caught real defects; capture-only sweeps have caught none.

### 6. Deploy the idle sign-out — one product commit (`e9b2155`), operator's call. See §1.

### 7. Smaller, still open

- **`AcceptTopic.cs`'s comment is now a FALSE LEAD** — it cites `AC-009` as the reason for
  grant-on-accept, and `SC-009` removed that clause. Fix the comment.
- **Is the Owner grant consulted by *anything* over HTTP?** Unverified. Other `O` rows
  (`ActionCreate`, `RiskManage`, `AdrCreate`…) are topic-scoped and *may* — ⚠ but `apiCreateAction`
  records that `SourceId` has **no cross-module FK** (`ADR-0001`), so whether Actions can resolve a
  Topic resource at all is open. `DEF-057` proves a handler can be wired into nothing.
- **`AC-090`'s text cites a "60-minute idle timeout"**; the realm says **1800s**. The verdict does not
  rest on it; the number is wrong.
- **`OQ-074`** — whose view do Chairman/Secretary "preview" on `/session`? Shipped as their own slot.
  ⚠ `navModel.ts` grants `session` to **guest only**, so they have **no nav link** to a page they are
  permitted on. Answering has a nav consequence either way.
- **`DEF-038`** roster residue · **`DEF-039`** MinIO tile on an S3 cloud · **`DEF-041`**
  voting-eligibility toggle absent from the a11y tree · **`DEF-055`** `09-put-env.sh`'s wrong refusal
  message · **`DEF-012`** package-data residue.
- **`OQ-062`** is stricter in code than in the decision — a *permanent* UAT Webex ban vs "off
  **until** a UAT space exists", so the exit condition can never be met.
- **★ Get one real user through one real flow on production.** Zero topics, one login. The stack is
  proven; the product is not.

---

## 5. Gotchas that cost real time

- **Deploy as `acmp-admin`, never root** (root bypasses the budget IAM-deny brake, `AC-085` leg 5;
  `[default]` **is** root and its session expires).
- **The deploy sequence that worked:** back up prod and **confirm the object in S3** → start UAT and
  poll **SSM `PingStatus`** (`instance-running` is not readiness) → fetch `/acmp/<env>/env`, re-pin
  both tags → `09-put-env.sh` → `08-bootstrap-box.sh <env> <full-sha>` → `smoke.sh` → read the
  **`keycloak-config` log** back over SSM.
- ⚠ **PowerShell joins arrays with SPACES.** `aws … --output text` returns an **array of lines**;
  `[IO.File]::WriteAllText(path, $array)` writes one space-joined line and would have **destroyed the
  SSM env file**. Join with an explicit newline and verify the line count before publishing.
- **`/acmp/*` env parameters are LF**, not CRLF — an older note said CRLF and went stale the same
  evening; `aws ssm get-parameter-history` settles it in one call.
- **The keycloak container's `docker exec` shell has no `KC_BOOTSTRAP_ADMIN_PASSWORD`** — the
  entrypoint exports it for its own process only. Read `/run/secrets/kc_bootstrap_admin_password`.
- **Use PowerShell for any `aws` call with a `/`-leading argument**, or `MSYS_NO_PATHCONV=1`.
- **Windows `python3` cannot see Git Bash's `/tmp`** — use Windows-style absolute paths for SSM
  `--parameters file://` payloads.
- **Write the Tamheed package only from `main`.** `defect.fixed_by` is a **FK** (PR refs go in
  `custom_attributes`); `open_question.lifecycle_status` is a **CHECK** over
  `Draft/Proposed/Approved/Rejected/Deferred/Implemented/Superseded/Obsolete` ("Resolved" rolls the
  batch back); `decision` has **no `status` column** (it is `lifecycle_status`);
  `deferred-work.invariant_at_stake` is a **FK**, not prose. ⚠ **FK ordering matters inside one
  batch** — put the referenced row first or the whole batch rolls back.
- **`gh pr create --body` and `git commit -m` with backticks or quotes break under PowerShell** —
  always `--body-file` / `-F <file>`.
- **`main`'s push run can fail for reasons that are not the code.** `#250`'s did twice — a 25-minute
  `backend` timeout, then 25 tests failing on 100-second `HttpClient` timeouts against an
  **in-process** TestHost (runner starvation). The third attempt passed clean. **Check what the
  failing commit actually changed before concluding `main` is red for a code reason.**
- **A compose `secrets:` entry whose file is MISSING fails the WHOLE stack.**
- **New `.cs` files need a UTF-8 BOM**, and `.cs` must be **LF**.
- **Never run `gen-secrets.sh` against the repo to test it** — `SECRETS_DIR` is hardcoded and it
  clobbers the operator's live dev secrets. (This is *why* `assert-oneshot.sh` was extracted.)
- **`git status --porcelain` reports an untracked *directory*** — use `-uall`.
- **`realm-export.json` reaches FRESH STACKS ONLY.** `reconcile.sh` is the only seam to prod/UAT.
- **The Playwright suite is NOT UAT-only** — `e2e.yml` runs the full 7-service stack with a real
  Keycloak on every PR. ⚠ **But with `KEYCLOAK_ADMIN_ENABLED=false`**, so it never touches the
  ADR-0038 write path (§4.3). ⚠ **e2e specs ARE now typechecked** (`tsconfig.e2e.json`, 34 files).
- **Local `dotnet test` shows ~31 integration failures with Docker off** — Testcontainers, not a
  regression.
- **Prod and UAT differ on purpose.** Do not harmonise them.
- **7 stale branches exist** (`chore/design-update-round2`, `feat/audit-adr`, …) — all pre-date this
  work; none are from it.
