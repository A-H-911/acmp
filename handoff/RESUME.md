# RESUME — ACMP

**The single entry point. Rewritten 2026-08-12 at session end.** Every `handoff/RESUME-*.md` and
every `handoff/prm-*.md` older than this file is ⛔ superseded history. This file is durably named so
it never needs renaming again. The paste-able kickoff prompt is **`handoff/prm-next.md`**.

---

## 0. Orient (2 minutes, do not skip)

```
server_info() · package_open("tamheed-package") · gate_run()
```

⚠ **If `package_open` fails on `.lock`**, check the PID *properly* before removing it — the lock holds
a bare PID and "is it alive?" **lies** under PID reuse. Confirm the process does not exist, or that
its identity and `StartTime` do not match the lock's mtime, then delete
`tamheed-package/data/.lock`. It went stale twice in one session; never remove it reflexively.

Then read **§2**. Then read `SC-003` … `SC-008` — six records of where an approved document and the
code legitimately diverged, and *why the code was right*. Two of them (`SC-007`, `SC-008`) were
written in the last session and will bite the next person who "fixes" what looks wrong.

---

## 1. State

| | |
|---|---|
| `main` | green · gates **7/7** · **132 evidenced verdicts / 12 narrated** |
| Verdicts | **80 Met · 12 Partial · 1 Pending** over 93 ACs |
| ⚠ Newest sha **with ECR images** | **`bcd8e96`** — later commits are `.md` / `tamheed-package/` only and publish nothing |
| Production | **live**, always-on · `i-04d9717feea79204b` · https://acmp.anas7ammo.dev |
| UAT | **stopped when idle** · `i-07ac28ac2fedab921` — start from `deploy/runbooks/cloud-operations.md` §1 |
| Open defects | `DEF-012` `DEF-038` `DEF-039` `DEF-041` `DEF-045` `DEF-053` `DEF-054` (7 of 54) |
| Open questions | `OQ-074` only (everything else is `Deferred` by design) |

**Phases `P1`–`P19` are COMPLETE.** `P14` (Tarseem diagrams) is deferred indefinitely (`DEC-028`) and
is off the ladder — it correctly has zero progress entries. The remaining work is **not a new slice**;
it is the list in §4.

---

## 2. ⚠ Rules this project has paid for. Read them before you write code.

**A. Read the implementation before calling something a defect.** Now **seven** instances; none was
caught by a gate. It has also made defects *smaller* (`DEF-051`'s cloud half was always guarded) and,
last session, made one **disappear**: `DW-025` was written on the premise that rescheduling a meeting
strands a guest's window — **ACMP has no reschedule at all**, which three checks established in two
minutes and which would have been "implemented" otherwise.

**B. An ADR/AC citation in a test name is load-bearing, and no gate reads it** (`SC-004`, `SC-007`).
Before overriding a test whose name or `InlineData` cites an ADR or AC, read that row. `SC-007` exists
because an `[InlineData("Guest")]` citing `AC-059` caught a narrowing no gate could see. Supersede
**narrowly** and record it.

**C. When an ADR names a specific seam, check the harness can reach it before approving** (`SC-005`).
Run last session before `ADR-0040` was proposed, and it paid twice: the API harness registers no
`IIdentityProvider` (a test asserts that absence *deliberately*), and the opt-in fix then broke 22
Webex tests because they take the factory as an xUnit **class fixture**, which needs a parameterless
constructor.

**D. Check whether it is already built.** Grep the domain enums, `i18n/locales/en.json`, and
`ACMP product context/*.dc.html` first.

**E. A green suite is not a look.** `DEF-047` shipped a visibly broken panel with 8 tests green.
Render new screens in a browser, **in both directions**. Last session this caught two things no test
could: a `Dialog` focus-trap bug that swallowed every keystroke after the first, and a shared
component whose styles lived in a stylesheet only `/admin` loads — `DEF-047` again in disguise. ⚠ The
throwaway harness must import **only the stylesheets the real route imports**, or it lies to you.

**F. Prove, don't assume.** `OQ-070`'s answer (`manage-users` **alone**) contradicted my own written
candidate (`+ view-realm`), and no gate would have caught the wider grant. CI now proves it on every
run (§3).

**G. Verify the deployed state, not the file that describes it.** `DEF-050` said exposure was
"probably nil" — inferred from `.env.example`. Reading SSM showed the truth *and* that the defect was
narrower than recorded. A control that DETECTS but does not TELL is this project's most repeated bug
class (`DEF-023`, `DEF-031`, `DEF-051`, now `DEF-054`).

---

## 3. ✅ What shipped last session — read before touching any of it

### `FR-159` / `AC-092` — guest presenters. **Met** (`AV-144`).

`#241` the writer + guest surface, `#242` `/session`. `ADR-0040` approved as `DEC-040`.

- **The invite is a MEETINGS use case over ONE Membership write port** (`IGuestProvisioner`). It reads
  `ScheduledEnd` from its own aggregate, so the boundary is crossed exactly once. The mirror shape
  needs two. ⚠ **`ADR-0021` had already fixed this pattern** (primitive port in `Shared.Contracts`,
  implemented in the owning module's Infrastructure, unauthorized at the port, two transactions
  accepted) and it forbids cross-module command sends. **Read `ADR-0021` before designing any new
  cross-module seam** — it turned an open architecture question into a lookup.
- **The window is `ScheduledEnd + 24h`** (`GuestAccess.Grace`). The ADR recommended *no* grace; the
  operator widened it because refusal is per-request and immediate, so no grace 401s a presenter
  **mid-presentation** when a meeting overruns.
- **`DEF-052`: there was no read-side role gate anywhere.** 14 content groups were
  `RequireAuthorization()` with no policy and every named policy is a WRITE capability. Latent only
  because no Guest had ever existed. Fixed **in the same merge** by `GuestSurfaceMiddleware` —
  **deny-by-default, not a policy per group**, because an opt-in list silently exempts every route
  added later. Allowlist = `POST /api/members/me`, `/api/session`, `/api/notifications`, GET-only
  `/api/meetings`, which is `navModel.ts`'s own ACCESS map.
- **`SC-006`** `/session` omits the design's alt-language topic title (no bilingual field exists in the
  domain). **`SC-007`** `AC-059` narrowed to exclude Guest.

