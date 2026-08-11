# RESUME — ADR-0038: guest invite, deploy plumbing

**Updated 2026-08-11 (second session).** Backend, invite UI **and role UI** are merged. This is the
single authoritative entry point; earlier ACMP resume files are superseded history.

---

## 0. Orient (2 minutes, do not skip)

```
server_info() · package_open("tamheed-package") · gate_run()
```

⚠ **If `package_open` fails on `.lock`**, check the PID properly before removing it — the lock holds
a bare PID and "is it alive?" **lies** under PID reuse. Confirm the process does **not exist**, or
that its identity and `StartTime` don't match, then delete `tamheed-package/data/.lock`.

Read **`SC-003`** and **`SC-004`** before designing anything. Both record where the ADR text and the
code legitimately diverged.

---

## 1. State

| | |
|---|---|
| `main` | `3bed1c4` · gates 7/7 · 127 evidenced verdicts |
| Production | **live on `e403e18`**, smoke 10/10, bundle verified |
| UAT | **stopped** (`i-07ac28ac2fedab921`) — start it from `cloud-operations.md` §1 |
| Merged | #232 Day 3 · #233 e2e hardening · #234 backend · #235 invite UI · **#236 role UI** |
| Open defects | `DEF-012` (package data) · `DEF-045` (e2e harness, fully classified) |

**Done:** `FR-156` invite (backend + UI) · `FR-157` role assignment (**backend + UI**) ·
`FR-158` roster shows `Invited`.

**Verdicts:** `AC-088` **Met** · `AC-089` **Met** · `AC-090` **Partial** · `AC-091`/`093` Partial ·
`AC-092` **Pending**.

⚠ **`AC-090` is blocked, not forgotten.** Its bar is behavioural — *"the subsequent request no longer
exercises the removed role"* — and `KeycloakAdmin:Enabled` is **false in dev, CI and the e2e stack**,
so `IIdentityProvider` is never registered and the endpoint **cannot execute anywhere today**. It
needs §5 **and** §6 below. The same blocker is the residual on `AC-088`.

---

## 2. ⚠ Two rules this session paid for repeatedly

**A. Check whether it is already built.** Three times a "new" thing already existed:
`MembershipStatus.Invited` + `SyncFromClaims`; the `invited` badge and its EN/AR keys; `/session`'s
full design reference. **Grep the domain enums, `i18n/locales/en.json`, and
`ACMP product context/*.dc.html` before designing.**

