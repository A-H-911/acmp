---
name: ph5-sl025-uat-live
description: PH-5/SL-025 — UAT is live and LOGIN WORKS; DEF-022/023/024/025 all Fixed; only AC-075 (one clean rebuild) and AC-076 (SL-027) remain.
metadata: 
  node_type: memory
  type: project
  originSessionId: f79e14d2-046a-4f4d-a818-420d4c0e3381
  modified: 2026-08-07T18:02:25.975Z
---

**State as of 2026-08-07.** UAT is live at `https://uat.acmp.anas7ammo.dev`, instance
`i-05085d458d886dc08` (the old `i-0b632ed4ea0cd2a68` was terminated), Elastic IP `35.173.149.191`,
**STOPPED** when idle (~$7.65/mo vs ~$38/mo). `export AWS_PROFILE=acmp-admin` on every AWS call.

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
  master-realm token + admin API, and the 443 listener 404s both by design. Fix is a design choice:
  SSM port-forward (no code) or an out-of-band seeder. **Never relax the deny** (AC-081 rests on it)
  and **never add a password bypass to `seed-users.sh`** (CON-007/CON-009).

**AC-084 Partial** — memory clean (no OOM, swap 20 MiB/4095), but its CPUCreditBalance alarm did not
exist at all until now (armed in `07-launch.sh`; it trips on a fresh box, which is real — a new
t3.medium has ~zero credits for ~2h), and **Keycloak runs at 96% of its 448 MiB cap**.

**Gotchas learned here** (the PE-174 set still applies): the local boot gate **cannot** verify a
browser login — the published `web` image bakes `VITE_OIDC_AUTHORITY` per environment (ADR-0037), so
a `-uat` bundle drives the deployed box wherever it runs. `08-bootstrap-box.sh` now **refuses on
image-tag drift**; re-pin `ACMP_IMAGE_TAG`/`ACMP_WEB_TAG` in `/acmp/uat/env` (CI writes **full** SHAs,
`web` carries `-uat`) before bootstrapping. Never hand-edit files on the box — a `chmod` there left the
checkout dirty and the next bootstrap refused to `git checkout` over it.

See [[ph5-aws-deployment]] and [[git-push-hang-fix]].
