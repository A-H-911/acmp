---
name: ph5-sl025-uat-live
description: PH-5/SL-025 — UAT is live and LOGIN WORKS; DEF-022/023/024/025 all Fixed; only AC-075 (one clean rebuild) and AC-076 (SL-027) remain.
metadata: 
  node_type: memory
  type: project
  originSessionId: f79e14d2-046a-4f4d-a818-420d4c0e3381
  modified: 2026-08-07T18:02:25.975Z
---

**State as of 2026-08-09.** UAT is live at `https://uat.acmp.anas7ammo.dev`, instance
**`i-07ac28ac2fedab921`** — the ONLY instance in the account; `i-05085d458d886dc08` and
`i-0b632ed4ea0cd2a68` are both terminated. Elastic IP `35.173.149.191`, **STOPPED** when idle
(~$7.65/mo vs ~$38/mo). `export AWS_PROFILE=acmp-admin` on every AWS call.

**AC-080 is Met (2026-08-09).** A cron-triggered backup was watched to fire and land off-instance.
Two things made it real, and both generalise: ⚠ **append to the live crontab, never replace it** —
`crontab.example` carries `ACMP_ENV_FILE=…` as an environment assignment applying to every line
below it, so a standalone temp crontab makes `backup.sh` refuse and you prove the refusal, not the
schedule. ⚠ **the .bak files cannot prove cron ran** — hand-run and cron-run artifacts are
byte-identical; the evidence is crond's own `CROND[…]: (root) CMD (…)` journal line, and the S3
check must run **from the laptop** (an `s3 ls` on the box shares fate with the box, which is the
whole of NFR-058's first clause).

**`session-manager-plugin` without admin rights.** winget/MSI wants Program Files and stalls on an
invisible UAC prompt (`consent.exe`). Instead: AWS's `SessionManagerPlugin.zip` holds a nested
`package.zip` whose `bin/session-manager-plugin.exe` runs standalone — `aws ssm start-session` only
needs it on `PATH`. acmp-admin **does** hold `ssm:StartSession` (not just `SendCommand`).

**Login works now.** All four seeded accounts (`chairman`/`secretary`/`member`/`auditor`) complete a
real browser PKCE login and JIT-provision with the correct role. Re-run the proof any time with
`node src/Acmp.Web/uat-login-probe.mjs https://uat.acmp.anas7ammo.dev` — it is committed, not a temp
script. Passwords were rotated off the seed value; the probe tries the temp one then
`Uat_Acmp#2026_Rotated`.

**Four defects fixed in this session, all of the silent-success family:**
- **DEF-023** — realm-export registered no cloud hostname. `reconcile.sh` now replaces
  `redirectUris`/`webOrigins` from `ACMP_WEB_ORIGIN`, set **only** by the cloud compose.
  ⚠ **Not `ACMP_HOST`** — `cloud-stack-boot.sh` sets that to `localhost` while the app is on `:18080`.
- **DEF-024** — `certbot-deploy-hook.sh` was committed `100644`; certbot **execs** deploy hooks, so it
  aborted the whole `certonly` and issued **no certificate** while SSM reported Success.
- **DEF-025** — `/kc/` inherited the SPA's CSP + `X-Frame-Options: DENY`, blocking Keycloak's inline
  scripts **and** the `automaticSilentRenew` iframe. Both nginx templates now give `/kc/` its own block.
- **DEF-022** — cron backups skipped the S3 copy. Fixed at both ends and evidenced by an `env -i` run.

**AC-075 is now Met** — a from-scratch build at `fca58f36` with zero interventions on instance
`i-07ac28ac2fedab921` (the earlier ones are terminated). AC-079/081 Met too.

**Two more defects found finishing the slice:**
- **DEF-026** (Fixed) — **AL2023 ships no cron**, so runbook §8 (`crontab -e`) died and the backup
  *schedule* never existed on any box. Now installed by `08-bootstrap-box.sh`. ⚠ It hid because
  `aws ssm send-command` has **no `set -e`** — the invocation Success is the *last* command's.
  **Always run SSM steps under `set -eu`** or their Success means nothing.
- **DEF-027** (Open, SL-027) — **AC-076 cannot work as written**: the e2e `global-setup` needs the
  master-realm token + admin API, and the 443 listener 404s both by design. **Never relax the deny**
  (AC-081 rests on it) and **never add a password bypass to `seed-users.sh`** (CON-007/CON-009).
  ✅ **Remedy (b) is now measured viable, zero code:** port-forward box **port 80** (the nginx 8080
  block has no `/kc/` deny) to `localhost:8085`, then `E2E_KEYCLOAK_URL=http://localhost:8085/kc`
  while the browser still drives the public host. Through the tunnel: master realm **200**,
  `/kc/admin/realms/acmp/users` **401** (reachable, just unauthenticated — `hostname strict` does
  NOT block the admin REST API); the same paths on 443 stay **404**. Only blocker left is getting
  `KC_BOOTSTRAP_ADMIN_PASSWORD` (a docker secret on the box) to the workstation.

**AC-084 Partial, one leg left (2026-08-09).** A real e2e run drove the box and memory was **sampled
every 10s** (docker stats has no peak counter — an unsampled peak is lost): Keycloak peaked
**441.9 / 448 MiB (98.6%) and HELD**, zero OOM, zero deaths, swap 46 MiB. The AC allows "raised **or
shown to hold**", so this leg is done — but 6 MiB headroom is a standing risk; the slack to fund any
raise is in `worker` (103.8/256) and `seq` (168.3/256).
⚠ **A STOPPED t3 EARNS NO CPU CREDITS.** The handoff's "credits recovered while idle" is wrong — the
alarm read OK only because a stopped instance publishes no datapoints. It returned to ALARM at ~9
credits the moment the box started. Credits accrue 24/hr **only while running**, so this leg needs
**~2 hours of running time before** the e2e run.

**AC-085 leg 1 — the method that cannot work.** Arming a budget notification *below* current spend
puts it in ALARM instantly and **delivers nothing**: no OK→ALARM transition means no notification.
That is an un-failable check inverted — one that cannot *succeed*. A **2% threshold ($1.20 vs $1.065
spent) is armed now** and will transition naturally; next session just **observes** both the ALARM
state *and* a non-zero `NumberOfNotificationsPublished` on `acmp-budget-alerts`, then deletes it.
State alone is not arrival.

**DEF-028 (new, High)** — `SchedulePage.onSubmit` ends in a bare `return` over six conditions, and
the `!chair` one has **no rendered error** (`chairError` tests the weaker `!effectiveChairId`). So
Schedule can be silently inert: no request, no message, button still enabled. Fails core-loop on UAT
(no `POST /api/meetings` ever reaches nginx) while **CI is green on the same spec**. Which condition
tripped was not isolated; the loose thread is that `GET /api/members` appears nowhere in the access
log while `POST /api/members/me` appears repeatedly.

⚠ **The e2e suite seeds fixed-password `e2e-*` users** (password committed in `e2e/users.ts`) into
whatever realm it points at — and UAT's 443 is open to `0.0.0.0/0`. **Delete them after every run**
(done: HTTP 204 ×3). Its governance writes are hash-chained and **permanent**.

**Gotchas learned here** (the PE-174 set still applies): the local boot gate **cannot** verify a
browser login — the published `web` image bakes `VITE_OIDC_AUTHORITY` per environment (ADR-0037), so
a `-uat` bundle drives the deployed box wherever it runs. `08-bootstrap-box.sh` now **refuses on
image-tag drift**; re-pin `ACMP_IMAGE_TAG`/`ACMP_WEB_TAG` in `/acmp/uat/env` (CI writes **full** SHAs,
`web` carries `-uat`) before bootstrapping. Never hand-edit files on the box — a `chmod` there left the
checkout dirty and the next bootstrap refused to `git checkout` over it.

See [[ph5-aws-deployment]] and [[git-push-hang-fix]].