**B. An ADR citation in a test name is load-bearing, and no gate reads it.** `SC-004` exists because
`ADR-0038` silently contradicted `ADR-0015` §Q3 (*"ACMP does NOT integrate the Keycloak Admin API in
v1"*), and the only thing that caught it was a `describe` block string. **Before overriding any test
whose name cites an ADR or AC, read that row.** If the code is right, record a `scope-change` —
don't diverge quietly, and don't build the worse thing out of deference to the document.

---

## 3. ✅ Role-assignment UI — DONE (#236)

`RoleAssignmentPanel` on the user detail. Three decisions worth not re-deriving:

- **One `Select`, not a multi-select.** The API takes a collection, but ACMP caches exactly one
  `CommitteeRole` and `AC-091` forbids reading Keycloak at request time — the app *cannot* know a
  multi-role set, so a multi-select would prefill a partial one and drop what it can't see. The
  assignment **replaces** the person's roles, and the panel says so on screen.
- **The confirm gate fires on "the set being SENT is privileged"**, mirroring the server — not on
  "this person is gaining privilege". An Administrator moving to Chairman gains nothing and is still
  refused without the flag.
- **Nothing is pre-hidden.** Self-change and last-Administrator answer with the server's refusal.

### ⚠ Two defects it uncovered in the invite that shipped the day before

- **`DEF-046`** — `members.ts` sent its JSON body with **no `Content-Type`**, so minimal-API binding
  answered **415**. The invite in #235 *never worked*. **No layer could have caught it**: every
  backend test uses `PostAsJsonAsync`, which sets the header itself; the panel test mocks the hook
  away; the api-layer test asserted the **body** and never the headers. *An assertion on a request's
  body says nothing about whether the request is well-formed enough to be read.*
- **`DEF-047`** — the invite panel rendered **edge-to-edge** with its primary action styled as body
  text. A fully green suite described a broken-looking screen, because role/label queries resolve
  perfectly against unstyled markup. **Found by rendering the real components in a browser.**

## 4. `FR-159` / `AC-092` — guest invite

Expiry is stored **ACMP-side** and enforced per request; the Keycloak user is disabled at expiry as
defence in depth. `IIdentityProvider.DisableUserAsync` already exists. `/session` is built to
`ACMP Navigation & IA.dc.html` **lines 304–347** (`GUEST / PRESENTER SHELL`). See `DEC-037`.

## 5. Deploy plumbing

`KeycloakAdmin__*` through `gen-secrets.sh` (file-backed, ADR-0032), both `.env` examples,
`docker-compose.cloud.yml`, `09-put-env.sh`. Options are `ValidateOnStart`, so a half-configured
environment **stops the host at boot** — intended.

---

## 5b. `OQ-069` — an operator decision, not a code fix

`FR-156` and `FR-157` both say "As an Administrator **or Secretary**" and the server honours it, but
`App.tsx:100` gates `/admin` with `RequireRole ['administrator']`, so **a Secretary can reach
neither control**. Widening the route exposes templates, health, streams, jobs and notification
settings to Secretary and contradicts permission-matrix row 27 (SoD-5). Options: narrow the
requirements, move the affordances somewhere a Secretary can reach, or widen and accept the SoD
consequence. **Do not just widen it.**

## 6. ⚠ The obligation that is not optional

**Prove the minimum `realm-management` role set on UAT** — create-user, set-temporary-password,
assign/remove realm roles, disable, logout. A stub transport cannot answer it, and **`realm-admin`
is not the answer if a narrower grant is refused**; a refusal is a signal to read, not to widen.
Unchecked box on PR #234.

---

## 7. Gotchas that cost real time

- **New `.cs` files need a UTF-8 BOM** or `dotnet format --verify-no-changes` fails on `CHARSET`.
- `AddHttpClient<TClient, TImpl>` names the client after the **service** type — asking for the
  implementation name silently returns a default client with no `BaseAddress`.
- **`export MSYS_NO_PATHCONV=1` before any `aws` call from Git Bash.** An argument starting with `/`
  is rewritten to a Windows path and SSM answers `ParameterNotFound` — which looks **exactly** like a
  missing IAM permission. This nearly bought an unnecessary policy widening.
- **Never `sed` the SSM env payload.** MSYS `sed` rewrote all 36 line endings while changing 2. Edit
  in **binary** and assert the CR count is unchanged.
- **The deployable sha is not HEAD.** `ci.yml` `paths-ignore` skips `*.md`, `docs/`, `.claude/`,
  `tamheed-package/` — governance commits publish no images. Deploy the newest sha with ECR images.
- **Write the package only from `main`.** `tamheed-package/data` is git-tracked; commit immediately.
- **The e2e suite is hardened, and UAT is never reset (`DEC-039`).** Any new spec must be
  **page-aware and count-agnostic** — see `DEF-045` for the four shapes that break.
- **Prod and UAT differ on purpose.** Do not harmonise them.
- **A green suite is not a look.** Testing-library queries by role/label pass against completely
  unstyled markup. Any new screen gets **rendered in a browser** — `npx vite` + a throwaway entry
  that mounts the real components inside a `QueryClientProvider`, `?lang=ar` for RTL. That is how
  `DEF-047` was found and how the `.adm-detail-card { overflow: hidden }` clip on the role
  dropdown was caught **before** it shipped.
- **`.adm-detail-card` has no padding and clips its children.** Child blocks supply their own
  padding (`.adm-detail-form`), and anything that opens a popover needs `.adm-card-overflow`.
- **An `afterEach` that calls `i18n.changeLanguage` must `cleanup()` FIRST** — the file's `afterEach`
  runs before the setup file's auto-cleanup, so the language switch re-renders mounted components
  outside `act()` and every test in the file warns, attributed to whichever one was running.
