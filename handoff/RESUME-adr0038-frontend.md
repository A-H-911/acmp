> ⛔ **SUPERSEDED — do not follow this file.** The single entry point is
> [`handoff/RESUME.md`](RESUME.md) (2026-08-12). Kept for the reasoning it records, not for
> its instructions or its state table, both of which are stale.

# RESUME — ADR-0038: guest invite, deploy plumbing

**Updated 2026-08-12.** Backend, invite UI, role UI **and the deploy plumbing** are merged. This is
the single authoritative entry point; earlier ACMP resume files are superseded history.

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
| `main` | `e573f59` · gates 7/7 · 129 evidenced verdicts |
| Production | **live on `e403e18`**, smoke 10/10, bundle verified |
| UAT | **stopped** (`i-07ac28ac2fedab921`) — start it from `cloud-operations.md` §1 |
| Merged | #234 backend · #235 invite UI · **#236 role UI · #237 deploy plumbing · #238 reconcile guard** |
| Open defects | `DEF-012` (package data) · `DEF-045` (e2e harness) · `DEF-050` (Webex secrets via `environment:`) |
| Open decisions | `OQ-069` (Secretary can't reach `/admin`) · `OQ-071` (automated grant test still owed) |

**Done:** `FR-156` invite (backend + UI) · `FR-157` role assignment (**backend + UI**) ·
`FR-158` roster shows `Invited`.

**Verdicts:** `AC-088`/`089`/**`090`** **Met** · `AC-091`/`092`/`093` **Partial**.

✅ **`AC-090` is MET, and literally so** (#239). It was not merely unevidenced — it was
**unsatisfiable by construction**: authorization is token-driven, so a removed role survived a full
access-token lifetime (**300s, measured**) and a forced sign-out cannot revoke a token in flight.
`ADR-0039` fixed that. Evidence is a **refused request**, not a call-count.

⚠ **The AC's own text still contains a wrong number** — it contrasts against a "60-minute idle
timeout"; the realm's `ssoSessionIdleTimeout` is **1800 (30 min)**. Harmless now that the guarantee
no longer depends on it, but don't reason from it.

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

## 4. `FR-159` / `AC-092` — guest invite (HALF DONE)

✅ **Enforcement done.** `ADR-0039`'s per-request revalidation refuses an expired member on their
**next request** (#239), and the hourly sweep disables them in Keycloak too (#240). The expiry
boundary lives in ONE place (`CommitteeMember.HasExpired`), so the API, the sweep and the banner
cannot disagree — `DEC-037` requires exactly that.

⏳ **Remaining, and it is the user-visible half:**

1. **Nothing sets `AccessExpiresAt`.** The guest invite from the meeting screen is the writer. It
   needs the meeting's `ScheduledEnd`, which is **Meetings-owned** — so it wants a cross-module
   contract (ADR-0001), *not* Membership reading Meetings' tables. `InviteUserCommand` already
   creates at `CommitteeRole.Guest`; this is a sibling command with a window, Secretary-authorized.
2. **`/session`** built to `ACMP Navigation & IA.dc.html` **lines 304–347** (`GUEST / PRESENTER
   SHELL`) — expiry banner, topic card, agenda-slot card, "Materials for your slot". The banner
   reads the same field the server enforces. See `DEC-037`.

⚠ The sweep is **defence in depth, not the enforcement** — it never gates access, it only bounds how
long a disabled-in-ACMP account can still *log in*. Don't rewrite it as the control.

## 5. ✅ Deploy plumbing — DONE (#237, `DW-024`)

The client is defined in **`reconcile.sh`, not `realm-export.json`** (`DEF-049`): Keycloak imports
the bundled export **only on first run**, so a declaration there reaches a fresh stack and *never*
prod or UAT. The secret settles it — it can only match `gen-secrets`' file by being *set from* it.

- **The grant is `manage-users` and nothing else**, proven on UAT (`OQ-070`). It lives in
  `deploy/keycloak/admin-client.env`, read by both the reconciler and the CI gate.
- `check-realm-export.mjs` asserts the **exact** set with `realm-admin` forbidden; both failure
  branches proven by forcing them. The reconciler **revokes** extras, so widening can't survive.
- **Enabling is one variable.** The secret is always written; `09-put-env.sh` refuses
  `ENABLED=true` + a placeholder secret before it reaches a box.

✅ **`DEF-051` fixed (#238) — and the client is now proven to exist.** CI logs, every run:

```
[reconcile] creating client 'acmp-admin-svc'…
[reconcile] 'acmp-admin-svc' is confidential, service-account-only, secret set from the mounted file
[reconcile] realm-management grant is exactly: manage-users
```

⚠ **`up.sh` is known-unguarded** — `docker compose up --wait` **returns while a one-shot is still
running** (measured, not assumed: the guard's first run caught it mid-flight). So dev **and on-prem
prod** cannot catch a failed reconcile, where the failure reproduces `DEF-023` exactly — nobody can
log in, every health check green. The heavier fix, `depends_on: keycloak-config
{ condition: service_completed_successfully }` on `api`/`worker`, is **yours to decide**: it turns a
transient Keycloak hiccup into a refusal to start.

## 5c. Next, in order

1. **`FR-159`'s writer + `/session`** — see §4. This is the only thing between `AC-092` and Met.
2. **Deploy with `KEYCLOAK_ADMIN_ENABLED=true`.** No longer needed for `AC-090` (its guarantee is
   ACMP-side now and proven where it is decided), but it is what makes invite/roles usable at all.
3. The **automated probe-based** grant test owed from `OQ-071` (you chose "both, UAT first"): wrap
   `probe-keycloak-grant.mjs` so a *narrower* grant is proven refused, not merely that the
   configured one applies.
4. `up.sh` / on-prem reconcile guard (§5).
5. `OQ-069` — Secretary cannot reach `/admin`; an operator decision, not a code fix.

---

## 5b. `OQ-069` — an operator decision, not a code fix

`FR-156` and `FR-157` both say "As an Administrator **or Secretary**" and the server honours it, but
`App.tsx:100` gates `/admin` with `RequireRole ['administrator']`, so **a Secretary can reach
neither control**. Widening the route exposes templates, health, streams, jobs and notification
settings to Secretary and contradicts permission-matrix row 27 (SoD-5). Options: narrow the
requirements, move the affordances somewhere a Secretary can reach, or widen and accept the SoD
consequence. **Do not just widen it.**

## 6. ✅ The obligation — DISCHARGED on UAT (`OQ-070`)

**The minimum set is one role: `manage-users`.** Proven 2026-08-12, in two runs, because one run
proves sufficiency and not minimality:

```
{}             -> POST /users 403        (a grant is NECESSARY)
{manage-users} -> 8/8 calls succeed      (SUFFICIENT)
```

A one-element sufficient set whose empty subset is refused is **minimal by construction**.

⚠ **The obvious guess was wider.** `manage-users + view-realm` was the candidate, on the theory that
the role-mapping calls need `view-realm` for `canMapRole`. **They do not.** Shipping it would have
handed the service account a read over the entire realm configuration for nothing.

Re-runnable: `node scripts/probe-keycloak-grant.mjs --base <url> --realm acmp --client acmp-admin-svc
--secret <s>`. The operator chose *"both, UAT first"*, so the **automated** half is still owed —
wrap the probe in a CI test against the bundled Keycloak so it can't decay.

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
