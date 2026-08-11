# RESUME — ADR-0038 frontend, guest invite, and the deploy plumbing

**Written 2026-08-11.** The backend is **MERGED** (PR #234, `main` `729da88`); everything below is what is
left. Read `SC-003` and `PE-248` in the package before starting — the design changed during
implementation, for good reasons, and the ADR text still carries the original.

---

## 0. Orient

```
server_info() · package_open("tamheed-package") · gate_run()
```

`ADR-0038` is **Approved**. `AC-088`/`089`/`090`/`091`/`093` are **Partial** (backend evidenced, UI outstanding — AV-132..136); `AC-092` is **Pending**. Prod is current on
`e403e18`; UAT is **stopped**.

---

## 1. State

| | |
|---|---|
| Backend | **merged** — PR #234, `main` `729da88`, CI green |
| Done | `FR-156` invite · `FR-157` role assignment + 4 guards · `FR-158` roster shows `Invited` |
| Evidence | 1738 tests, per-file coverage **99.67%**, `dotnet format` clean |
| Not done | **SPA**, `FR-159` guest invite, compose/secret plumbing |

---

## 2. ⚠ Read this before touching the SPA

**Three times today the thing was already built.** Check before writing:

- `MembershipStatus.Invited` already existed, and `SyncFromClaims` already flipped a pre-registered
  record to Active on first login. `SC-003` records why the ADR's separate "invite record" was
  dropped.
- **The `invited` badge already renders** — `STATUS_TONE.Invited = 'info'` in
  `UsersMembership.tsx:24`, and both `admin.status.invited` keys exist (EN `"Invited"`, AR
  `"مدعو"`). `DEF-038`'s visible half is closed by the backend filter change alone.
- `ACMP Administration.dc.html` **§(8) USER DETAIL + INVITE** is the design reference: two fields
  (*Email address*, *Full name*), primary action *Invite user / دعوة مستخدم*, and the `uStatus`
  vocabulary. **Read it directly (INV-014); do not compose.**

---

## 3. The SPA work

**It must land whole.** `api/members.ts` mutations without the dialog are unused exports, and the
frontend gate is **per-file ≥95%** — dead code fails CI. Mutations + dialog + i18n + tests in one
commit.

1. **`api/members.ts`** — `useInviteUser()` → `POST /members/invite`, `useAssignRoles()` →
   `PUT /members/{publicId}/roles` with `{ roles, confirmedPrivileged }`. Invalidate `['members']`.
   ⚠ Its header comment currently says *"Read-only in P3/P4 UI … there is no create/edit-role
   mutation here"* — that becomes false; update it.
2. **Invite dialog** to §(8). The temporary password comes back in the response and is shown
   **once**: copy-to-clipboard, no re-fetch, and it must never be logged or written anywhere.
3. **Role assignment** — the privileged-grant confirmation must send `confirmedPrivileged`. The
   server refuses without it, so the dialog is a real gate, not decoration.
4. **`admin.kc.note` banner is now partly false** — it tells the reader identities and roles are
   managed in Keycloak. Reword to match what the app can now do.
5. **i18n EN + AR for every new string.** `check-i18n` compares **keys only**, so a missing enum
   value renders raw English and no gate catches it.

## 4. `FR-159` / `AC-092` — guest invite

Guest expiry is stored **ACMP-side** and enforced per request; the Keycloak user is disabled at
expiry as defence in depth. `IIdentityProvider.DisableUserAsync` already exists.
`ADR-0038` and `DEC-037` carry the rest; `/session` is built to
`ACMP Navigation & IA.dc.html` **lines 304–347**.

## 5. Deploy plumbing

`KeycloakAdmin__*` through `gen-secrets.sh` (file-backed, ADR-0032), both `.env` examples,
`docker-compose.cloud.yml`, and `09-put-env.sh`. Options are `ValidateOnStart`, so a half-configured
environment **stops the host at boot** — that is intended.

---

## 6. ⚠ The obligation that is not optional

**`ADR-0038` requires the minimum `realm-management` role set to be PROVEN on UAT** against a real
realm — create-user, set-temporary-password, assign/remove realm roles, disable, logout.

A stub transport cannot answer it, and **`realm-admin` is not the answer if a narrower grant is
refused** — a refusal is a signal to read, not to widen. This is an unchecked box on PR #234.

---

## 7. Gotchas that cost time today

- **New `.cs` files need a UTF-8 BOM** or `dotnet format --verify-no-changes` fails on `CHARSET`.
- `AddHttpClient<TClient, TImpl>` names the client after the **service** type — asking for the
  implementation name silently returns a default client with no `BaseAddress`.
- **From Git Bash on Windows, `export MSYS_NO_PATHCONV=1`** before any `aws` call: an argument
  starting with `/` is rewritten to a Windows path, and SSM answers `ParameterNotFound`, which looks
  exactly like a missing IAM permission.
- **Never `sed` the SSM env payload** — MSYS `sed` rewrites all 36 line endings while changing 2.
  Edit in binary and assert the CR count is unchanged.
- The e2e suite is now hardened against an accumulated database (`DEC-039`); **any new spec must be
  page-aware and count-agnostic** — see `DEF-045`.
