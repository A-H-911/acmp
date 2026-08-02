#!/usr/bin/env sh
# ACMP — materialize Docker secret files (ADR-0032, docs/domain/deployment.md §3.3).
#
# Reads credential values from deploy/.env (operator) or deploy/.env.example (CI/dev default) and writes one file
# per secret under deploy/secrets/ (git-ignored, mode 600). The .NET hosts consume the config-key-named files via
# AddKeyPerFile(/run/secrets); the infra images consume the *_password files via their *_FILE convention or a shim.
#
# printf '%s' (NOT echo) is deliberate: AddKeyPerFile uses the file content VERBATIM — a trailing newline would end
# up inside the connection string and break the login. Idempotent; run before every `docker compose up`.
set -eu

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
# ACMP_ENV_FILE lets a caller point this at a different env file (the PH-5 spike harness uses
# it so it never has to clobber the operator's deploy/.env). Default order is unchanged.
ENV_FILE="${ACMP_ENV_FILE:-$ROOT/deploy/.env}"
[ -f "$ENV_FILE" ] || ENV_FILE="$ROOT/deploy/.env.example"
SECRETS_DIR="$ROOT/deploy/secrets"
mkdir -p "$SECRETS_DIR"
# Dir 0700 keeps other host users out (deployment.md §3.3); the files themselves are 0644 (below) so the non-root
# CONTAINER UIDs (mssql / keycloak / postgres / minio / the app) can read the compose-mounted secret — a 0600 file
# owned by the host user is unreadable inside the container and the stack fails to start. (chmod is a no-op on a
# Windows dev host; it applies on the Linux VM + CI.)
chmod 700 "$SECRETS_DIR" 2>/dev/null || true

# Load the credential values. The env file is trusted operator/committed config (same file docker compose reads
# for non-secret interpolation), so sourcing it is acceptable; `#` inside an unquoted value is literal in POSIX sh.
set -a
# shellcheck disable=SC1090
. "$ENV_FILE"
set +a

write_secret() {  # name value
  printf '%s' "$2" > "$SECRETS_DIR/$1"
  chmod 644 "$SECRETS_DIR/$1"   # readable by the non-root container UID that mounts it (dir is 0700 — see above)
}

# --- infra credentials (consumed by SQL Server / MinIO / Postgres / Keycloak) ---
write_secret mssql_sa_password            "${MSSQL_SA_PASSWORD:?set MSSQL_SA_PASSWORD}"
write_secret kc_bootstrap_admin_password  "${KC_BOOTSTRAP_ADMIN_PASSWORD:?set KC_BOOTSTRAP_ADMIN_PASSWORD}"

# MinIO is dev/e2e-only from PH-5 on (the cloud stack uses S3, ADR-0035) — write its secrets
# only when a value is present, so a cloud deploy/.env need not carry them at all.
if [ -n "${MINIO_ROOT_PASSWORD:-}" ]; then
  write_secret minio_root_password        "$MINIO_ROOT_PASSWORD"
fi
# Keycloak's datastore credential. Pre-PH-5 (dev/e2e) this is the Postgres password; in the
# cloud stack Keycloak persists to SQL Server (ADR-0036) under its own login, so the value is
# written under BOTH names — kc_db_password for docker-compose.yml, keycloak_svc_password for
# docker-compose.cloud.yml + the sqlserver-init CREATE LOGIN. One source value, no drift.
KC_DB_PW="${ACMP_KC_DB_PASSWORD:-${KC_DB_PASSWORD:?set KC_DB_PASSWORD (or ACMP_KC_DB_PASSWORD)}}"
write_secret kc_db_password               "$KC_DB_PW"
write_secret keycloak_svc_password        "$KC_DB_PW"

# --- app config-key secrets (AddKeyPerFile maps `__` -> `:`) ---
# Runtime DB login: sa in dev/base; the prod overlay sets ACMP_DB_USER=acmp_svc + ACMP_DB_PASSWORD (P18a Batch 3).
# TrustServerCertificate flips to False at Step B (operator, deployment.md §3.4); Encrypt stays True (P16-B3).
DB_USER="${ACMP_DB_USER:-sa}"
DB_SERVER="${ACMP_DB_SERVER:-sqlserver}"; DB_NAME="${ACMP_DB_NAME:-Acmp}"; DB_TRUST="${ACMP_DB_TRUSTCERT:-True}"
if [ "$DB_USER" = "sa" ]; then DB_PW="$MSSQL_SA_PASSWORD"; else DB_PW="${ACMP_DB_PASSWORD:?set ACMP_DB_PASSWORD}"; fi
write_secret ConnectionStrings__Acmp \
  "Server=${DB_SERVER};Database=${DB_NAME};User Id=${DB_USER};Password=${DB_PW};TrustServerCertificate=${DB_TRUST};Encrypt=True"
# Object-storage secret key. Cloud (ADR-0035): the per-environment S3 IAM user's secret
# emitted by deploy/aws/03-iam.sh. Dev/e2e: the bundled MinIO root password. Same config key
# either way, so no application change is needed to switch backends.
write_secret Minio__SecretKey             "${ACMP_S3_SECRET_KEY:-${MINIO_ROOT_PASSWORD:?set ACMP_S3_SECRET_KEY (cloud) or MINIO_ROOT_PASSWORD (dev)}}"

# Prod least-priv (P18a Batch 3, ADR-0031): when the runtime login is acmp_svc (not sa), also provide its raw
# password (sqlserver-init CREATE LOGIN) and a SEPARATE migrator connection string (sa) for the --migrate-only
# deploy step — the runtime login has no DDL rights, so migrations run under a privileged principal.
if [ "$DB_USER" != "sa" ]; then
  write_secret acmp_svc_password          "$DB_PW"
  write_secret ConnectionStrings__AcmpMigrator \
    "Server=${DB_SERVER};Database=${DB_NAME};User Id=sa;Password=${MSSQL_SA_PASSWORD};TrustServerCertificate=${DB_TRUST};Encrypt=True"
fi

# --- Webex (only when enabled; otherwise the adapter is off and reads nothing — no empty files) ---
if [ "${WEBEX_ENABLED:-false}" = "true" ]; then
  write_secret Webex__BotToken            "${WEBEX_BOT_TOKEN:?set WEBEX_BOT_TOKEN}"
  write_secret Webex__WebhookSecret       "${WEBEX_WEBHOOK_SECRET:?set WEBEX_WEBHOOK_SECRET}"
  write_secret Webex__OAuthClientSecret   "${WEBEX_OAUTH_CLIENT_SECRET:?set WEBEX_OAUTH_CLIENT_SECRET}"
  write_secret Webex__TokenEncryptionKey  "${WEBEX_TOKEN_ENCRYPTION_KEY:?set WEBEX_TOKEN_ENCRYPTION_KEY}"
  write_secret Webex__OAuthSetupKey       "${WEBEX_OAUTH_SETUP_KEY:?set WEBEX_OAUTH_SETUP_KEY}"
fi

printf 'gen-secrets: wrote %s secret file(s) to deploy/secrets/ from %s\n' \
  "$(find "$SECRETS_DIR" -type f ! -name .gitkeep | wc -l | tr -d ' ')" "${ENV_FILE#"$ROOT"/}"