### The four `DEC-041` items — all built

| | |
|---|---|
| `DEF-050` `#243` | Webex credentials → mounted secrets. **Verified first**: prod and UAT carry one Webex line, `WEBEX_ENABLED=false`, zero credentials — nothing was exposed, nothing rotated. Narrower than recorded: cloud never used env delivery. |
| `DW-025` `#244` | Guest windows follow the meeting: **cancel / item removal / slot reassignment close the window**. Reschedule does not exist. |
| `OQ-071` `#245` | The minimum-grant proof is a **CI job on every run** — leg 1 sufficient, leg 2 strips the grant and **requires the 403**. |
| `OQ-069` `#246` | Roster + invite + role assignment moved out of Administration into **`/members`** (Administrator **and** Secretary). `SC-008` records both `INV-014` divergences. |

**Two were built against my recommendation** (`OQ-069`, `DW-025`). The reasoning that changed them is
in `DEC-041` — read it before re-opening either.

⚠ **`IGuestWindowWriter` is deliberately separate from `IGuestProvisioner`.** The provisioner needs the
identity provider and is registered only when configured; folding the window writer in would make
**cancelling a meeting fail** wherever in-app user management is off — which is every environment
today.

⚠ **A new advisory can turn `main` red with no code change.** `GHSA-q939-rpr3-3284` (SSH.NET, HIGH)
landed mid-session and blocked every merge; found because a branch touching only a workflow file
failed the *backend* gate. Pinned in `Directory.Build.props`, scoped to `Acmp.Integration.Tests`.

---

## 4. Everything left, in order

**1. ★ Deploy with `KEYCLOAK_ADMIN_ENABLED=true`.** The single highest-value action, and it is
**yours, not code**. Invite (`FR-156`), role assignment (`FR-157`) **and the guest-presenter invite
(`FR-159`)** are all merged, tested and **unreachable**: `IIdentityProvider` is registered only when
configured, so those endpoints fail at composition in every environment. Enabling is **one variable**
— the secret is always written and `reconcile.sh` converges the client and its grant on every boot;
`09-put-env.sh` refuses `ENABLED=true` with a placeholder secret. This converts `AC-088`/`AC-091`'s
stated residual into an observation and unlocks `AC-090`'s behavioural leg.

**2. `DEF-053` — the `/session` route guard.** Small and known: `DEC-037` says "enforced at the API
**and not only by the route guard**". The API half is done and tested (403 for five roles); the route
half was not built, so a non-guest sees the "you are not presenting" empty state instead of being
turned away. Add `RequireRole roles={['guest','chairman','secretary']}` in `App.tsx` exactly as
`/members` does, plus a route test; consider distinguishing 403 from 204 in `SessionPage`.

**3. `DEF-054` — `up.sh` cannot catch a failed realm reconcile.** Measured: `compose up --wait`
returns while a one-shot is still mid-flight. CI and the cloud deploy are covered; **dev and on-prem
prod are not**. Same shape as `DEF-023`/`DEF-051` — third occurrence of "detects but does not tell".

**4. `AC-093` (Partial) — read the audit content back.** The rows exist and are asserted *as rows*;
what is missing is a test that reads before/after **out of the hash chain** for a governed identity
change.

**5. `AC-004` (the only Pending) — session idle timeout.** No evidence recorded at all. Decide whether
Keycloak's timeout is the control and prove it, or record why it cannot be driven.

