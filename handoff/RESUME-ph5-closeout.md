# Resume prompt — PH-5 acceptance close-out (paste into a fresh session)

> Rewritten 2026-08-09. Supersedes the earlier version of this file, which is stale in several
> places — most importantly its claim that CPU credits "recovered while the box sat idle". They do
> not; see the AC-084 section. Everything below is also in the package.

---

Resume the PH-5 acceptance close-out on ACMP. UAT is live and running the current `main`. **Ten
acceptance criteria are Met; two are Partial**, and neither remaining item is blocked on
engineering. Full handoff: `handoff/RESUME-ph5-closeout.md` — read it first.

## Orient before touching anything

1. `server_info()` → expect **2.7.1**. Then `package_open("tamheed-package")`, then `gate_run()` →
   expect **7/7 ready**, audit evidence **100 evidenced / 12 narrated**.
2. Read the reasoning rather than re-deriving it: **AV-106** (AC-080 Met), **AV-111** (AC-076,
   supersedes AV-108), **AV-112** (AC-084 Met, supersedes AV-109), **AV-110** (AC-085, supersedes
   AV-107), **DEF-029** (the live blocker), **OQ-066** and **OQ-067**, and progress entries
   **PE-184 … PE-190**.
3. `git log --oneline -5` and `gh pr list`.
4. **Close the package** (`package_close`) when done reading — it holds a single-writer lock, and
   **commit `tamheed-package/data` the moment it returns**.

## State

- **UAT runs `155cc803` — current `main`.** Instance **`i-07ac28ac2fedab921`** is the only instance
  in the account; Elastic IP **35.173.149.191**; `https://uat.acmp.anas7ammo.dev`. `/acmp/uat/env`
  pins `ACMP_IMAGE_TAG=155cc803…` and `ACMP_WEB_TAG=155cc803…-uat`, so box, pins and `main` agree.
- **`export AWS_PROFILE=acmp-admin`** on every AWS call. Never operate as root.
- Acceptance: **AC-075/077/078/079/080/081/082/083/084/086 Met** · **AC-076/085 Partial**.
- Open defects: **DEF-012** (v_backlog residue — by design, no action) and **DEF-029** (below).
  DEF-027 and DEF-028 are both **Fixed**.
- Seeded accounts `chairman` / `secretary` / `member` / `auditor`, password `Uat_Acmp#2026_Rotated`.
  The three `e2e-*` accounts are **disabled**, not deleted — read DEF-029 before changing that.

## Do these, in this order

### 1. AC-076 → Met — one operator action, no code (~20 min)

The DEF-028 fix is deployed and **verified on UAT**: the meeting-scheduling step that used to hang
for 180 s now passes, and the whole run dropped from 3.8 min to 55.5 s. Auth (real PKCE) and
RTL/a11y pass. Only the core-loop spec still fails, and **the cause is fixture data, not product
code** — `getByRole('option', {name: 'E2E Member'})` matches two elements because
`GET /api/members` returns 10 rows where 7 are expected.

**Why:** a previous session DELETED the `e2e-*` Keycloak users on sound security grounds. App-side
identity is the Keycloak `sub`, and audit rows are immutable, so re-seeding minted new subs and
created a SECOND `CommitteeMember` for each. See **DEF-029**.

**To close it**, deactivate exactly these three rows and nothing else. They were identified by
matching every row's `keycloakUserId` against the subs still present in the realm — the two rows in
each duplicated pair share display name, role *and* email, so **only the sub distinguishes them**.
Do not re-derive this; it costs a box start, a tunnel and a browser probe.

```
POST /api/members/4126bdc9-b6d8-4963-9db3-2b8d1b4b5aa0/deactivate   # E2E Chairman  (orphan sub b0bf51fb…)
POST /api/members/6432bcce-fe70-44d6-9265-9e8d03360849/deactivate   # E2E Member    (orphan sub aba62130…)
POST /api/members/86815220-6154-4af9-a766-3bcfe3714896/deactivate   # E2E Secretary (orphan sub a309db7d…)
```

