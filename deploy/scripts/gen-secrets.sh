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

# DEF-020 preflight. Seq requires SEQ_FIRSTRUN_ADMINPASSWORDHASH to be a Base-64 salted hash and dies with
# "The input is not a valid Base-64 string" on anything else -- including the literal CHANGE_ME that
# .env.cloud.example ships. The reason this must fail HERE, loudly, is that it otherwise degrades silently:
# api declares `depends_on: seq: {condition: service_started}`, and service_started is satisfied by a container
# that started and then crashed, so a restart-looping Seq never blocks anything. The stack comes up fully
# healthy -- api, web and keycloak all green -- with observability dead and no log aggregation, which is exactly
# the state you cannot diagnose your way out of. Found on the first ever boot of the cloud stack (option C3).
# Only validated when set: the on-prem .env.example leaves it commented out and runs Seq unauthenticated.
# The LENGTH check is not belt-and-braces, it is the half that actually bites. An alphabet-only check
# passes any run of base64 characters, so the placeholder "unused" (6 chars, all legal) sailed through
# and Seq crash-looped anyway on the very next boot -- the guard failed to catch the exact failure it
# was written for. Base-64 is always a multiple of 4 characters, so length is what separates a real
# hash from a word that happens to be spelled in the base64 alphabet.
if [ -n "${SEQ_FIRSTRUN_ADMINPASSWORDHASH:-}" ]; then
  seq_hash_len=$(printf '%s' "$SEQ_FIRSTRUN_ADMINPASSWORDHASH" | wc -c | tr -d ' ')
  if ! printf '%s' "$SEQ_FIRSTRUN_ADMINPASSWORDHASH" | grep -Eq '^[A-Za-z0-9+/]+={0,2}$' \
     || [ "$((seq_hash_len % 4))" -ne 0 ]; then
    echo "gen-secrets: SEQ_FIRSTRUN_ADMINPASSWORDHASH is not valid Base-64 -- Seq will crash-loop." >&2
    echo "  value: '$SEQ_FIRSTRUN_ADMINPASSWORDHASH'" >&2
    echo "  Generate a real one:  docker run --rm datalust/seq config hash" >&2
    echo "  Leave it unset to run Seq without authentication (dev/on-prem only)." >&2
    exit 1
  fi
fi

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

# --- Keycloak Admin service account (ADR-0038) ---
# ALWAYS written, unlike the Webex block below, and the difference is not an inconsistency:
#   - this secret IS mounted as a real compose secret (ADR-0032), and a compose `secrets:` entry
#     whose file is missing fails the WHOLE stack — so a conditionally-written file would mean a
#     conditionally-bootable deployment;
#   - reconcile.sh sets the Keycloak client's secret FROM this file on every boot, so the client and
#     the app agree whether or not the feature is switched on;
#   - and that is what makes enabling in-app user management a ONE-VARIABLE change. KeycloakAdmin
#     options are ValidateOnStart: a KeycloakAdmin__Enabled=true that arrives before its secret
#     STOPS THE HOST at boot rather than degrading, and always-writing removes that ordering hazard.
# An unused service-account secret is inert — nobody authenticates as a client the app never calls.
#
# NOT `:?` — an existing operator deploy/.env predates this variable, and failing hard would break a
# working dev stack on `git pull`. Unset generates a random per-run value instead, which is SAFE
# rather than a shortcut: reconcile.sh pushes this file's contents to the Keycloak client on every
# boot, so a value nobody chose is still a value both sides agree on. There is deliberately no
# literal fallback — a default credential in a script is a credential.
if [ -z "${KEYCLOAK_ADMIN_CLIENT_SECRET:-}" ]; then
  KEYCLOAK_ADMIN_CLIENT_SECRET="$(head -c 24 /dev/urandom | base64 | tr -d '\n=' | tr '+/' '-_')"
  echo "gen-secrets: KEYCLOAK_ADMIN_CLIENT_SECRET unset — generated one for this run (set it in .env to pin it)."
fi
write_secret KeycloakAdmin__ClientSecret "$KEYCLOAK_ADMIN_CLIENT_SECRET"

# --- Webex (DEF-050 fixed 2026-08-12) ---------------------------------------------------------
# ALWAYS WRITTEN, EVEN WHEN WEBEX IS OFF — and that is the whole point rather than sloppiness.
# These five are now real compose secrets mounted into api and worker, and a `secrets:` entry whose
# FILE IS MISSING fails the entire stack, not just the feature. Writing them only under
# WEBEX_ENABLED=true would mean the default configuration (Webex off, everywhere today) cannot boot.
# Same reasoning, same shape as KeycloakAdmin__ClientSecret above.
#
# WHAT CHANGED AND WHY (DEF-050): these files used to be written under `if WEBEX_ENABLED=true` and
# mounted by NOTHING, while the credentials themselves travelled to the app as plain compose
# `environment:` entries — the one delivery channel ADR-0032 exists to avoid, because an environment
# value is readable by `docker inspect`, sits in /proc/1/environ, and is captured by anything that
# dumps container config. So the app read its Webex credentials from the rejected channel while the
# chosen channel sat unused beside it, and five files containing real credentials were written on
# every run for nobody to read. Verified 2026-08-12 against the DEPLOYED env in SSM: prod and UAT
# both carry `WEBEX_ENABLED=false` and NO credential variables at all, so nothing was ever exposed
# and nothing needed rotating — but the next person to enable Webex would have shipped one.
#
# NOT `:?` — an empty file is correct when the feature is off. KeyPerFile turns it into an empty
# config value, exactly what the old `${WEBEX_BOT_TOKEN:-}` environment default produced, and the
# adapter never reads it because Webex__Enabled is false. Failing hard here would break every stack
# that legitimately runs without Webex, which is all of them.
write_secret Webex__BotToken            "${WEBEX_BOT_TOKEN:-}"
write_secret Webex__WebhookSecret       "${WEBEX_WEBHOOK_SECRET:-}"
write_secret Webex__OAuthClientSecret   "${WEBEX_OAUTH_CLIENT_SECRET:-}"
write_secret Webex__TokenEncryptionKey  "${WEBEX_TOKEN_ENCRYPTION_KEY:-}"
write_secret Webex__OAuthSetupKey       "${WEBEX_OAUTH_SETUP_KEY:-}"

# Enabling Webex without its credentials is a configuration error worth catching at the last point
# before a machine, not at the first webhook that fails to verify.
if [ "${WEBEX_ENABLED:-false}" = "true" ]; then
  : "${WEBEX_BOT_TOKEN:?WEBEX_ENABLED=true requires WEBEX_BOT_TOKEN}"
  : "${WEBEX_WEBHOOK_SECRET:?WEBEX_ENABLED=true requires WEBEX_WEBHOOK_SECRET}"
  : "${WEBEX_OAUTH_CLIENT_SECRET:?WEBEX_ENABLED=true requires WEBEX_OAUTH_CLIENT_SECRET}"
  : "${WEBEX_TOKEN_ENCRYPTION_KEY:?WEBEX_ENABLED=true requires WEBEX_TOKEN_ENCRYPTION_KEY}"
  : "${WEBEX_OAUTH_SETUP_KEY:?WEBEX_ENABLED=true requires WEBEX_OAUTH_SETUP_KEY}"
fi

printf 'gen-secrets: wrote %s secret file(s) to deploy/secrets/ from %s\n' \
  "$(find "$SECRETS_DIR" -type f ! -name .gitkeep | wc -l | tr -d ' ')" "${ENV_FILE#"$ROOT"/}"
