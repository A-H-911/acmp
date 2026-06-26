#!/usr/bin/env bash
# Idempotent Keycloak realm reconciliation (CHANGE-004 follow-up, OQ-041).
#
# Keycloak imports the bundled realm-export only on FIRST run (it never re-imports an existing realm),
# so committed realm-config changes don't reach a deployment whose Keycloak DB volume already has the
# realm. This one-shot job runs after Keycloak is healthy and re-applies the *critical* client-scope
# assignments via the admin API — safely and idempotently, WITHOUT touching users/passwords (unlike a
# destructive `import --override`). It uses kcadm.sh (the keycloak image ships bash + kcadm but no
# curl/jq), so it adds no new runtime dependency (CON-001).
set -euo pipefail

K=/opt/keycloak/bin/kcadm.sh
SERVER="${KC_URL:-http://keycloak:8080}"
REALM="${ACMP_REALM:-acmp}"

echo "[reconcile] authenticating to ${SERVER} (master realm)…"
"$K" config credentials --server "$SERVER" --realm master \
  --user "$KC_BOOTSTRAP_ADMIN_USERNAME" --password "$KC_BOOTSTRAP_ADMIN_PASSWORD"

# Ensure <client> carries <scope> as a DEFAULT client scope. The PUT is a no-op when already assigned,
# so this is safe to run on every deploy.
ensure_default_scope() {
  local client="$1" scope="$2" cid sid
  cid=$("$K" get clients -r "$REALM" -q "clientId=$client" --fields id --format csv --noquotes | head -n1 | tr -d '\r')
  sid=$("$K" get client-scopes -r "$REALM" --fields id,name --format csv --noquotes | tr -d '\r' | grep ",${scope}\$" | cut -d, -f1 | head -n1)
  if [[ -z "$cid" || -z "$sid" ]]; then
    echo "[reconcile] ERROR: client '$client' (id='$cid') or scope '$scope' (id='$sid') not found in realm '$REALM'" >&2
    return 1
  fi
  "$K" update "clients/$cid/default-client-scopes/$sid" -r "$REALM"
  echo "[reconcile] ensured default client scope '$scope' on client '$client'."
}

# CHANGE-004: the access token must carry `sub`. In Keycloak 24+ that claim ships in the built-in
# `basic` client scope; without it ICurrentUser.UserId is empty and JIT provisioning + subject-scoped
# ABAC silently break. The realm-export now assigns `basic`, and this reconciles existing realms too.
ensure_default_scope acmp-web basic

echo "[reconcile] done."