**KEEP** the live counterparts `efb37e41-…`, `c06dc05a-…` and `77d0faee-…`.

`Policies.AdminUsers`, or Administration → Users & Membership in the UI. Deactivating a member is an
ordinary governance action, not data repair. `activeMembers` filters on `isActive`, so this removes
the ambiguity; then re-run (recipe below). **This needs an account holding the Administrator role —
and role-mappings for all eight realm users show only `acmp-admin` has it.** No credential for it was
available to the previous session, which is the only reason this is still open.

⚠ **Never delete the `e2e-*` accounts again — disable them** (`PUT …/users/{id}` with
`{"enabled": false}`). That removes the login risk just as completely and preserves the `sub`.

### 2. AC-085 leg 1 → Met — pure observation, no action (~1 day)

Legs 2–5 are Met. A **2 % ACTUAL notification is already armed and sitting in OK**
($1.20 against $1.065 spent). It will transition on its own as ordinary spend crosses it.
Check **both**: the 2.0 threshold reading ALARM **and** a non-zero `NumberOfNotificationsPublished`
on the `acmp-budget-alerts` topic. **State alone is not arrival** — that distinction is the whole
finding. Arming a threshold *below* current spend can never work: it goes ALARM instantly with no
OK→ALARM transition, so nothing is ever delivered. Delete the 2 % notification once observed.

### AC-084 is Met — do NOT re-do it (AV-112)

Recorded here because an earlier draft of this file sent a session to close it after it was already
closed. The alarm cleared on its own at 50.09 credits after ~1h50m of running, with **no threshold
tuning**, and the run then held it OK throughout while the balance ROSE, 50.09 → 51.59. The number
that settles it is usage, not balance: **0.426 and 0.498 credits** consumed in the two 5-minute
periods covering the run, against **2.0 earned** in the same window. An e2e run costs about a
quarter of what the box earns while running it — so the clause was never about e2e load at all, only
about cold-start warm-up. ⚠ **A STOPPED t3 EARNS NO CPU CREDITS**; the alarm reads OK while stopped
only because a stopped instance publishes no datapoints. Memory holds too: Keycloak 430.7 MiB of its
448 cap, zero OOM, all six containers healthy. **Keycloak's ~17 MiB of headroom is a standing risk**,
the thinnest in the stack against a 3536/3584 MiB limits total; the slack to fund any raise is in
`worker` and `seq`. See **OQ-067** for the design question this raised.

## Running the suite against UAT (proven recipe)

```bash
# 1. tunnel — session-manager-plugin needs NO install: AWS's SessionManagerPlugin.zip holds a
#    nested package.zip whose bin/session-manager-plugin.exe runs standalone, on PATH.
aws ssm start-session --target i-07ac28ac2fedab921 \
  --document-name AWS-StartPortForwardingSession \
  --parameters '{"portNumber":["80"],"localPortNumber":["8085"]}'
# 2. box port 80 -> the nginx 8080 block, which carries NO /kc/ deny. The public 443 listener
#    still 404s /kc/admin and /kc/realms/master, and must keep doing so (AC-081).
# 3. KC_BOOTSTRAP_ADMIN_USERNAME/PASSWORD come from the box's docker secret. Pull them
#    disk-to-disk; never render them into a transcript.
E2E_WEB_URL=https://uat.acmp.anas7ammo.dev E2E_KEYCLOAK_URL=http://localhost:8085/kc \
  npx playwright test e2e/auth.spec.ts e2e/core-loop.spec.ts e2e/rtl-a11y.spec.ts
# 4. afterwards: DISABLE the e2e-* users (never delete — DEF-029).
```

Only the three specs above are in AC-076's scope. The other 25 are visual-regression specs whose
baselines come from the local stack; they would fail on UAT for reasons that say nothing about UAT.

## Redeploying (proven recipe)

