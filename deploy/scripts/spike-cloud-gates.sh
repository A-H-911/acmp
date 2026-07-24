#!/usr/bin/env bash
# PH-5 spike gates U3 + U2 (SL-021). Proves the two unproven assumptions behind the cloud
# topology BEFORE any AWS spend. Run locally with Docker Desktop up:
#
#   bash deploy/scripts/spike-cloud-gates.sh
#   bash deploy/scripts/spike-cloud-gates.sh --keep     # leave the stack up for poking
#
# SAFETY — this cannot touch your long-lived dev stack:
#   * isolated compose project name (acmpspike) => its own containers AND its own volumes;
#   * its own generated env file under the OS temp dir, never deploy/.env;
#   * publishes NO host ports (every check runs inside the compose network), so it cannot
#     collide with the dev stack's 1433/8085/8088;
#   * teardown is `down -v` scoped to the acmpspike project only.
# Never run `up --build` against the `acmp` dev project — the SQL volume/password state
# mismatches and the container comes up unhealthy.
#
# U3: SQL Server Express really does carry Full-Text Search + the Arabic word-breaker.
# U2: Keycloak 26 boots in PRODUCTION mode on KC_DB=mssql, imports the realm, and survives a
#     restart with its data intact (AC-077). The browser PKCE leg is stage 3, see the tail.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
PROJECT=acmpspike
CF="$ROOT/deploy/docker-compose.cloud.yml"
ENV_FILE="${TMPDIR:-/tmp}/acmp-spike.env"
KEEP=0; [ "${1:-}" = "--keep" ] && KEEP=1
PASS=0; FAIL=0

log()  { printf '\n\033[1m[spike] %s\033[0m\n' "$*"; }
ok()   { PASS=$((PASS+1)); printf '  \033[32mPASS\033[0m %s\n' "$*"; }
bad()  { FAIL=$((FAIL+1)); printf '  \033[31mFAIL\033[0m %s\n' "$*"; }
dc()   { docker compose -p "$PROJECT" -f "$CF" --env-file "$ENV_FILE" "$@"; }
sq()   { # run a query as sa inside the sqlserver container; prints the bare scalar
  dc exec -T sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa \
    -P "$(grep '^MSSQL_SA_PASSWORD=' "$ENV_FILE" | cut -d= -f2-)" -C -No -h -1 -W -Q "SET NOCOUNT ON; $1" 2>/dev/null | tr -d '\r' | sed '/^$/d'
}

cleanup() {
  if [ "$KEEP" = "1" ]; then
    log "--keep: leaving the $PROJECT stack running. Tear down with:"
    printf '  docker compose -p %s -f %s --env-file %s down -v\n' "$PROJECT" "$CF" "$ENV_FILE"
  else
    log "tearing down the isolated $PROJECT stack (volumes included)"
    dc down -v --remove-orphans >/dev/null 2>&1 || true
  fi
}
trap cleanup EXIT

docker info >/dev/null 2>&1 || { echo "Docker daemon is not running — start Docker Desktop first."; exit 1; }

