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

# ---------------------------------------------------------------------------------------------
# ADR-0038 — the in-app user-management service account (DW-024).
#
# IT LIVES HERE AND NOT IN realm-export.json, AND THAT IS THE WHOLE POINT (DEF-049). Keycloak
# imports the bundled realm export ONLY on first run, so a client declared there reaches a fresh
# stack and NEVER appears on prod or UAT, whose realms already exist — the same seam that produced
# DEF-023 (redirect URIs) and CHANGE-004 (the `basic` scope). The client SECRET has the identical
# problem, which is what settles it: a secret can only be made to match gen-secrets' file by being
# SET from that file, so the client cannot be fully expressed in the export at all. One definition,
# applied everywhere, every boot.
#
# The grant is read from admin-client.env rather than written here, so the CI gate
# (scripts/check-realm-export.mjs) and this reconciler cannot drift — see OQ-070 for why the set is
# exactly `manage-users` and why the obvious guess was wider.
ensure_admin_client() {
  local secret_file="$1" cid svc rm want have missing extra
  # shellcheck disable=SC1091
  . /opt/keycloak/admin-client.env

  cid=$("$K" get clients -r "$REALM" -q "clientId=$ACMP_KC_ADMIN_CLIENT_ID" --fields id --format csv --noquotes | head -n1 | tr -d '\r')
  if [[ -z "$cid" ]]; then
    echo "[reconcile] creating client '$ACMP_KC_ADMIN_CLIENT_ID'…"
    "$K" create clients -r "$REALM" \
      -s "clientId=$ACMP_KC_ADMIN_CLIENT_ID" \
      -s enabled=true \
      -s publicClient=false \
      -s serviceAccountsEnabled=true \
      -s standardFlowEnabled=false \
      -s directAccessGrantsEnabled=false \
      -s "description=ADR-0038 in-app user management (invite, role assignment, forced sign-out, guest disable)" >/dev/null
    cid=$("$K" get clients -r "$REALM" -q "clientId=$ACMP_KC_ADMIN_CLIENT_ID" --fields id --format csv --noquotes | head -n1 | tr -d '\r')
  fi
  [[ -n "$cid" ]] || { echo "[reconcile] ERROR: could not create or find '$ACMP_KC_ADMIN_CLIENT_ID'" >&2; return 1; }

  # Re-assert the flags on every run: a client created by hand (the OQ-070 probe created one on UAT)
  # or loosened later must converge back, or "confidential, service-account-only" is a claim rather
  # than a state.
  "$K" update "clients/$cid" -r "$REALM" \
    -s publicClient=false -s serviceAccountsEnabled=true \
    -s standardFlowEnabled=false -s directAccessGrantsEnabled=false >/dev/null

  # The secret comes FROM THE FILE, never the other way round. Reading Keycloak's generated secret
  # back out is how it ends up in a log — the OQ-070 probe did exactly that and had to be rotated.
  "$K" update "clients/$cid" -r "$REALM" -s "secret=$(cat "$secret_file")" >/dev/null
  echo "[reconcile] '$ACMP_KC_ADMIN_CLIENT_ID' is confidential, service-account-only, secret set from the mounted file."

  svc=$("$K" get "clients/$cid/service-account-user" -r "$REALM" --fields id --format csv --noquotes | head -n1 | tr -d '\r')
  rm=$("$K" get clients -r "$REALM" -q "clientId=realm-management" --fields id --format csv --noquotes | head -n1 | tr -d '\r')
  [[ -n "$svc" && -n "$rm" ]] || { echo "[reconcile] ERROR: service account ($svc) or realm-management ($rm) not found" >&2; return 1; }

  have=$("$K" get "users/$svc/role-mappings/clients/$rm" -r "$REALM" --fields name --format csv --noquotes | tr -d '\r' | grep -v '^$' || true)
  want="$ACMP_KC_ADMIN_ROLES"

  # REMOVE FIRST, and remove anything not wanted — including realm-admin. Adding without removing
  # would make this a grant-only reconciler, so a widened grant (the likely "fix" for a 403, see
  # DEF-048) would survive every subsequent deploy silently.
  extra=""
  for r in $have; do
    case " $want " in *" $r "*) ;; *) extra="$extra $r"; "$K" remove-roles -r "$REALM" --uid "$svc" --cclientid realm-management --rolename "$r" ;; esac
  done
  [[ -n "$extra" ]] && echo "[reconcile] REVOKED non-minimal grant(s):$extra"

  missing=""
  for r in $want; do
    case " $have " in *" $r "*) ;; *) missing="$missing $r"; "$K" add-roles -r "$REALM" --uid "$svc" --cclientid realm-management --rolename "$r" ;; esac
  done
  [[ -n "$missing" ]] && echo "[reconcile] granted:$missing"

  # Read back rather than trust the exit codes — DEF-023 is a defect that every green check missed.
  have=$("$K" get "users/$svc/role-mappings/clients/$rm" -r "$REALM" --fields name --format csv --noquotes | tr -d '\r' | grep -v '^$' | sort | tr '\n' ' ')
  local expect
  expect=$(printf '%s\n' $want | sort | tr '\n' ' ')
  if [[ "$have" != "$expect" ]]; then
    echo "[reconcile] ERROR: grant did not converge. want='$expect' have='$have'" >&2
    return 1
  fi
  echo "[reconcile] realm-management grant is exactly: $have"
}

# Gated on the secret being present, not on an ENABLED flag: the client and its grant are cheap and
# inert (a service account nobody authenticates as does nothing), and reconciling them even while
# KeycloakAdmin:Enabled is false is what makes enabling the feature a ONE-VARIABLE change. That
# ordering matters — KeycloakAdminOptions is ValidateOnStart, so a flag that arrives before its
# secret STOPS THE HOST rather than degrading (DW-024).
if [[ -f /run/secrets/KeycloakAdmin__ClientSecret ]]; then
  ensure_admin_client /run/secrets/KeycloakAdmin__ClientSecret
else
  echo "[reconcile] KeycloakAdmin__ClientSecret not mounted — skipping the ADR-0038 service account."
fi

echo "[reconcile] done."
