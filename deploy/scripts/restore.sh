#!/usr/bin/env bash
# ACMP restore (P18b ADR-0033; re-shaped for the cloud topology in P23 / PH-5). DESTRUCTIVE — overwrites the
# live databases. Restores EVERY stateful SQL Server database from its newest .bak and verifies each one.
#
#   deploy/scripts/restore.sh                 # newest .bak per database in ACMP_BACKUP_DIR
#   deploy/scripts/restore.sh Acmp=/path/Acmp_20260803_020000.bak keycloak=/path/keycloak_20260803_020000.bak
#
# Config: ACMP_BACKUP_DIR, ACMP_DB_NAMES (default "Acmp keycloak"), COMPOSE.
#
# WHAT CHANGED IN P23: the old Keycloak leg piped a gzipped pg_dump into `psql` inside the keycloak-db
# container. That container NO LONGER EXISTS — ADR-0036 moved Keycloak onto the same SQL Server instance —
# so the leg could only ever have failed, and it was written to fail non-fatally, which would have reported
# a successful restore of a system nobody can log into. Keycloak is now restored the same way as Acmp.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"
# ACMP_ENV_FILE lets a drill point at its own env instead of the operator's (gen-secrets.sh convention),
# so a restore rehearsal can never inherit production values by accident.
ENV_FILE="${ACMP_ENV_FILE:-deploy/.env}"
[ -f "$ENV_FILE" ] && set -a && . "$ENV_FILE" && set +a || true

COMPOSE="${COMPOSE:-docker compose -f deploy/docker-compose.cloud.yml}"
BACKUP_DIR="${ACMP_BACKUP_DIR:-/opt/acmp/backups}"
DB_NAMES="${ACMP_DB_NAMES:-${ACMP_DB_NAME:-Acmp} keycloak}"
log() { printf '[restore %s] %s\n' "$(date +%H:%M:%S)" "$*"; }

# sqlcmd as sa inside the sqlserver container. -b makes a T-SQL error a non-zero exit so `set -e` bites.
sq() { $COMPOSE exec -T sqlserver sh -c \
  "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P \"\$(cat /run/secrets/mssql_sa_password)\" -C -No -b $1"; }

# Explicit db=file pairs override the newest-per-database default.
ARGS=("$@")
bak_for() { # db -> the db=file argument if given, else the newest .bak for that database
  local db="$1" a
  for a in ${ARGS[@]+"${ARGS[@]}"}; do
    case "$a" in "$db"=*) printf '%s' "${a#*=}"; return;; esac
  done
  ls -1t "$BACKUP_DIR/${db}"_*.bak 2>/dev/null | head -1 || true
}

# Resolve every backup BEFORE touching anything: a half-restored pair (new Acmp, old Keycloak) is worse
# than not starting, and a missing file must not be discovered after the first database is already gone.
declare -a PLAN=()
for DB in $DB_NAMES; do
  BAK="$(bak_for "$DB")"
  [ -n "$BAK" ] && [ -f "$BAK" ] || { echo "restore: no .bak found for [$DB] in $BACKUP_DIR"; exit 1; }
  PLAN+=("$DB=$BAK")
  log "plan: [$DB] <- $(basename "$BAK")"
done

# Stop everything that holds a connection. RESTORE needs exclusive access, and SINGLE_USER alone is a race:
# it kicks the current sessions out, then the api/keycloak reconnect within milliseconds and can take the one
# permitted single-user slot for themselves, leaving the RESTORE to fail with "database is in use".
# Only the ones actually up: `compose start` on a service with no container is an error, so restarting a
# fixed list would fail the run on any stack that isn't the full one (a restore drill, a partially-up host).
RUNNING="$($COMPOSE ps --services --status running 2>/dev/null | tr '\n' ' ')"
HOLDERS=""
for s in api worker keycloak; do
  case " $RUNNING " in *" $s "*) HOLDERS="$HOLDERS $s";; esac
done
if [ -n "$HOLDERS" ]; then
  log "stopping$HOLDERS for exclusive access"
  $COMPOSE stop $HOLDERS >/dev/null
fi

for ENTRY in "${PLAN[@]}"; do
  DB="${ENTRY%%=*}"; BAK="${ENTRY#*=}"; BAK_NAME="$(basename "$BAK")"
  log "restoring [$DB] from $BAK_NAME"
  sq "-Q \"IF DB_ID('$DB') IS NOT NULL ALTER DATABASE [$DB] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; \
      RESTORE DATABASE [$DB] FROM DISK='/backups/${BAK_NAME}' WITH REPLACE, STATS=10; \
      ALTER DATABASE [$DB] SET MULTI_USER;\""
done

# Verify each database independently. The Acmp probe is a governance table (the correct name really is
# decisions.decisions — lowercase schema AND table); the Keycloak probe is its realm table, which is what
# distinguishes "restored" from "restored empty". A non-numeric answer fails the run.
verify() { # db  query  label
  local n
  n="$(sq "-h -1 -W -Q \"SET NOCOUNT ON; $2\"" | tr -d '[:space:]')"
  log "verify $3 = ${n:-?}"
  case "$n" in ''|*[!0-9]*) echo "restore: verify query for [$1] did not return a number"; exit 1;; esac
  printf '%s' "$n"
}
for DB in $DB_NAMES; do
  case "$DB" in
    keycloak) verify "$DB" "SELECT COUNT(*) FROM [$DB].dbo.REALM;" "keycloak realms" >/dev/null;;
    *)        verify "$DB" "SELECT COUNT(*) FROM [$DB].decisions.decisions;" "decisions.decisions rows" >/dev/null;;
  esac
done

if [ -n "$HOLDERS" ]; then
  log "restarting$HOLDERS"
  $COMPOSE start $HOLDERS >/dev/null
fi

log "restore complete. Smoke: /healthz + /readyz, then a real login (proves the Keycloak restore, not just the row count)."