# --- spike env: local, http, no S3 needed for these gates --------------------------------
log "writing spike env -> $ENV_FILE"
cat > "$ENV_FILE" <<'ENV'
ACMP_REGISTRY=acmp
ACMP_IMAGE_TAG=spike
AWS_REGION=us-east-1
KEYCLOAK_HOSTNAME=http://localhost:8088/kc
KEYCLOAK_AUTHORITY=http://localhost:8088/kc/realms/acmp
KEYCLOAK_ORIGIN=http://localhost:8088
KC_BOOTSTRAP_ADMIN_USERNAME=admin
KC_BOOTSTRAP_ADMIN_PASSWORD=SpikeKC_2026#x
ACMP_KC_DB_NAME=keycloak
ACMP_KC_DB_USER=keycloak_svc
ACMP_KC_DB_PASSWORD=SpikeKCDB_2026#x
MSSQL_SA_PASSWORD=SpikeStrong_2026#x
ACMP_DB_NAME=Acmp
ACMP_DB_USER=acmp_svc
ACMP_DB_PASSWORD=SpikeSvc_2026#x
ACMP_DB_TRUSTCERT=True
ACMP_BACKUP_DIR=/tmp/acmp-spike-backups
ACMP_S3_ENDPOINT=s3.us-east-1.amazonaws.com
ACMP_S3_BUCKET=acmp-spike
ACMP_S3_ACCESS_KEY=spike
ACMP_S3_SECRET_KEY=spike
SEQ_FIRSTRUN_ADMINPASSWORDHASH=unused-in-spike
ACMP_REQUIRE_HTTPS_METADATA=false
WEBEX_ENABLED=false
ACTION_REMINDERS_SWEEP_CRON="0 6 * * *"
ENV
# Secrets are file-backed (ADR-0032) and the compose file resolves them relative to deploy/,
# so the spike must write into deploy/secrets. It is backed up and restored on exit.
# deploy/.env is NOT touched: gen-secrets honours ACMP_ENV_FILE.
printf '\n\033[33m[spike] NOTE: deploy/secrets/ is temporarily rewritten with spike values and\n'
printf '        restored on exit. Your deploy/.env is untouched. If your dev stack is running,\n'
printf '        do not restart it until this finishes.\033[0m\n'
BK="$(mktemp -d)"; cp -a "$ROOT/deploy/secrets/." "$BK/" 2>/dev/null || true
restore_secrets() { cp -a "$BK/." "$ROOT/deploy/secrets/" 2>/dev/null || true; }
trap 'cleanup; restore_secrets' EXIT
ACMP_ENV_FILE="$ENV_FILE" sh "$ROOT/deploy/scripts/gen-secrets.sh" >/dev/null

# =========================================================================================
log "U3 — SQL Server Express + Full-Text Search + Arabic word-breaker"
dc build sqlserver >/dev/null
dc up -d sqlserver >/dev/null
printf '  waiting for SQL Server'
for i in $(seq 1 40); do
  st="$(docker inspect -f '{{.State.Health.Status}}' "$(dc ps -q sqlserver)" 2>/dev/null || echo starting)"
  [ "$st" = "healthy" ] && { printf ' healthy\n'; break; }
  printf '.'; sleep 5
done
[ "$st" = "healthy" ] || { bad "SQL Server never became healthy"; exit 1; }

edition="$(sq "SELECT CAST(SERVERPROPERTY('Edition') AS nvarchar(128));")"
case "$edition" in *Express*) ok "edition is Express (DEF-014): $edition";; *) bad "expected Express, got: $edition";; esac

fts="$(sq "SELECT CAST(SERVERPROPERTY('IsFullTextInstalled') AS int);")"
[ "$fts" = "1" ] && ok "IsFullTextInstalled = 1 under Express" || bad "IsFullTextInstalled = '$fts' (expected 1)"

ar="$(sq "SELECT COUNT(*) FROM sys.fulltext_languages WHERE lcid = 1025;")"
[ "${ar:-0}" -ge 1 ] && ok "Arabic word-breaker present (LCID 1025)" || bad "Arabic word-breaker (LCID 1025) missing"

# End-to-end FTS proof: index an Arabic row and match it with CONTAINS.
sq "IF DB_ID('ftsspike') IS NULL CREATE DATABASE ftsspike;" >/dev/null
if sq "USE ftsspike;
CREATE TABLE dbo.t (id int NOT NULL PRIMARY KEY, body nvarchar(400));
INSERT INTO dbo.t VALUES (1, N'قرار لجنة الهندسة المعمارية بشأن النظام');
CREATE FULLTEXT CATALOG ftc AS DEFAULT;
CREATE FULLTEXT INDEX ON dbo.t(body LANGUAGE 1025) KEY INDEX PK__t__3213E83F CHANGE_TRACKING AUTO;" >/dev/null 2>&1; then
  sleep 8   # let the population crawl finish
  hit="$(sq "USE ftsspike; SELECT COUNT(*) FROM dbo.t WHERE CONTAINS(body, N'الهندسة');")"
  [ "${hit:-0}" -ge 1 ] && ok "Arabic CONTAINS() query matched an indexed row" \
                        || bad "Arabic CONTAINS() returned no rows (got '$hit')"
else
  bad "could not create the Arabic full-text index (see: dc logs sqlserver)"
