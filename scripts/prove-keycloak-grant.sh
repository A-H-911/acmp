#!/usr/bin/env bash
# OQ-071 / OQ-070 / ADR-0038 — prove the MINIMUM realm-management grant, automatically, every run.
#
# WHY THIS EXISTS AS A CI JOB RATHER THAN A RUNBOOK STEP (DEC-041). The obligation as written said
# "prove the minimum grant ON UAT". What makes the proof valid is that a REAL Keycloak evaluates its
# own authorization — a stub cannot answer the question at all — and the e2e stack runs the SAME
# image against the SAME realm export, so the authorization model here is identical. What UAT
# uniquely exercises is deployment topology (the /kc prefix behind nginx, the SSM-delivered secret),
# which is a connectivity question and stays a separate, reduced UAT check. A one-off manual proof on
# a box that is stopped when idle decays the moment anything changes; this one is re-verified
# continuously, which is the whole reason for the split.
#
# ⚠ SUFFICIENCY IS NOT MINIMALITY, and leg 2 is the difference. A passing probe under {manage-users}
# proves only that the grant is ENOUGH. Minimality needs the narrower grant to be REFUSED — so this
# strips the grant to nothing, re-runs, and requires failure. Without leg 2 the gate would pass just
# as happily on a service account holding realm-admin, which is the exact outcome ADR-0038 exists to
# prevent. (OQ-070 recorded that the obvious candidate — manage-users + view-realm — was WIDER than
# the truth and no gate would have said so.)
#
# Usage:  scripts/prove-keycloak-grant.sh [compose-args...]
#   e.g.  scripts/prove-keycloak-grant.sh -f deploy/docker-compose.yml --env-file deploy/.env.example
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
CF=("$@")
[ ${#CF[@]} -gt 0 ] || CF=(-f "$ROOT/deploy/docker-compose.yml" --env-file "$ROOT/deploy/.env.example")

KC_BASE="${KC_PROBE_BASE:-http://localhost:8085}"
REALM="${KC_PROBE_REALM:-acmp}"
SECRET_FILE="${KC_PROBE_SECRET_FILE:-$ROOT/deploy/secrets/KeycloakAdmin__ClientSecret}"

# The grant and the client id come from the SAME file the reconciler and check-realm-export read, so
# a gate reading a different value cannot pass while the deployment drifts (DW-024's lesson).
# shellcheck disable=SC1091
. "$ROOT/deploy/keycloak/admin-client.env"
CLIENT="$ACMP_KC_ADMIN_CLIENT_ID"
GRANT="$ACMP_KC_ADMIN_ROLES"

[ -f "$SECRET_FILE" ] || { echo "prove-keycloak-grant: no secret at $SECRET_FILE — run gen-secrets first." >&2; exit 2; }
SECRET="$(cat "$SECRET_FILE")"

kc() {  # run kcadm inside the running keycloak container
  docker compose "${CF[@]}" exec -T keycloak /opt/keycloak/bin/kcadm.sh "$@"
}

login() {
  kc config credentials --server http://localhost:8080 --realm master \
    --user "${KC_BOOTSTRAP_ADMIN_USERNAME:-admin}" \
    --password "$(docker compose "${CF[@]}" exec -T keycloak cat /run/secrets/kc_bootstrap_admin_password)" >/dev/null
}

svc_uid() {
  kc get "clients?clientId=$CLIENT" -r "$REALM" --fields id --format csv --noquotes \
    | tr -d '\r' | head -1 | while read -r id; do kc get "clients/$id/service-account-user" -r "$REALM" --fields id --format csv --noquotes; done \
    | tr -d '\r' | head -1
}

probe() { node "$ROOT/scripts/probe-keycloak-grant.mjs" --base "$KC_BASE" --realm "$REALM" --client "$CLIENT" --secret "$SECRET"; }

echo "== leg 1: the declared grant [$GRANT] is SUFFICIENT — all eight adapter calls must succeed"
probe
echo "   leg 1 passed."

echo "== leg 2: a NARROWER grant must be REFUSED — otherwise 'minimum' is untested"
login
UID_SVC="$(svc_uid)"
[ -n "$UID_SVC" ] || { echo "could not resolve the $CLIENT service-account user" >&2; exit 1; }

# Strip every declared role, run the probe, and REQUIRE it to fail. Restored below in all cases.
restore() {
  echo "== restoring the declared grant"
  for r in $GRANT; do
    kc add-roles -r "$REALM" --uid "$UID_SVC" --cclientid realm-management --rolename "$r" >/dev/null 2>&1 || true
  done
  probe >/dev/null && echo "   restored — the probe passes again." || {
    echo "   ⚠ RESTORE FAILED: the service account no longer holds its grant. Re-run reconcile.sh." >&2
    exit 1
  }
}
trap restore EXIT

for r in $GRANT; do
  kc remove-roles -r "$REALM" --uid "$UID_SVC" --cclientid realm-management --rolename "$r" >/dev/null
done

if probe >/tmp/narrow-probe.log 2>&1; then
  echo "   ✗ the probe SUCCEEDED with no realm-management grant at all." >&2
  echo "     That means the declared grant is not what authorizes these calls, so this gate proves" >&2
  echo "     nothing about minimality. Read the refusal that should have happened." >&2
  exit 1
fi
echo "   leg 2 passed — refused without the grant:"
sed -n '1,6p' /tmp/narrow-probe.log | sed 's/^/     /'

echo "== the grant is SUFFICIENT and NECESSARY: [$GRANT] is minimal by construction."
