#!/usr/bin/env bash
# ACMP nightly backup (P18b, ADR-0033, deployment.md §6). Runs on the HOST via cron against the running prod stack.
# Backs up, in order: SQL Server (native compressed .bak), Keycloak's Postgres (pg_dump), and the MinIO object store
# (mc mirror). Optionally copies the .bak off-box to the warm-standby VM. Non-SQL legs are guarded so a missing
# tool/target degrades gracefully rather than failing the whole run; a SQL failure exits non-zero (cron alerts).
#
#   deploy/scripts/backup.sh
# Config (env or deploy/.env): ACMP_BACKUP_DIR (host dir, bind-mounted to sqlserver:/backups), ACMP_DB_NAME,
#   STANDBY_HOST + STANDBY_DIR (off-box scp target; skipped if unset), BACKUP_RETENTION_DAYS (default 30).
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"
[ -f deploy/.env ] && set -a && . deploy/.env && set +a || true

COMPOSE="${COMPOSE:-docker compose -f deploy/docker-compose.yml -f deploy/docker-compose.prod.yml}"
BACKUP_DIR="${ACMP_BACKUP_DIR:-/opt/acmp/backups}"
DB_NAME="${ACMP_DB_NAME:-Acmp}"
RETENTION="${BACKUP_RETENTION_DAYS:-30}"
TS="$(date +%Y%m%d_%H%M%S)"
mkdir -p "$BACKUP_DIR" "$BACKUP_DIR/minio"

log() { printf '[backup %s] %s\n' "$(date +%H:%M:%S)" "$*"; }

# 1) SQL Server — native compressed backup to the /backups bind-mount (= host $BACKUP_DIR). CRITICAL: any failure
#    aborts the run. BACKUP requires sysadmin/db_backupoperator, so it connects as sa (password from the secret file).
log "SQL: BACKUP DATABASE [$DB_NAME] -> /backups/${DB_NAME}_${TS}.bak"
$COMPOSE exec -T sqlserver sh -c \
  "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P \"\$(cat /run/secrets/mssql_sa_password)\" -C -No -b -Q \
   \"BACKUP DATABASE [$DB_NAME] TO DISK='/backups/${DB_NAME}_${TS}.bak' WITH COMPRESSION, INIT, STATS=10;\""
log "SQL: done ($(ls -1 "$BACKUP_DIR/${DB_NAME}_${TS}.bak" 2>/dev/null || echo 'MISSING'))"

# 2) Keycloak Postgres — pg_dump over the container's local trust socket (no password needed). Guarded.
if $COMPOSE ps keycloak-db >/dev/null 2>&1; then
  log "Keycloak: pg_dump -> kc_${TS}.sql.gz"
  $COMPOSE exec -T keycloak-db sh -c "pg_dump -U \"\$POSTGRES_USER\" keycloak" | gzip > "$BACKUP_DIR/kc_${TS}.sql.gz" \
    || log "Keycloak: pg_dump FAILED (non-fatal — SQL backup already taken)"
fi

# 3) MinIO objects — mirror the store via a throwaway minio/mc container on the compose network. Guarded.
NET="$(docker network ls --format '{{.Name}}' | grep -E 'acmp.*_default|_acmp-net$' | head -1 || true)"
if [ -n "${NET:-}" ] && [ -n "${MINIO_ROOT_USER:-}" ]; then
  log "MinIO: mc mirror -> $BACKUP_DIR/minio"
  docker run --rm --network "$NET" -v "$BACKUP_DIR/minio:/mirror" --entrypoint sh minio/mc -c \
    "mc alias set acmp http://minio:9000 '$MINIO_ROOT_USER' \"\$(cat /run/secrets/minio_root_password 2>/dev/null || echo \"$MINIO_ROOT_PASSWORD\")\" >/dev/null 2>&1 || mc alias set acmp http://minio:9000 '$MINIO_ROOT_USER' '$MINIO_ROOT_PASSWORD' >/dev/null; mc mirror --overwrite --remove acmp /mirror" \
    || log "MinIO: mirror FAILED (non-fatal)"
fi

# 4) Off-box copy to the warm standby (RPO/disaster). Guarded — skipped if STANDBY_HOST unset. (deployment.md §7)
if [ -n "${STANDBY_HOST:-}" ]; then
  log "Standby: scp ${DB_NAME}_${TS}.bak -> ${STANDBY_HOST}:${STANDBY_DIR:-/opt/acmp/backups}/"
  scp -q "$BACKUP_DIR/${DB_NAME}_${TS}.bak" "${STANDBY_HOST}:${STANDBY_DIR:-/opt/acmp/backups}/" \
    || log "Standby: scp FAILED (non-fatal — local backup retained)"
fi

# 5) Retention prune (local). Standby retention is the operator's mirror policy.
log "Prune: removing local *.bak / *.sql.gz older than ${RETENTION}d"
find "$BACKUP_DIR" -maxdepth 1 -type f \( -name '*.bak' -o -name '*.sql.gz' \) -mtime "+${RETENTION}" -delete || true

log "backup complete: ${DB_NAME}_${TS}.bak"
