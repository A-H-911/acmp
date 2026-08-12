# RESUME — ACMP

**The single entry point. Written 2026-08-12 at session end.** Every earlier `handoff/RESUME-*.md`
is ⛔ superseded history. This file is durably named so it never needs renaming again.

---

## 0. Orient (2 minutes, do not skip)

```
server_info() · package_open("tamheed-package") · gate_run()
```

⚠ **If `package_open` fails on `.lock`**, check the PID *properly* before removing it — the lock
holds a bare PID and "is it alive?" **lies** under PID reuse. Confirm the process does not exist, or
that its identity and `StartTime` don't match the lock's mtime, then delete
`tamheed-package/data/.lock`. Never remove it reflexively.

Read **`SC-003`**, **`SC-004`** and **`SC-005`** before designing anything. All three record where an
approved document and the code legitimately diverged, and *why the code was right*.

---

## 1. State

| | |
|---|---|
| `main` | `95c4ca3` · gates **7/7** · **132 evidenced verdicts** |
| Verdicts | `AC-088`–`AC-092` **all Met** |
| Production | **live on `e403e18`**, always-on · `i-04d9717feea79204b` · smoke 10/10 |
| UAT | **stopped** · `i-07ac28ac2fedab921` — start from `deploy/runbooks/cloud-operations.md` §1 |
| Open defects | `DEF-012` `DEF-036` `DEF-038` `DEF-039` `DEF-041` `DEF-045` `DEF-050` (7 of 51) |
| Open questions | `OQ-069` `OQ-071` `OQ-074` — **all three need you, not code** |

**ADR-0038 + ADR-0039 + ADR-0040 shipped** — #236 role UI · #237 deploy plumbing · #238 reconcile
guard · #239 per-request revalidation · #240 guest-expiry sweep · **#241 guest-invite writer + guest
surface** · **#242 `/session`**.

`AC-088` `AC-089` `AC-090` `AC-091` `AC-092` **all Met**. `FR-159` is complete — see §3 for what
it decided and what it deliberately left open. **The next work is operator action, not code (§4).**

---

## 2. ⚠ Rules this project has paid for. Read them before you write code.

