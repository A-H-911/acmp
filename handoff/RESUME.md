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
| `main` | `b3326e8` · gates **7/7** · **130 evidenced verdicts** |
| Verdicts | **79 Met · 14 Partial · 1 Pending** |
| Production | **live on `e403e18`**, always-on · `i-04d9717feea79204b` · smoke 10/10 |
| UAT | **stopped** · `i-07ac28ac2fedab921` — start from `deploy/runbooks/cloud-operations.md` §1 |
| Open defects | `DEF-012` `DEF-036` `DEF-038` `DEF-039` `DEF-041` `DEF-045` `DEF-050` (7 of 51) |
| Open questions | `OQ-069` `OQ-071` (2 of 72) — **both need you, not code** |

**ADR-0038 + ADR-0039 shipped this session** — #236 role UI · #237 deploy plumbing · #238 reconcile
guard · #239 per-request revalidation · #240 guest-expiry sweep.

`AC-088` `AC-089` `AC-090` `AC-091` **Met** · `AC-092` **Partial** (one half left, see §3).

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

## 3. ★ NEXT SLICE — `FR-159` / `AC-092`: the guest-invite writer + `/session`

**This is the only thing between `AC-092` and Met.** The enforcement is done; the *user-visible half*
is not.

### Already built — do NOT rebuild (rule D)

| Thing | Where |
|---|---|
| `AccessExpiresAt`, `SetAccessWindow(...)`, `HasExpired(now)` | `CommitteeMember` (migration shipped) |
| **Per-request refusal** of an expired member → `401 access_expired` | `PrincipalRevalidationMiddleware` + `PrincipalRevalidator` |
| **Hourly sweep** disabling past-window members locally **and** in Keycloak | `ExpireGuestAccessHandler`, registered `Cron.Hourly()` in `Acmp.Worker` |
| Keycloak account creation + `DisableUserAsync` | `IIdentityProvider` / `KeycloakAdminClient` |
| An invite that already creates at `CommitteeRole.Guest` | `InviteUserHandler` |

The expiry boundary is decided in **one** place (`CommitteeMember.HasExpired`, exclusive) and read by
three callers — the API, the sweep, and soon the banner. `DEC-037` requires banner and server to read
the same value; keep it that way structurally rather than by convention.

### What is missing

**1. The writer — nothing sets `AccessExpiresAt`.**

`FR-159`: *"As a Secretary, I want to invite a guest presenter **from the meeting screen** with access
that expires after the meeting."*

⚠ **The design question to settle first (ADR-0001):** the window comes from the meeting's
`ScheduledEnd`, which is **Meetings-owned**. Membership must not read Meetings' tables. Options:

- a cross-module contract in `Acmp.Shared.Contracts.Membership`/`Meetings` — the established pattern
  (`ICommitteeDirectory`, `ITopicScheduler`, and the new `IPrincipalRevalidator` all look like this);
- or the API endpoint orchestrates: query Meetings, then send the Membership command with an explicit
  `AccessExpiresAt`. Thinner, but puts a two-step in an endpoint layer whose stated rule is "no
  business logic, delegates to MediatR".

Prefer the contract. **It is a new architecture decision → raise an ADR row (Proposed) and stop for
approval**, per the standing DoD.

Also decide: does the window end *at* `ScheduledEnd`, or with a grace period? `DEC-037` says
"expires after the meeting" — the boundary is already exclusive, so `ScheduledEnd` is defensible, but
say so explicitly rather than leaving it implied.

Authorization: **Secretary** (`FR-159`), not the Administrator-or-Secretary pair `FR-156` uses.

**2. `/session` — the guest surface.**

Built to `ACMP product context/ACMP Navigation & IA.dc.html` **lines 304–347**
(`GUEST / PRESENTER SHELL`). **Read the `.dc.html` directly with file tools, not the design MCP**
(INV-014). `DEC-037` fixes the content: expiry banner, topic card (key, title, alt-language title,
summary), agenda-slot card (meeting name, MTG key, "Item 3 of 6", a 15-minute time box), and
"Materials for your slot" (deck + diagram, each openable). Copy is fixed too — *"Presenter access —
read-only, and expires after the meeting"* / *"Presenter · Read-only"*.

The banner **must read the same `AccessExpiresAt` the server enforces**. That is `AC-092`'s explicit
requirement and the reason the value is stored once.

Route: Guest **plus** Chairman/Secretary for preview, **enforced at the API and not only by the route
guard** (`DEC-037`). `navModel.ts` already sets `ACCESS.session = { guest: 'full' }`.

### Gates this slice must clear

- **Frontend per-file coverage ≥95%** — mutations without UI are unused exports and **fail CI**, so
  it must land whole.
- **i18n EN *and* AR together** — `check-i18n` compares **keys only**, so a missing value renders raw
  English and no gate catches it. Verify RTL in a browser (rule E).
- Every guard proven by **forcing its refusal**, never by asserting a handler was called.

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
