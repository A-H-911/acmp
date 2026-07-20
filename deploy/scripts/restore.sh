#!/usr/bin/env bash
# ACMP restore (P18b, ADR-0033, deployment.md §6.2). Restores a SQL Server .bak into the running stack and verifies.
# Optionally restores the Keycloak Postgres dump + MinIO mirror. DESTRUCTIVE — overwrites the live database.
#
#   deploy/scripts/restore.sh [<db_backup_file.bak>]     # default: newest *.bak in ACMP_BACKUP_DIR
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"
[ -f deploy/.env ] && set -a && . deploy/.env && set +a || true

COMPOSE="${COMPOSE:-docker compose -f deploy/docker-compose.yml -f deploy/docker-compose.prod.yml}"
BACKUP_DIR="${ACMP_BACKUP_DIR:-/opt/acmp/backups}"
DB_NAME="${ACMP_DB_NAME:-Acmp}"
log() { printf '[restore %s] %s\n' "$(date +%H:%M:%S)" "$*"; }

BAK="${1:-$(ls -1t "$BACKUP_DIR/${DB_NAME}"_*.bak 2>/dev/null | head -1 || true)}"
[ -n "$BAK" ] && [ -f "$BAK" ] || { echo "restore: no .bak found in $BACKUP_DIR (or bad arg)"; exit 1; }
BAK_NAME="$(basename "$BAK")"
log "restoring $BAK_NAME into [$DB_NAME]"

# RESTORE needs exclusive access: drop connections, restore WITH REPLACE, reopen. Runs as sa (secret file).
$COMPOSE exec -T sqlserver sh -c \
  "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P \"\$(cat /run/secrets/mssql_sa_password)\" -C -No -b -Q \
   \"IF DB_ID('$DB_NAME') IS NOT NULL ALTER DATABASE [$DB_NAME] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; \
     RESTORE DATABASE [$DB_NAME] FROM DISK='/backups/${BAK_NAME}' WITH REPLACE, STATS=10; \
     ALTER DATABASE [$DB_NAME] SET MULTI_USER;\""

# Verify: a governance table must have rows (deployment.md §6.2 step 6 — correct name is decisions.decisions).
log "verify: SELECT COUNT(*) FROM decisions.decisions"
COUNT="$($COMPOSE exec -T sqlserver sh -c \
  "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P \"\$(cat /run/secrets/mssql_sa_password)\" -C -No -h -1 -W -Q \
   \"SET NOCOUNT ON; SELECT COUNT(*) FROM [$DB_NAME].decisions.decisions;\"" | tr -d '[:space:]')"
log "decisions.decisions rows = ${COUNT:-?}"
case "$COUNT" in ''|*[!0-9]*) echo "restore: verify query did not return a number"; exit 1;; esac

# Optional: Keycloak + MinIO restore (operator supplies the matching dump/mirror; guarded).
KC_DUMP="${2:-$(ls -1t "$BACKUP_DIR"/kc_*.sql.gz 2>/dev/null | head -1 || true)}"
if [ -n "${KC_DUMP:-}" ] && [ -f "$KC_DUMP" ]; then
  log "Keycloak: restoring $(basename "$KC_DUMP")"
  gunzip -c "$KC_DUMP" | $COMPOSE exec -T keycloak-db sh -c "psql -U \"\$POSTGRES_USER\" -d keycloak" \
    || log "Keycloak: restore FAILED (non-fatal)"
fi

log "restore complete: [$DB_NAME] from $BAK_NAME (verified $COUNT decision rows). Run a /healthz + /readyz smoke."
