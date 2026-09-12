#!/usr/bin/env bash
# NFR-008 authenticated-latency probe — RUNS ON THE BOX, against a deployed uat stack.
#
#   bash deploy/scripts/nfr008-auth-probe.sh
#
# WHY THIS EXISTS AS A COMMITTED SCRIPT RATHER THAN A ONE-OFF (DEC-153, DW-050).
# NFR-008's second clause — "auth check <= 200 ms P95" — is the last of the sixteen NFRs never
# measured. It cannot be measured without a token, and it cannot be measured from the 401 path
# either: a request with NO token is rejected before any signature is verified, so it times the
# rejection and not the check (PE-1012 measured exactly that and said so). The probe therefore
# needs a real token, and obtaining one needs `directAccessGrantsEnabled` on the `acmp-web`
# client, which the realm ships with DISABLED on purpose.
#
# THE WHOLE POINT OF THE SCRIPT IS THAT THE FLIP IS TEMPORARY AND PROVEN TO HAVE BEEN REVERTED.
# It takes three readings, and only the middle one is the measurement:
#     1. BEFORE  — a password grant MUST be refused          (proves the flag gates what we think)
#     2. flip on — a password grant succeeds, measure
#     3. AFTER   — a password grant MUST be refused again    (proves the revert actually took)
# Reading 2 alone would be worthless: without 1 and 3 there is no evidence the flag was ever the
# thing controlling access, nor that it went back. `revert()` is on a `trap`, so it runs on every
# exit path including a failed grant, a bad measurement, or Ctrl-C.
#
# ⛔ SAFETY — IT REFUSES TO RUN ANYWHERE BUT UAT. Enabling a direct password grant on production,
# even for seconds, is not a thing this script is allowed to do by accident. The guard reads the
# Keycloak container's own KC_HOSTNAME rather than trusting an argument, because an argument is
# exactly what gets mistyped at 2am.
#
# ⛔ IT NEVER PRINTS THE TOKEN. Only statuses, timings and the flag's before/after values.
#
# THE CREDENTIAL is `E2E_PASSWORD` from src/Acmp.Web/e2e/users.ts — a fixture published in this
# repository, for accounts the Playwright suite seeds and submits on every CI run. It is not a
# person's password and it is not a secret; the account only exists in non-production realms.
# Override with E2E_USER / E2E_PASSWORD if the fixture is ever rotated.
set -u

KC=acmp-cloud-keycloak-1
WEB=acmp-cloud-web-1
BASE=https://localhost
KCADM=/opt/keycloak/bin/kcadm.sh
REALM=acmp
CLIENT=acmp-web
E2E_USER="${E2E_USER:-e2e-secretary}"
E2E_PASSWORD="${E2E_PASSWORD:-E2e!Passw0rd}"
SAMPLES="${SAMPLES:-40}"

die() { echo "FATAL: $*" >&2; exit 1; }

# ---- guard: uat only -----------------------------------------------------------------------
host="$(docker inspect "$KC" --format '{{range .Config.Env}}{{println .}}{{end}}' 2>/dev/null \
        | sed -n 's/^KC_HOSTNAME=//p' | head -1)"
[ -n "$host" ] || die "cannot read KC_HOSTNAME from $KC — is the stack up?"
case "$host" in
  *uat*) echo "environment guard OK: KC_HOSTNAME=$host" ;;
  *)     die "REFUSING: KC_HOSTNAME=$host does not look like uat. This script enables a password
       grant and must never touch production." ;;
esac

