# Resume prompt — PH-5 acceptance close-out (paste into a fresh session)

> Written 2026-08-09. Supersedes `handoff/RESUME-ph5-sl025.md`, which describes the SL-025
> provisioning work that is now finished (AC-075 Met). Everything below is also in the package.

---

Resume the PH-5 acceptance close-out on ACMP. UAT is live, login works, and the runbook is proven
zero-intervention. Eight acceptance criteria are Met; three are Partial and one is Pending.
Full handoff: `handoff/RESUME-ph5-closeout.md` — read it first.

## Orient before touching anything

1. `server_info()` → expect **2.7.1**. Then `package_open("tamheed-package")`, then `gate_run()` →
   expect **7/7 ready, audit 93/12**.
2. Read the reasoning rather than re-deriving it: **AV-105** (AC-080 Partial), **AV-104** (AC-084
   Partial), **AV-088** (AC-085 Partial), **DEF-027** (blocks AC-076), and progress entries
   **PE-178 … PE-183**.
3. `git log --oneline -5` and `gh pr list`.
4. **Close the package** (`package_close`) when done reading — it holds a single-writer lock.

## State

- **main `0b19971`, tree clean.** Tamheed **2.7.1**, MCP transport verified working.
- **UAT is built and STOPPED**: instance **`i-07ac28ac2fedab921`** (t3.medium), Elastic IP
  **35.173.149.191**, `https://uat.acmp.anas7ammo.dev`. Comes back in ~40s (AC-082 proved it).
  **Stop it again when idle** — ~$7.65/mo stopped vs ~$38/mo running.
- The box runs commit **`fca58f36…`** and `/acmp/uat/env` pins the matching image tags, so it is
  self-consistent. `main` has since moved — **if you re-bootstrap, re-pin the tags first** (the
  guard in `08-bootstrap-box.sh` will refuse otherwise, by design).
- **`export AWS_PROFILE=acmp-admin`** on every AWS call. Never operate as root.
- Acceptance: **AC-075/077/078/079/081/082/083/086 Met** · **AC-080/084/085 Partial** ·
  **AC-076 Pending**.
- Open defects: **DEF-012** (v_backlog residue — disclosed by design, no action) and **DEF-027**
  (blocks AC-076).
- Seeded accounts: `chairman` / `secretary` / `member` / `auditor`. Temp password
  `ChangeMe_Acmp#2026`; the login probe **rotated them to `Uat_Acmp#2026_Rotated`**.
- **`acmp-uat-cpu-credits-low` is currently `OK`** — the balance recovered while the box sat idle.
  It read ALARM right after provisioning because a fresh t3.medium starts at ~0 credits.

## Do these, in this order

### 1. AC-080 → Met — cheapest real progress (~15 min instance time)

The drill is already fully evidenced; what is missing is that the *schedule* has never been seen to
fire. Exit criterion, written into AV-105: **an actual cron-triggered backup observed to land
off-instance.** Every prerequisite is in place — `cronie` is installed by the bootstrap (DEF-026),
`crontab.example` carries its `ACMP_ENV_FILE` line (DEF-022), and a hand-run `backup.sh` already put
objects in S3.

Start the instance, install a temporary cron line a few minutes out, watch it fire, confirm both
`.bak` objects appear in **`s3://acmp-uat-backups/sql/`** with fresh timestamps, restore the real
schedule, record the verdict. A hand-invoked run does **not** satisfy this — the point is the
scheduler.

### 2. DEF-027 → AC-076 → AC-084 — the chain, and one decision gates it

`AC-076` says to run the Playwright suite with `E2E_KEYCLOAK_URL=https://uat…/kc`. That cannot work:
the suite's `global-setup` seeds fixtures through the Keycloak **admin API** and needs a
**master-realm token**, and the 443 listener 404s both by deliberate design. Measured, not assumed.

**Try option (b) first — it needs zero code.** `global-setup` already takes `E2E_KEYCLOAK_URL`
separately from `E2E_WEB_URL`, so an **SSM port-forward** of the box's Keycloak admin API to
localhost satisfies it while the browser still drives the public host. Blocked only on
`session-manager-plugin` not being installed locally — install it and this becomes "run the suite".

If (b) is unavailable, option (a) is a committed, clearly test-only out-of-band seeder plus a
skip-seeding mode in `global-setup`. **Option (c) — relaxing the `/kc/admin` deny — is rejected:**
it is a direct security regression and AC-081 rests on that posture. Equally, **do not add a
password bypass to `seed-users.sh`** — its temporary-password + `UPDATE_PASSWORD` behaviour is
load-bearing for CON-007/CON-009 and for attributed voting.

