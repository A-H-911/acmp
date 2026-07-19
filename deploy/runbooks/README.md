# ACMP Operations Runbook (P18)

On-prem, single-VM, Docker Compose (deployment.md). All commands run from the checkout root (`/opt/acmp`) as the
`deploy` user. The stack is brought up with `deploy/scripts/up.sh` (which materializes Docker secrets first — a bare
`docker compose up` is not sufficient, ADR-0032).

## Prerequisites (per VM)

1. Docker Engine + Compose plugin; a `deploy` OS user in the `docker` group.
2. Clone to `/opt/acmp`, checkout `main`.
3. `deploy/.env` — copy `deploy/.env.example`, set real values. For production also set the prod-overlay block:
   `ACMP_DB_USER=acmp_svc`, `ACMP_DB_PASSWORD=…`, `SEQ_FIRSTRUN_ADMINPASSWORDHASH=…`
   (`docker run --rm datalust/seq config hash`), `ACMP_BACKUP_DIR=/opt/acmp/backups`, and (for the standby)
   `STANDBY_HOST`/`STANDBY_DIR`.
4. `mkdir -p /opt/acmp/backups` (the SQL `/backups` bind-mount target).

## Secrets & TLS (operator obligations — deployment.md §3.4)

- Secrets are **file-backed** (ADR-0032). `up.sh` runs `gen-secrets.sh` to write `deploy/secrets/*` (0644 files in a
  0700 dir) from `deploy/.env`. Never commit `deploy/.env` or `deploy/secrets/*`.
- **TLS:** the prod overlay serves the web on HTTP:80 for the **org reverse proxy / load balancer to terminate TLS**
  (recommended, CON-001). For self-terminated TLS, mount a cert into `deploy/nginx/certs/` and add a 443 listener.
- **C-CRYPTO Step B / TDE / MinIO+Seq TLS / backup encryption** remain operator steps (deployment.md §3.4). ⚠ The
  **SQL edition** you choose (OQ-040) is a security decision: **Express/Web support neither TDE nor backup
  encryption** — pick Standard+ if those P1 controls are required. The bundled image is Developer (full features).
- **Keycloak session policy (OQ-003, AC-004):** in the ACMP realm set the **60-minute idle timeout** and MFA for
  Chairman + Secretary. This closes AC-004 and is a realm-config action (no code change).

## First-time install

1. `deploy/scripts/up.sh --prod` — brings up the full prod stack. On first start: `sqlserver-init` provisions the
   least-priv `acmp_svc` login (ADR-0031), `db-migrate` runs EF migrations + installs the Hangfire schema, Keycloak
   imports the ACMP realm, then api/worker/web start.
2. Wait for health: `docker compose -f deploy/docker-compose.yml -f deploy/docker-compose.prod.yml ps` → all `healthy`
   (≤5 min, NFR-052).
3. Smoke: `curl -k https://<host>/api/healthz` → 200; `.../api/readyz` → 200 (SQL + MinIO + Hangfire ready).
4. Seed the initial Administrator in the Keycloak admin console; assign the `Administrator` realm role.
5. Verify login end-to-end (browser → Keycloak → dashboard).
6. Install the backup schedule: `crontab deploy/scripts/crontab.example` (adjust the path).
7. Provision the standby VM: copy the checkout + `deploy/.env`; `docker compose … pull`; do NOT run the stack there.

## Routine upgrade

1. Merge to `main`; CI green. On the VM: `git pull`.
2. `deploy/scripts/up.sh --prod` (rebuilds/pulls images; `db-migrate` runs new migrations as the privileged
   principal — the runtime acmp_svc never migrates). Migrations are expand-contract / backward-compatible (NFR-050).
3. Smoke (step 3 above). Tag the release.

## Rollback (≤30 min — DoD)

1. `git checkout <previous-tag>` (or set the previous `IMAGE_*`), `deploy/scripts/up.sh --prod`.
2. Wait for `healthy`; smoke.
3. **If the schema changed**, roll the data back too: `deploy/scripts/restore.sh` from the last-known-good `.bak`
   (there is no `MigrateDown` in production — restore is the only safe schema rollback). Post-mortem in Seq + audit.

## Backup

- Automated by cron (`crontab.example`): nightly full + every-4h business-day (RPO ≤4h, NFR-057), off-box to the
  standby (NFR-058). Manual run: `deploy/scripts/backup.sh`.
- Covers **SQL Server** (`.bak`), **Keycloak Postgres** (`pg_dump`, ADR-0015), and **MinIO** objects (`mc mirror`).
  A SQL failure exits non-zero (hook your alerting to that). Verify a `.bak` appears in `ACMP_BACKUP_DIR`.

## Restore (tested)

`deploy/scripts/restore.sh [file.bak]` — RESTORE … WITH REPLACE, then verifies `decisions.decisions` has rows.
DESTRUCTIVE (overwrites the live DB). Validated: seed → backup → delete → restore round-trips the data. RTO ≤8h
(NFR-056). Quarterly restore test (NFR-058) — run `restore.sh` against a scratch stack and confirm the row count.

## Warm-standby promotion (primary failure)

On the **standby** VM: `deploy/scripts/promote.sh` — brings the stack up, restores the newest synced backup, and
smokes `/healthz`+`/readyz`. Then **manually** repoint DNS / the reverse proxy to the standby and announce
restoration to the committee. Budget: RTO ≤8h, RPO ≤4h.

## Observability

- Seq UI: `http://127.0.0.1:8341` on the VM (SSH-tunnel; not public). Admin password per `SEQ_FIRSTRUN_ADMINPASSWORDHASH`.
- Hangfire dashboard: `/hangfire` (Secretary/Admin). Nightly integrity-verify job at 03:00 (ADR-0030).
- Audit immutability: the DB refuses UPDATE/DELETE on `schema::audit` for the runtime `acmp_svc` login (ADR-0031).