kc() { docker exec "$KC" bash -c "
  $KCADM config credentials --server http://localhost:8080 --realm master \
     --user \"\$KC_BOOTSTRAP_ADMIN_USERNAME\" \
     --password \"\$(cat /run/secrets/kc_bootstrap_admin_password)\" >/dev/null 2>&1 || exit 1
  $*"; }

flag() { kc "$KCADM get clients/$UUID -r $REALM --fields directAccessGrantsEnabled" | tr -d ' \r\n'; }

grant() {  # prints the HTTP status; body lands in /tmp/g.json and is never echoed whole
  curl -sk -o /tmp/g.json -w '%{http_code}' \
    -X POST "$BASE/kc/realms/$REALM/protocol/openid-connect/token" \
    -d grant_type=password -d "client_id=$CLIENT" \
    --data-urlencode "username=$E2E_USER" --data-urlencode "password=$E2E_PASSWORD"
}
grant_error() { grep -o '"error":"[^"]*"' /tmp/g.json || true; }

UUID="$(kc "$KCADM get clients -r $REALM -q clientId=$CLIENT --fields id --format csv --noquotes" \
        | tr -d '\r\n"')"
[ -n "$UUID" ] || die "could not resolve the $CLIENT client uuid"
echo "client $CLIENT uuid=$UUID"
echo "BEFORE flag: $(flag)"

echo
echo "### 1. BEFORE-CONTROL — the grant MUST be refused"
st="$(grant)"; echo "    $E2E_USER -> HTTP $st  $(grant_error)"
[ "$st" = "200" ] && die "the grant SUCCEEDED before the flip — directAccessGrantsEnabled is not
     gating what this probe assumes, so readings 2 and 3 would prove nothing. Stopping."

revert() {
  echo
  echo "### 5. REVERT"
  kc "$KCADM update clients/$UUID -r $REALM -s directAccessGrantsEnabled=false" >/dev/null 2>&1
  echo "    AFTER flag: $(flag)"
  echo "### 6. AFTER-CONTROL — the grant MUST be refused again"
  st="$(grant)"; echo "    $E2E_USER -> HTTP $st  $(grant_error)"
  [ "$st" = "200" ] && echo "    ** REVERT FAILED — the client still accepts a password grant. FIX THIS NOW. **"
  rm -f /tmp/g.json
}
trap revert EXIT

echo
echo "### 2. ENABLE direct grants (temporary)"
kc "$KCADM update clients/$UUID -r $REALM -s directAccessGrantsEnabled=true" >/dev/null 2>&1
echo "    flag now: $(flag)"

echo
echo "### 3. TOKEN"
st="$(grant)"
if [ "$st" != "200" ]; then
  echo "    HTTP $st  $(grant_error)"
  echo "    STOP: no token. 'invalid_grant' means this realm's $E2E_USER password is not the"
  echo "    repository fixture. NOT resetting it — that is a mutation outside this probe's remit."
  exit 1
fi
TOK="$(tr ',' '\n' < /tmp/g.json | grep -o '"access_token":"[^"]*"' | cut -d'"' -f4)"
echo "    HTTP 200, token acquired (len ${#TOK}); never printed"

pct() { sort -n | awk -v p="$1" '{a[NR]=$1} END{if(!NR){print "n/a";exit} i=int(p/100*NR); if(i<1)i=1; printf "%.1f", a[i]*1000}'; }

run() {  # $1=label $2=mode
  local lbl="$1" mode="$2" i r code
  : > /tmp/t
  for i in $(seq 1 "$SAMPLES"); do
    case "$mode" in
      none) r=$(curl -sk -o /dev/null -w '%{http_code} %{time_total}' "$BASE/api/session/me") ;;
      bad)  r=$(curl -sk -o /dev/null -w '%{http_code} %{time_total}' -H "Authorization: Bearer ${TOK}x" "$BASE/api/session/me") ;;
      good) r=$(curl -sk -o /dev/null -w '%{http_code} %{time_total}' -H "Authorization: Bearer $TOK" "$BASE/api/session/me") ;;
      health) r=$(curl -sk -o /dev/null -w '%{http_code} %{time_total}' "$BASE/healthz") ;;
    esac
    echo "${r#* }" >> /tmp/t; code="${r%% *}"
  done
  printf '    %-24s status=%-4s P50=%sms  P95=%sms\n' "$lbl" "$code" "$(pct 50 </tmp/t)" "$(pct 95 </tmp/t)"
}

echo
echo "### 4. MEASURE — GET /api/session/me, $SAMPLES samples each"
# JIT provisioning makes the FIRST authenticated call slow; that is a one-time cost, not steady state.
for i in 1 2 3 4 5; do curl -sk -o /dev/null -H "Authorization: Bearer $TOK" "$BASE/api/session/me"; done
run "no token (expect 401)"       none
run "tampered token (expect 401)" bad
run "valid token (expect 204)"    good
run "unauth /healthz baseline"    health
echo
echo "    The auth cost is (valid token) minus (/healthz): /healthz maps ZERO checks, so its time"
echo "    is pure framework + hop. The TAMPERED row is the one that proves signature validation"
echo "    actually runs — a 401 there costs real crypto; a 401 with no token costs none."

echo
echo "    20-way concurrency, valid token, 3 rounds:"
for round in 1 2 3; do
  : > /tmp/c
  for i in $(seq 1 20); do
    ( curl -sk -o /dev/null -w '%{time_total}\n' -H "Authorization: Bearer $TOK" "$BASE/api/session/me" >> /tmp/c ) &
  done
  wait
  printf '      round %s: P50=%sms P95=%sms (n=%s)\n' "$round" "$(pct 50 </tmp/c)" "$(pct 95 </tmp/c)" "$(wc -l < /tmp/c)"
done
rm -f /tmp/t /tmp/c
