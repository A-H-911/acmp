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

# DEF-023: the bundled realm-export registers only the dev + ngrok hostnames, and the cloud compose
# mounts it verbatim — so on a deployed box Keycloak answers `Invalid parameter: redirect_uri` and
# NOBODY CAN LOG IN, while every health check stays green (Keycloak is running fine; it simply
# refuses the one URI the SPA uses). A single shared realm file cannot express a hostname that is
# per-environment by design — the same seam class as DEF-019's web image tag.
#
# So the origin is reconciled here instead, from ACMP_WEB_ORIGIN. Only docker-compose.cloud.yml sets
# it (to KEYCLOAK_ORIGIN, which the cloud topology defines as the ONE browser-facing origin — the SPA
# and Keycloak share it under /kc/, ADR-0037). The dev compose deliberately does NOT set it: there
# KEYCLOAK_ORIGIN is keycloak.localhost:8085 while the SPA is on localhost:8088, so it is not the SPA
# origin and using it would break dev. Unset => this block no-ops => dev is untouched.
#
# REPLACE, not append: a public client in cloud has no business accepting localhost redirects, and
# assignment is what makes re-runs idempotent.
ensure_web_origin() {
  local origin="$1" cid before after
  cid=$("$K" get clients -r "$REALM" -q "clientId=acmp-web" --fields id --format csv --noquotes | head -n1 | tr -d '\r')
  if [[ -z "$cid" ]]; then
    echo "[reconcile] ERROR: client 'acmp-web' not found in realm '$REALM'" >&2
    return 1
  fi
  before=$("$K" get "clients/$cid" -r "$REALM" --fields redirectUris --format json | tr -d '\r\n ')
  "$K" update "clients/$cid" -r "$REALM" \
    -s "redirectUris=[\"${origin}/*\"]" \
    -s "webOrigins=[\"${origin}\"]"
  # Read back rather than trust the exit code — DEF-023 is a defect that every green check missed.
  after=$("$K" get "clients/$cid" -r "$REALM" --fields redirectUris --format json | tr -d '\r\n ')
  if [[ "$after" != *"${origin}/*"* ]]; then
    echo "[reconcile] ERROR: redirectUris did not take. before=$before after=$after" >&2
    return 1
  fi
  echo "[reconcile] acmp-web origin set to ${origin} (redirectUris=${origin}/*, webOrigins=${origin})."
}

# post.logout.redirect.uris is NOT set here. It lives in realm-export.json as the literal `+`, which
# Keycloak reads as "whatever redirectUris allows" — environment-independent, so it needs no patching
# and avoids kcadm's dotted-key path splitting entirely. Caveat: like every realm-export value it
# reaches only a FIRST import, so a realm created before that edit keeps its old list until re-created.
if [[ -n "${ACMP_WEB_ORIGIN:-}" ]]; then
  ensure_web_origin "$ACMP_WEB_ORIGIN"
else
  echo "[reconcile] ACMP_WEB_ORIGIN unset — leaving acmp-web redirect URIs as imported (dev topology)."
fi

echo "[reconcile] done."