**6. The other 11 Partials** — `AC-003` `AC-005` `AC-006` `AC-007` `AC-009` `AC-010` `AC-011` `AC-033`
`AC-034` `AC-041` `AC-048`. Nearly all are Partial for the *same* reason: proven by unit/handler tests
with **no live or E2E leg**. `AC-041` (Arabic visual regression) rests on a manual Playwright render.
Treat this as one campaign, not eleven tasks.

**7. `DEF-038` — the roster lists only members who have already logged in** (1 of 26 at observation).
This matters more now that `/members` is the invite surface: an invited person is `Invited`, not
`Active`, until first login.

**8. `Streams.NameAr` on prod** — in scope for Day 3, not done. Real table is
`membership.streams`.`name_ar`; the C# names do not exist in SQL and every module owns a schema.

**9. `AC-085` leg 1** — an observation wait, not work. When spend crosses **$2.30**, run
`deploy/scripts/check-budget-notification.sh` and `audit_record` **the body** (a count cannot
discriminate on a shared topic — `AV-118`).

**10. `OQ-074`** — `DEC-037` never said *whose* view Chairman/Secretary "preview". Shipped as **their
own** slot. A chosen presenter's view would be a second authorization path over somebody else's
content.

**11. Remaining defects** — `DEF-039` (System Health renders a MinIO tile; the cloud moved to S3),
`DEF-041` (voting-eligibility toggle absent from the accessibility tree), `DEF-012` (package-data
residue in `v_backlog`), `DEF-045` (classified: harness causes, no product defect).

**12. `OQ-062` is stricter in code than in the decision** — a *permanent* UAT Webex ban vs "off
**until** a UAT space exists", so the exit condition can never be met. Worth reconciling.

**Not on this list, deliberately:** the ~45 `Deferred` open questions and the `DW-0xx` feature backlog
are parked by design. If a reschedule capability is ever built, it **must** call `IGuestWindowWriter`
with the new `ScheduledEnd + GuestAccess.Grace`.

---

## 5. Gotchas that cost real time

- **The deployable sha is NOT HEAD** — `ci.yml` `paths-ignore` skips `*.md`, `docs/`, `.claude/`,
  `tamheed-package/`, so governance and handoff commits publish **no images**.
- **Deploy as `acmp-admin`, never root.** Root bypasses the budget IAM-deny brake (`AC-085` leg 5);
  `[default]` in `~/.aws/config` **is** root and its session expires.
- **Use PowerShell for any `aws` call with a `/`-leading argument** — Git Bash rewrites `/acmp/prod/env`
  into `C:/Program Files/Git/acmp/...` and SSM answers `ParameterNotFound`, which looks exactly like a
  missing IAM permission while `describe-parameters` happily lists the same names. (`MSYS_NO_PATHCONV=1`
  also works.)
- **Write the Tamheed package only from `main`** — `tamheed-package/data` is git-tracked, so writing
  from a feature branch fragments the record. `defect.fixed_by` is a **FOREIGN KEY**: put PR refs in
  `custom_attributes` or the whole batch rolls back. `G-COMPLETE` also rejects `{{ }}` placeholders.
- **A squash-merge folds branch-local governance commits into the merge commit** — local `main` then
  "diverges" and a plain `git pull` conflicts on `data/*.jsonl`. Verify the rows survived
  (`git show origin/main:tamheed-package/data/...`), then `git checkout -B main origin/main`.
- **A compose `secrets:` entry whose file is MISSING fails the WHOLE stack** — so any secret you mount
  must be written **unconditionally** by `gen-secrets`.
- **New `.cs` files need a UTF-8 BOM**, and `.cs` must be **LF** — editing via a Python text-mode
  rewrite silently converts to CRLF and `dotnet format --verify-no-changes` fails on `ENDOFLINE`.
- **Never run `gen-secrets.sh` against the repo to test it** — `SECRETS_DIR` is hardcoded and it will
  clobber the operator's live dev secrets. Copy the tree.
- **`git status --porcelain` reports an untracked *directory*, not the files inside** — use `-uall`.
- **`realm-export.json` reaches FRESH STACKS ONLY** — Keycloak never re-imports an existing realm.
  `reconcile.sh` is the only seam that reaches prod/UAT.
- **`.adm-detail-card` has no padding and clips its children**; anything opening a popover needs
  `.adm-card-overflow`.
- **`userEvent.setup()` installs its own clipboard stub** — define a clipboard spy *after* it.
- **The Playwright E2E suite is NOT UAT-only** — `e2e.yml` runs the full 7-service stack with a real
  Keycloak on every PR. UAT adds *deployed-topology* validation, not application logic.
- **Local `dotnet test` shows ~31 integration failures with Docker off** — Testcontainers, not a
  regression. Verify the message rather than assuming either way.
- **Prod and UAT differ on purpose.** Do not harmonise them.
