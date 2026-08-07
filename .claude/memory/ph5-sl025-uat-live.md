---
name: ph5-sl025-uat-live
description: PH-5/SL-025 — UAT is live and LOGIN WORKS; DEF-022/023/024/025 all Fixed; only AC-075 (one clean rebuild) and AC-076 (SL-027) remain.
metadata: 
  node_type: memory
  type: project
  originSessionId: f79e14d2-046a-4f4d-a818-420d4c0e3381
  modified: 2026-08-07T15:30:47.472Z
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

**Remaining:** **AC-075 Partial** — the provisioning *scripts* now pass with zero interventions; it
needs one more from-scratch build carrying the DEF-024 fixes, and nothing known is in its way.
**AC-076 Pending** (SL-027, the Playwright suite vs UAT). AC-079/081 now **Met**.

**Gotchas learned here** (the PE-174 set still applies): the local boot gate **cannot** verify a
browser login — the published `web` image bakes `VITE_OIDC_AUTHORITY` per environment (ADR-0037), so
a `-uat` bundle drives the deployed box wherever it runs. `08-bootstrap-box.sh` now **refuses on
image-tag drift**; re-pin `ACMP_IMAGE_TAG`/`ACMP_WEB_TAG` in `/acmp/uat/env` (CI writes **full** SHAs,
`web` carries `-uat`) before bootstrapping. Never hand-edit files on the box — a `chmod` there left the
checkout dirty and the next bootstrap refused to `git checkout` over it.

See [[ph5-aws-deployment]] and [[git-push-hang-fix]].
