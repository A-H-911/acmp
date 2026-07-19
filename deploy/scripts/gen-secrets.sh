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
ENV_FILE="$ROOT/deploy/.env"
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
write_secret minio_root_password          "${MINIO_ROOT_PASSWORD:?set MINIO_ROOT_PASSWORD}"
write_secret kc_db_password               "${KC_DB_PASSWORD:?set KC_DB_PASSWORD}"
write_secret kc_bootstrap_admin_password  "${KC_BOOTSTRAP_ADMIN_PASSWORD:?set KC_BOOTSTRAP_ADMIN_PASSWORD}"

# --- app config-key secrets (AddKeyPerFile maps `__` -> `:`) ---
# Runtime DB login: sa in dev/base; the prod overlay sets ACMP_DB_USER=acmp_svc + ACMP_DB_PASSWORD (P18a Batch 3).
# TrustServerCertificate flips to False at Step B (operator, deployment.md §3.4); Encrypt stays True (P16-B3).
DB_USER="${ACMP_DB_USER:-sa}"
if [ "$DB_USER" = "sa" ]; then DB_PW="$MSSQL_SA_PASSWORD"; else DB_PW="${ACMP_DB_PASSWORD:?set ACMP_DB_PASSWORD}"; fi
write_secret ConnectionStrings__Acmp \
  "Server=${ACMP_DB_SERVER:-sqlserver};Database=${ACMP_DB_NAME:-Acmp};User Id=${DB_USER};Password=${DB_PW};TrustServerCertificate=${ACMP_DB_TRUSTCERT:-True};Encrypt=True"
write_secret Minio__SecretKey             "$MINIO_ROOT_PASSWORD"

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
