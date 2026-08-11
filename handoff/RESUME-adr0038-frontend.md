# RESUME — ADR-0038: role UI, guest invite, deploy plumbing

**Rewritten 2026-08-11 at session end.** Backend **and** the invite UI are merged. This is the
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
| `main` | `5edd633` · gates 7/7 · 124 evidenced verdicts |
| Production | **live on `e403e18`**, smoke 10/10, bundle verified |
| UAT | **stopped** (`i-07ac28ac2fedab921`) — start it from `cloud-operations.md` §1 |
| Merged today | #232 Day 3 · #233 e2e hardening · #234 ADR-0038 backend · #235 invite UI |
| Open defects | `DEF-012` (package data) · `DEF-045` (e2e harness, fully classified) |

**Done:** `FR-156` invite (backend **+ UI**) · `FR-157` role assignment (**backend only**) ·
`FR-158` roster shows `Invited`.

**Verdicts:** `AC-088`/`089`/`090`/`091`/`093` **Partial** (`AV-132`…`136`) — backend evidenced, UI
outstanding. `AC-092` **Pending**.

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

## 3. Next: role-assignment UI (`FR-157` / `AC-089`, `AC-090`)

The backend is merged and tested. The UI is all that's missing.

- `useAssignRoles()` → `PUT /members/{publicId}/roles`, body `{ roles, confirmedPrivileged }`,
  invalidate `['members']`. Follow `useInviteUser()` in `api/members.ts`.
- **Granting Administrator or Chairman must send `confirmedPrivileged`.** The server refuses without
  it, so the confirmation is a real gate — build it as one, not as a cosmetic dialog.
- The server also refuses self-role-change and removing the last Administrator. **Surface those
  refusals as messages**; do not pre-hide the control and call that the rule.
- **`admin.kc.note` banner is now partly false** — it still tells the reader roles are managed in
  Keycloak. Reword it.
- **Must land whole**: mutations without UI are unused exports and the frontend gate is per-file
  **≥95%** — dead code fails CI.
- **i18n EN + AR together.** `check-i18n` compares **keys only**, so a missing value renders raw
  English and no gate catches it.

## 4. `FR-159` / `AC-092` — guest invite

Expiry is stored **ACMP-side** and enforced per request; the Keycloak user is disabled at expiry as
defence in depth. `IIdentityProvider.DisableUserAsync` already exists. `/session` is built to
`ACMP Navigation & IA.dc.html` **lines 304–347** (`GUEST / PRESENTER SHELL`). See `DEC-037`.

## 5. Deploy plumbing

`KeycloakAdmin__*` through `gen-secrets.sh` (file-backed, ADR-0032), both `.env` examples,
`docker-compose.cloud.yml`, `09-put-env.sh`. Options are `ValidateOnStart`, so a half-configured
environment **stops the host at boot** — intended.

---

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
