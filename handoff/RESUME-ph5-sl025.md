# Resume prompt — PH-5 / SL-025 (paste into a fresh session)

> Written 2026-08-07 at the end of the Stage 2 session. Untracked on purpose — commit it or delete
> it. Everything below is also in the package (PE-173/174/175, DEF-022, DEF-023, AV-096…099).

---

Resume PH-5 slice SL-025 on ACMP. A UAT environment is already live on AWS and three acceptance
criteria are Met. One CRITICAL defect blocks three more.

## Orient before touching anything

1. `server_info()`, then `package_open("tamheed-package")`, then `gate_run()` — expect 7/7, ready.
2. Read the tail of the record: `entity_query("progress-entry")` → **PE-173, PE-174, PE-175**, and
   `entity_query("defect")` → **DEF-022** and **DEF-023**. They contain the reasoning behind
   everything below; do not re-derive it.
3. `git log --oneline -5` and `gh pr list`.
4. **Close the package** (`package_close`) when done reading — it holds a single-writer lock.

## State

- **PR #181 is open** with the AC-075 rebuild findings + DEF-023. Check CI, then merge.
- **UAT is built and STOPPED**: instance `i-0b632ed4ea0cd2a68`, Elastic IP `35.173.149.191`,
  `https://uat.acmp.anas7ammo.dev`. Start it before any browser/e2e work; AC-082 proved it returns
  in ~40s. **Stop it again when idle** — ~$7.65/mo stopped vs ~$38/mo running.
- **`export AWS_PROFILE=acmp-admin`** on every AWS call. Never operate as root.
- Acceptance: **AC-077/078/080/081/082/083/086 Met** · **AC-075/079/084 Partial** ·
  **AC-076 Pending** (SL-027).
- The environment config lives in SSM at `/acmp/uat/env` (SecureString, Standard tier) and
  **survives instance termination** — you do not need to rebuild it.

## Do these, in this order

### 1. DEF-023 — CRITICAL, unblocks three ACs at once

**Nobody can log into a cloud environment.** `deploy/keycloak/realm-export.json` registers
`acmp-web`'s `redirectUris` as `localhost:8088`, `localhost:5173` and two ngrok hosts — no cloud
hostname anywhere — and `docker-compose.cloud.yml` mounts it verbatim with no per-environment
substitution. The SPA starts a correct PKCE flow and Keycloak answers
`Invalid parameter: redirect_uri`. Prod would behave identically.

**Recommended fix:** the `keycloak-config` one-shot already authenticates with `kcadm` to reconcile
the realm — have it update `redirectUris` / `webOrigins` / `post.logout.redirect.uris` from
`ACMP_HOST` when that is set. Leaves dev untouched; reuses machinery already in the stack.

**Verify with a real browser login, not a health check.** That is the entire lesson of this defect:
every existing check passes on an environment nobody can enter.

Also re-check, recorded on DEF-023 but unassessed: our nginx CSP is set at *server* level and so
applies to everything proxied under `/kc/`, and Keycloak's login theme uses inline scripts. The
console reported them blocked. The error page still rendered, so it is unproven — but a login form
whose scripts are blocked can fail subtly.

Once fixed: **AC-079** (login + JIT provisioning), **AC-084** (e2e on t3.medium) and **AC-076**
(Playwright suite vs UAT) all become reachable.

### 2. AC-075 → Met

Needs **one clean from-scratch build** on the now-fixed scripts with **zero interventions** (~10
min). Terminate the instance, then run the runbook verbatim:

```
bash deploy/aws/07-launch.sh uat          # re-associates the Elastic IP by tag
bash deploy/aws/08-bootstrap-box.sh uat <sha-with-published-images>
# certbot + seed-users per deploy/runbooks/cloud-provisioning.md §6-§7
```

`09-put-env.sh` is correctly skipped — the SSM parameter persists. DNS needs no touch. **If it needs
any intervention, that is the finding** — record it, don't patch and claim a pass.

### 3. DEF-022 and the image-tag drift

- **DEF-022**: `crontab.example` runs `backup.sh` with no `ACMP_ENV_FILE`, so it reads `deploy/.env`
  which the cloud bootstrap never writes; `|| true` swallows it, `ACMP_BACKUP_BUCKET` ends up unset,
  and the off-instance S3 copy is **silently skipped** — NFR-058 unmet. Fix both the crontab line
  and `backup.sh`'s fall-through-to-defaults behaviour.
- **Image-tag drift**: the box ran `749071e` images with a `2ec8c14` checkout, because
  `ACMP_IMAGE_TAG`/`ACMP_WEB_TAG` in the SSM parameter pin whatever was current when the environment
  was published. Nothing reconciles the bootstrap sha with the image tags and **nothing detects the
  mismatch**. Add a runbook step (or a guard) that re-pins them for the commit being deployed.

## Environment gotchas — these cost hours; do not rediscover them

- **MSYS/Git Bash mangles leading-slash AWS arguments.** Four scripts were fixed
  (`05`, `07`, `08`, `09`) and **the directions differ**: `07/08/09` need `MSYS_NO_PATHCONV=1`
  (set in-script), while `05-route53.sh` needs conversion **on** and uses `cygpath -m` for its
  `file://` change-batch. Never set the variable shell-wide — that breaks `05`.
- **Reading SSM output on Windows needs `PYTHONIOENCODING=utf-8 PYTHONUTF8=1`**, or the CLI dies on
  non-ASCII with a `charmap` codec error while merely printing.
- **`aws ssm send-command` needs real JSON via `file://`**, never the `commands=[...]` shorthand —
  the shorthand parser splits on commas/newlines and tears multi-line scripts apart.
- **SSM caps `StandardOutputContent` at 24,000 chars.** Use `--quiet-pull`, or Docker progress bars
  bury the real error.
- **The Playwright MCP server is unusable** (accepts a navigate, then silent for 1800s). Use the
  repo's own Playwright at `src/Acmp.Web` instead — it works immediately. Put probe scripts *inside*
  that directory so `@playwright/test` resolves.
- **Git Credential Manager hangs on push** (4 times in one session). Ask the operator to run
  `! git push …`, or fix it once with `gh auth setup-git` — the `gh` CLI never failed.
- **Before `cloud-stack-boot.sh`**: Docker Desktop must be running, and **stop the local `acmp` dev
  stack first** — the gate swaps `deploy/secrets` and PE-151 records that breaking a live stack.
- Seeded accounts: `chairman` / `secretary` / `member` / `auditor`, temp password
  `ChangeMe_Acmp#2026`, all with `UPDATE_PASSWORD` pending.

## Cost discipline — binding, not advisory

Budget **$60/mo**; projection ~$47/mo. **Never run two instances** (2 × t3.medium ≈ $76/mo alone).
**Do not launch prod until UAT is proven.** **Never create a NAT gateway** (~$32/mo). Verify on any
new instance: `CpuCredits=standard`, 50 GB gp3, zero NAT gateways.

## Do not

- Do not re-litigate settled decisions (Elastic IP over a boot-updater; widened Route 53 grant;
  `promoted_to` as the DEC→ADR link).
- Do not hand-edit `tamheed-package/` — MCP tools are the only write path.
- Do not mark an AC Met on a check that cannot fail. This session caught three of those; DEF-023
  exists because every automated check passed on an unusable environment.
