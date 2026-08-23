---
name: p18-deployment
description: "P18 Deployment — P18a hardening MERGED (#146), P18b backup/runbook in PR"
metadata: 
  node_type: memory
  type: project
  originSessionId: 56c6a1f9-39ae-4fe8-a5bc-546bda9ec91c
---

# P18 Deployment — START HERE (2026-07-19)

**P18a MERGED to main (PR #146, squash `7991f93`, all 9 CI checks green).** **P18b in PR #147 (open).** Deployment finalized per [[p18-slice-notes]]/deployment.md. Ladder now: **P19 (final audit & release readiness) is next — the last slice.**

## What shipped

**P18a — 3 batches, `feat/P18a-deploy-hardening`:**
- **Docker secrets EVERYWHERE (ADR-0032).** File-backed `/run/secrets`. .NET api/worker use native `AddKeyPerFile` (`__`→`:`); SQL Server + Keycloak use **entrypoint shims** (no reliable `_FILE`); MinIO + Postgres native `*_FILE`. `deploy/scripts/gen-secrets.sh` writes `deploy/secrets/*` — **0644 files in a 0700 dir** (0600 unreadable by the non-root container UID → stack fails), **`printf` not `echo`** (KeyPerFile takes content verbatim — trailing newline corrupts the connstr). `deploy/scripts/up.sh [--prod]` = single-command bring-up (NFR-052). ⚠ **The mssql shim is INLINED as an ENTRYPOINT, not a COPYed script** — `Dockerfile.sqlserver` is built with TWO contexts (compose=repo-root, `SearchProvidersFtsTests`=`deploy/`) so a COPY can't resolve in both.
- **Prod overlay `docker-compose.prod.yml`** (additive; base stays dev/e2e). Keycloak `start` prod-mode, Seq auth, **internal-only ports via Compose `!override`** (MinIO/Seq consoles on 127.0.0.1), resource limits §9, OCI labels, SQL `/backups` bind-mount.
- **Least-priv `acmp_svc` login (ADR-0031) — CLOSES AC-017/018 residual + D-16.** Runtime = `acmp_svc` (db_datareader+db_datawriter+EXECUTE+acmp_app, **NOT db_owner/sysadmin**) so the audit DENY binds. **Migrator/runtime split:** `--migrate-only` API flag → `db-migrate` one-shot (as sa) runs migrations + pre-provisions the Hangfire schema; runtime sets `Database:MigrateOnStartup=false` + `Hangfire:PrepareSchema=false`. `sqlserver-init` one-shot provisions acmp_svc + the acmp_app role before migrate. **★ db_owner was REJECTED (devil's-advocate review): it can REVOKE the DENY** → not least-priv, doesn't close the residual. Proof = extended `AuditImmutabilityDbPermissionTests` (`Least_priv_svc_login_with_datawriter_is_still_denied_audit_mutation`). Also: MinIO/Hangfire/Seq readiness checks on `/readyz` (NFR-045, custom `IHealthCheck`, `[ExcludeFromCodeCoverage]`).

**P18b — `feat/P18b-backup-runbook` (PR #147):** `deploy/scripts/{backup,restore,promote}.sh` + `crontab.example` (SQL `.bak`+pg_dump+mc mirror+scp; nightly + 4h business-day, RPO≤4h; **restore verify query = `decisions.decisions` lowercase — the doc's `decisions.Decisions` was wrong**); ADR-0033 (backup-as-scripts); `deploy/runbooks/README.md`. **Tested restore proven: seed 3 → backup → DELETE 0 → restore → 3** on a real SQL container.

## Gotchas / operator residuals (deployment.md §3.4 — stay open by design)
- TLS certs/topology (prod web is HTTP:80 for org-proxy TLS; or mount a cert); C-CRYPTO Step B + TDE + MinIO/Seq TLS/SSE + backup encryption; **OQ-040 SQL edition is a SECURITY decision (Express/Web forecloses TDE + backup encryption)**; **AC-004 Keycloak realm idle-timeout/MFA (OQ-003)**; full stack-integrated backup/restore + a real standby VM.
- ✓ **Keystone validator = 7/7 OK** with my P18 doc changes — **run the `keystone/1.0.0/scripts/validate_package.py` path** (`.../plugins/cache/keystone/keystone/1.0.0/`). ⚠ **Do NOT run the stale `keystone/0.1.0` build** also in the cache — it lacks the acceptance-audit filename-skip + has no G-PROGRESS gate, so it FALSELY reports `G-IDS NOT READY` with 74 AC-dup findings (acceptance-audit ↔ acceptance-criteria). That "74 findings" scare earlier was the wrong-validator artifact, NOT a real package defect.
- Dev-stack constraint: the long-lived `acmp` dev stack runs on host ports; validate on throwaway containers / CI, never `up --build` the dev stack. Docker IS available in-session (Server 29.6.1).