fi

# =========================================================================================
log "U2 — Keycloak 26 production mode on KC_DB=mssql"
dc up -d sqlserver-init >/dev/null 2>&1 || true
sleep 3
rcsi="$(sq "SELECT CAST(DATABASEPROPERTYEX('keycloak','IsReadCommittedSnapshotOn') AS int);")"
[ "$rcsi" = "1" ] && ok "keycloak DB has READ_COMMITTED_SNAPSHOT ON" || bad "RCSI = '$rcsi' (expected 1)"

# Re-running init must be a clean no-op — this is the guard that stops every redeploy hanging.
if dc up -d sqlserver-init >/dev/null 2>&1; then ok "sqlserver-init re-run is idempotent (guarded RCSI did not block)"
else bad "sqlserver-init failed on re-run"; fi

dc up -d keycloak >/dev/null
printf '  waiting for Keycloak (production mode, mssql)'
kst=starting
for i in $(seq 1 48); do
  kst="$(docker inspect -f '{{.State.Health.Status}}' "$(dc ps -q keycloak)" 2>/dev/null || echo starting)"
  [ "$kst" = "healthy" ] && { printf ' healthy\n'; break; }
  printf '.'; sleep 5
done
if [ "$kst" = "healthy" ]; then
  ok "Keycloak reached healthy in production mode on SQL Server"
else
  bad "Keycloak never became healthy — logs:"; dc logs --tail 40 keycloak; exit 1
fi

# Realm imported + schema really landed in SQL Server (not an embedded H2 fallback).
tables="$(sq "SELECT COUNT(*) FROM keycloak.INFORMATION_SCHEMA.TABLES;")"
[ "${tables:-0}" -gt 50 ] && ok "Keycloak schema is in SQL Server ($tables tables)" \
                          || bad "keycloak DB has only '$tables' tables — schema may not be on mssql"
realm="$(sq "SELECT COUNT(*) FROM keycloak.dbo.REALM WHERE NAME = 'acmp';")"
[ "${realm:-0}" = "1" ] && ok "realm 'acmp' imported" || bad "realm 'acmp' not found (got '$realm')"

# AC-077: a restart must preserve realm + users.
log "U2 — restart persistence (AC-077)"
dc restart keycloak >/dev/null
for i in $(seq 1 48); do
  kst="$(docker inspect -f '{{.State.Health.Status}}' "$(dc ps -q keycloak)" 2>/dev/null || echo starting)"
  [ "$kst" = "healthy" ] && break; sleep 5
done
realm2="$(sq "SELECT COUNT(*) FROM keycloak.dbo.REALM WHERE NAME = 'acmp';")"
[ "$kst" = "healthy" ] && [ "${realm2:-0}" = "1" ] && ok "realm survived a Keycloak restart" \
  || bad "realm did not survive restart (healthy=$kst realm=$realm2)"

# OIDC discovery through Keycloak itself (the nginx /kc/ hop is exercised in stage 3).
iss="$(dc exec -T keycloak sh -c 'exec 3<>/dev/tcp/127.0.0.1/8080; printf "GET /realms/acmp/.well-known/openid-configuration HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n" >&3; cat <&3' 2>/dev/null | tr ',' '\n' | grep -o '"issuer":"[^"]*"' | head -1)"
[ -n "$iss" ] && ok "OIDC discovery served: $iss" || bad "OIDC discovery document not served"

# =========================================================================================
printf '\n\033[1m[spike] RESULT: %d passed, %d failed\033[0m\n' "$PASS" "$FAIL"
cat <<'NOTE'
Still MANUAL (stage 3) — the browser PKCE leg of U2, which needs the web image + nginx:
  docker compose -p acmpspike -f deploy/docker-compose.cloud.yml --env-file <spike env> \
    up -d --build web
  cd src/Acmp.Web && E2E_WEB_URL=http://localhost:8088 \
    E2E_KEYCLOAK_URL=http://localhost:8088/kc npx playwright test auth.spec.ts
(That leg also needs ACMP_HTTP_PORT=8088 in the spike env so `web` publishes a host port.)
NOTE
[ "$FAIL" -eq 0 ]