Once the suite runs, it also supplies the e2e load **AC-084** needs. AC-084 then has two remaining
legs beyond that run:

- the **CPUCreditBalance alarm must not be tripped** — currently `OK`, so run the suite on a box
  that has been up long enough to hold credits rather than immediately after a rebuild;
- **Keycloak sits at 96% of its 448 MiB cap** (430.5 MiB observed after only four logins). Raising
  it is a real decision, not a tweak: container limits already total **3536 MiB against a 3584 MiB
  budget**, so more for Keycloak means less for something else.

### 3. AC-085 — scope it before promising anything

**3 of the 5 amended legs are already evidenced live.** The gap is leg 1: the `acmp-budget-alerts`
SNS topic has a *confirmed* email subscription, but **an actually-observed threshold notification is
still outstanding** — and that fires on real spend crossing a threshold, so it may not be forceable
on demand. Read AV-088 in full before deciding whether this is closeable or should stay Partial with
the reason stated.

## Environment gotchas — these cost hours; do not rediscover them

- **`tamheed-package/data` is git-TRACKED.** `git reset --hard` / `checkout` / `stash` destroy
  uncommitted package writes exactly like uncommitted source. **Commit package writes the moment
  `package_close` returns**, before any branch operation. This cost three sessions of writes.
- **`git rm --cached` + `.git/info/exclude` does NOT protect a file** — the commit records a
  *deletion*, and the next `reset --hard` applies it. It deleted `findings_10.md` once. The
  `findings_*.md` corpus is now excluded-and-never-tracked, which is safe.
- **`aws ssm send-command` has NO `set -e`.** The invocation status is the *last* command's, so a
  mid-list failure still reports **Success**. Run every step under `set -eu` or the result means
  nothing — this hid DEF-024 (no TLS certificate) and DEF-026 (no cron) behind green invocations.
- **MSYS mangles leading-slash AWS arguments.** `07/08/09` set `MSYS_NO_PATHCONV=1` in-script;
  `05-route53.sh` needs conversion **on** with `cygpath -m`. Never set it shell-wide.
- **Reading SSM output on Windows** needs `PYTHONIOENCODING=utf-8 PYTHONUTF8=1`.
- **`ssm send-command` needs real JSON via `file://`**, never `commands=[...]` shorthand.
- **SSM caps stdout at 24,000 chars** — use `--quiet-pull`.
- **Never hand-edit files on the box.** A `chmod` there left the checkout dirty and the next
  bootstrap refused to `git checkout` over it. Rebuild rather than edit.
- **The local cloud boot gate cannot verify a browser login** — the published `web` image bakes
  `VITE_OIDC_AUTHORITY` per environment (ADR-0037), so a `-uat` bundle drives the deployed box
  wherever it runs. It *can* prove the authorization round-trip and the `/kc/` headers, and does.
- **The Playwright MCP server is unusable**; use the repo's own Playwright in `src/Acmp.Web`. The
  committed login probe is `src/Acmp.Web/uat-login-probe.mjs`.
- **`git push` hanging** → `gh auth setup-git` (already applied).
- **The `block-no-verify` hook falsely blocks `bash -n script.sh && git commit …`** — it matches a
  bare `-n` anywhere in the command. Run the syntax check as a separate call.
- **`findings_*.md` are all git-excluded** local field reports to the Tamheed owner, not ACMP
  deliverables. Latest: `findings_12` (the `mcp` 2.0.0 incident), closed by 2.7.1 — no findings_13.

## Cost discipline — binding, not advisory

Budget **$60/mo**. **Never run two instances** (2 × t3.medium ≈ $76/mo alone). **Do not launch prod
until UAT is proven** — and note `/acmp/prod/env` **does not exist**, so prod is blocked on
operator-supplied secrets regardless. **Never create a NAT gateway** (~$32/mo). On any new instance
verify `CpuCredits=standard`, 50 GB gp3, zero NAT gateways.

## Do not

- Do not re-litigate settled decisions: Elastic IP over a boot-updater; the widened Route 53 grant;
  `promoted_to` as the DEC→ADR link; the `/kc/admin` + master-realm deny.
- **`docs/` is a frozen read-only archive.** Its `NFR-056`/`NFR-057` swap is **left wrong on
  purpose** — the package rows and `deploy/` are correct. Do not "fix" the archive.
- Do not hand-edit `tamheed-package/` — the MCP tools are the only write path, and corrections to
  the append-only journal are **appended**, never edited.
- Do not start **P14 / Tarseem diagrams** — deferred indefinitely by DEC-028.
- **Do not mark an AC Met on a check that cannot fail.** DEF-023, DEF-024 and DEF-026 all passed
  every automated signal this project had. Each was caught by looking at a *result* rather than an
  exit code.