`main` → CI publishes on push only → verify the images exist in ECR **before** re-pinning
(DEF-019) → `bash deploy/aws/09-put-env.sh uat <env-file>` with `ACMP_IMAGE_TAG=<full-sha>` and
`ACMP_WEB_TAG=<full-sha>-uat` → `bash deploy/aws/08-bootstrap-box.sh uat <full-sha>`. The drift
guard should **pass on its own**; if you find yourself reaching for `ACMP_ALLOW_TAG_DRIFT=1`, stop —
that flag is for deliberate rollbacks only.

## Environment gotchas — these cost hours; do not rediscover them

- **`tamheed-package/data` is git-TRACKED.** `git reset --hard` / `checkout` / `stash` destroy
  uncommitted package writes. **Commit the moment `package_close` returns.**
- **⚠ A claim of ABSENCE is never supportable by a truncated search.** `tail`/`head`/`-m` in the
  pipeline means "I did not check", not "it is not there". This put a false lead into DEF-028 and
  cost a retraction; it is the second occurrence of the shape (PE-183 was the first).
- **⚠ In a system with immutable history, cleanup is not symmetric with creation.** Deleting an
  upstream identity ORPHANS what it created downstream, permanently. That is DEF-029.
- **`aws ssm send-command` has NO `set -e`.** The invocation status is the *last* command's, so a
  mid-list failure still reports Success. Send the whole script as ONE element under `set -eu`.
- **MSYS mangles leading-slash AWS arguments.** `--name /acmp/uat/env` returns `ParameterNotFound`
  until `MSYS_NO_PATHCONV=1` is set — while `describe-parameters` lists it happily. Confirmed again
  2026-08-09. `07/08/09` set it in-script; `05-route53.sh` needs conversion **on** with `cygpath -m`.
- **Reading SSM output on Windows** needs `PYTHONIOENCODING=utf-8 PYTHONUTF8=1`; commands via
  `file://` JSON; stdout caps at 24,000 chars (`--quiet-pull`).
- **`python -c` with embedded newlines is broken here** — `python` is a pyenv-win `.bat` shim that
  mangles it. Put the script in a `.py` file. Also use `pwd -W`, not `pwd`, when handing a path to a
  Windows binary from MSYS.
- **Never hand-edit files on the box.** A `chmod` there left the checkout dirty and the next
  bootstrap refused to `git checkout` over it. Rebuild rather than edit.
- **The local cloud boot gate cannot verify a browser login** — the `web` image bakes
  `VITE_OIDC_AUTHORITY` per environment (ADR-0037). It *can* prove the authorization round-trip and
  the `/kc/` headers, and does.
- **CI cannot catch load races.** On localhost the data always wins; over the internet it does not.
  That is DEF-028, and it is why AC-076 exists at all.
- **The Playwright MCP server is unusable**; use the repo's own Playwright. The committed login
  probe is `src/Acmp.Web/uat-login-probe.mjs`.
- **`block-no-verify` falsely blocks a bare `-n` anywhere in the command** — including inside a
  commit message body. Reword rather than fight it.

## Cost discipline — binding, not advisory

Budget **$60/mo**; actual spend **$1.065**. **Never run two instances.** **Never create a NAT
gateway** (~$32/mo). **Stop the box when idle** (~$7.65/mo stopped vs ~$38/mo running) — but note
the AC-084 warm-up tension in OQ-067. On any new instance verify `CpuCredits=standard`, 50 GB gp3,
zero NAT gateways. `/acmp/prod/env` **does not exist**, so prod remains blocked on operator secrets
regardless.

## Do not

- Do not re-litigate settled decisions: Elastic IP over a boot-updater; the widened Route 53 grant;
  `promoted_to` as the DEC→ADR link; the `/kc/admin` + master-realm deny.
- **`docs/` is a frozen read-only archive.** Its `NFR-056`/`NFR-057` swap is **left wrong on
  purpose**. Do not "fix" the archive.
- Do not hand-edit `tamheed-package/` — the MCP tools are the only write path, and corrections to
  the append-only journal are **appended**, never edited.
- Do not start **P14 / Tarseem diagrams** — deferred indefinitely by DEC-028.
- **Do not mark an AC Met on a check that cannot fail** — and note the inverse now has a case too:
  AC-085 leg 1's first attempt was a check that could not *succeed*.