**A. Read the implementation before calling something a defect.** Six times in one session I asserted
a defect from a pattern-match and was wrong; **none was caught by a gate**, and four would have
shipped a change that broke deliberate behaviour. It has since also made a defect *smaller*
(`DEF-051`'s cloud half was always guarded — I grepped compose and never read the deploy script).

**B. An ADR/AC citation in a test name is load-bearing, and no gate reads it** (`SC-004`). Before
overriding a test whose name cites an ADR or AC, read that row. Supersede **narrowly** and record it.

**C. When an ADR names a specific seam, check the test harness can reach it before approving**
(`SC-005`). `ADR-0039` specified `JwtBearerEvents.OnTokenValidated`; it worked — then the test showed
the API host authenticates with a **non-JWT scheme**, so that seam is unreachable from any API test,
making the approved mechanism only *unit*-provable: exactly the evidence `AV-134` refused.

**D. Check whether it is already built.** Three times in one session the "new" thing existed already.
Grep the domain enums, `src/Acmp.Web/src/i18n/locales/en.json`, and `ACMP product context/*.dc.html`.

**E. A green suite is not a look.** Testing-library queries pass against completely unstyled markup —
`DEF-047` shipped a visibly broken panel with 8 tests green. Render new screens in a browser.

**F. Prove, don't assume.** `OQ-070`'s answer (`manage-users` **alone**) contradicted my own written
candidate (`+ view-realm`), and no gate would have caught the wider grant.

---

## 3. ✅ `FR-159` / `AC-092` — DONE. Read this before touching guest access again.

**Complete: #241 (writer + guest surface) and #242 (`/session`). `AC-092` is Met (`AV-144`).**
`ADR-0040` was raised, approved in full as `DEC-040`, and implemented.

### The three decisions, and the one the operator changed

1. **The invite is a MEETINGS use case over ONE Membership write port** (`IGuestProvisioner`).
   The window comes from `Meeting.ScheduledEnd` and the slot is an `AgendaItem` — both Meetings-owned
   — so the handler reads its own aggregate and crosses the boundary exactly once. The mirror shape
   (Membership owning it) needs **two** crossings. ⚠ `ADR-0021` had **already** fixed this pattern
   (primitive port in `Shared.Contracts`, implemented in the owning module's Infrastructure,
   unauthorized at the port, two transactions accepted) and it forbids cross-module command sends.
   **Read `ADR-0021` before designing any new seam** — it turned an open question into a lookup.
2. **The window is `ScheduledEnd + 24h`** (`GuestAccess.Grace`, a named constant).
   The ADR recommended *no* grace; the operator widened it after the cost was stated plainly:
   refusal is per-request and **immediate**, so no grace hands a presenter a 401 **in the middle of
   presenting** when a meeting overruns. Changing the duration is one constant.
3. **A Guest reaches the guest surface and nothing else** — `GuestSurfaceMiddleware`, see below.

### ⚠ `DEF-052` — the finding that mattered most

**There was no read-side role gate anywhere in the API.** All 14 content groups are
`.MapGroup(...).RequireAuthorization()` with **no policy**, and every named policy in
`AuthorizationRegistration.Matrix` is a *write* capability. Any authenticated principal could read the
entire governance record. Harmless while every principal was a committee member — and **the FR-159
writer creates the first external one**, so the merge that added the writer is the merge that would
have opened the record to an outsider. It shipped in the **same PR**, not a follow-up.

Enforced in **one deny-by-default middleware**, not a policy per group: an opt-in list protects only
the endpoints somebody remembered to decorate, so every route added later is open — the `SC-005`
shape exactly. The allowlist is `POST /api/members/me`, `/api/session`, `/api/notifications`, and
**GET-only** `/api/meetings`, which is `navModel.ts`'s own `ACCESS` map, not a new judgement.

### Scope changes recorded (read them before "fixing" what looks wrong)

- **`SC-006`** — `/session` omits the design's alt-language topic title. `Topic` has a single `Title`
  and no bilingual field anywhere; the reference asks for data the system has never captured.
- **`SC-007`** — `AC-059`'s "readable by any authenticated role" now **excludes Guest**. Caught by an
  existing `[InlineData("Guest")]` citing the AC. The directory is 26 people's names and emails.

### Two bugs no test could see, both found by opening the screen

- **`Dialog` re-ran its focus trap whenever `onClose` changed identity** — an inline arrow, so every
  render — and its cleanup restores focus. The **second keystroke** in any dialog text field went
  elsewhere. Fixed **in `Dialog`** via a ref; every future caller with a field would have hit it.
- **The credential block's styles lived in `administration.css`**, which only `/admin` imports, so the
  same markup in a meetings dialog rendered **unstyled with every test green** — `DEF-047` again. A
  shared component now owns its stylesheet. ⚠ **Grep which routes import a stylesheet before
  borrowing its classes.**

### Still open, deliberately

- **`OQ-074`** — `DEC-037` says Chairman/Secretary may "preview" `/session` but not *whose* view.
  Shipped as **their own** slot (what the caller-scoped read model gives for free). A chosen
  presenter's view would be a second authorization path over somebody else's content.
- **`DW-025`** — rescheduling a meeting does not move an already-written window.

---

## 4. Everything else remaining, in order

**1. Deploy with `KEYCLOAK_ADMIN_ENABLED=true`.** Invite and role assignment are merged, tested and
**unreachable**: `IIdentityProvider` is registered only when configured, so both endpoints fail at
composition in every environment. This is what converts `AC-088`/`AC-091`'s stated residual into an
observation. Enabling is **one variable** — the secret is always written and `reconcile.sh` keeps the
client and its grant converged on every boot. `09-put-env.sh` refuses `ENABLED=true` with a
placeholder secret before it reaches a box.

**2. `OQ-071`'s automated grant test.** You chose *"both, UAT first"*; UAT is done (`OQ-070`), the
automated half is owed. Wrap `scripts/probe-keycloak-grant.mjs` in CI so a **narrower** grant is
proven *refused* — today's CI check only proves the configured grant is *applied*.

**3. `DEF-051`'s remaining half — `up.sh`.** Measured, not assumed: `docker compose up --wait`
**returns while a one-shot is still running**, so dev **and on-prem prod** cannot catch a failed
realm reconcile, where the failure reproduces `DEF-023` exactly (nobody can log in, every health
check green). The heavier fix — `depends_on: keycloak-config { condition:
service_completed_successfully }` on `api`/`worker` — is **your availability call**: it turns a
transient Keycloak hiccup into a refusal to start.

**4. `OQ-069` — an operator decision, not a code fix.** `FR-156`/`FR-157` say "Administrator **or
Secretary**" and the server honours it, but `App.tsx:100` gates `/admin` with `RequireRole
['administrator']`, so a Secretary reaches neither control. **Do not just widen the route** — it
exposes templates, health, streams, jobs and notification settings, contradicting permission-matrix
row 27 (SoD-5). Options: narrow the requirements, move the affordances, or widen and accept the SoD
consequence.

**5. `DEF-050` — the Webex secrets.** Delivered as plain compose `environment:` variables while the
five files `gen-secrets` writes for them are **mounted by nothing** — the channel `ADR-0032` exists
to avoid. Found by reading it *as the precedent for ADR-0038's secret* and deliberately not copied.
⚠ **Deployed exposure is unverified**: the real env comes from SSM, not the examples, so severity may
be higher than recorded. Check before deciding.

**6. `AC-093`** — Partial only because the hash-chained audit **content** has not been read back for
the invite/role actions. Needs an integration test that reads the row, not another emission assert.

**7. `Streams.NameAr` on prod** — was in scope for Day 3 and is not done. Real table is
**`membership.streams`.`name_ar`** (every module owns a schema; the C# names don't exist in SQL).

**8. `AC-085` leg 1** — an observation wait, not work. When spend crosses **$2.30**, run
`deploy/scripts/check-budget-notification.sh` and `audit_record` the printed body.

**9. Older Partials** — `AC-003/004/005/006/007/009/010/011/033/034/041/048` predate this work.

---

## 5. Gotchas that cost real time

- **The deployable sha is NOT HEAD** — `ci.yml` `paths-ignore` skips `*.md`, `docs/`, `.claude/`,
  `tamheed-package/`, so governance commits publish **no images**. Deploy the newest sha with ECR
  images.
- **Deploy as `acmp-admin`, never root.** Root bypasses the budget IAM-deny brake (AC-085 leg 5);
  `[default]` in `~/.aws/config` **is** root and its session expires.
- **`export MSYS_NO_PATHCONV=1` before any `aws` call from Git Bash** — an argument starting with `/`
  is rewritten to a Windows path and SSM answers `ParameterNotFound`, which looks exactly like a
  missing IAM permission.
- **Write the Tamheed package only from `main`** — `tamheed-package/data` is git-tracked, so writing
  from a feature branch fragments the record. `defect.fixed_by` is a **FOREIGN KEY**: put PR refs in
  `custom_attributes` or the whole batch rolls back.
- **New `.cs` files need a UTF-8 BOM** or `dotnet format --verify-no-changes` fails on `CHARSET`.
- **`git status --porcelain` reports an untracked *directory*, not the files inside it** — use
  `-uall` when sweeping new files, or a new file in a new folder is silently skipped.
- **`realm-export.json` reaches FRESH STACKS ONLY** — Keycloak never re-imports an existing realm.
  `reconcile.sh` is the only seam that reaches prod/UAT. Third occurrence of that bug class.
- **`.adm-detail-card` has no padding and clips its children** — child blocks supply their own
  (`.adm-detail-form`), and anything opening a popover needs `.adm-card-overflow`.
- **An `afterEach` calling `i18n.changeLanguage` must `cleanup()` FIRST**, or every test in the file
  emits an act() warning attributed to whichever test was running.
- **The Playwright E2E suite is NOT UAT-only** — `e2e.yml` runs the full 7-service stack with a real
  Keycloak on every PR. UAT adds *deployed-topology* validation, not application logic.
- **Local `dotnet test` shows ~31 integration failures with Docker off** — Testcontainers, not a
  regression. Verify the message rather than assuming either way.
- **Prod and UAT differ on purpose.** Do not harmonise them.
