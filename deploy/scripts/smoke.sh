#!/usr/bin/env bash
# ACMP deployment smoke check (SL-027). Verifies a DEPLOYED environment from OUTSIDE it.
#
#   bash deploy/scripts/smoke.sh acmp.anas7ammo.dev
#   ACMP_S3_BUCKET=acmp-prod-recordings bash deploy/scripts/smoke.sh acmp.anas7ammo.dev   # + S3 leg
#
# WHY THIS EXISTS AND WHY IT IS NOT THE PLAYWRIGHT SUITE. SL-027 listed both; only the suite was
# built. The suite is the wrong tool for production and always was: global-setup seeds fixed-password
# accounts whose password is committed in e2e/users.ts, and core-loop.spec.ts submits a topic and
# schedules a meeting. ACMP audit events are hash-chained and append-only (INV-005) and a
# CommitteeMember can be deactivated but never deleted (DEF-029), so nothing the suite writes to a
# production system of record can be undone. PE-185 recorded that the suite "must stay incapable of
# pointing at production"; that refusal now exists in e2e/global-setup.ts, and THIS is what replaces
# it for prod.
#
# EVERY CHECK HERE IS NON-DESTRUCTIVE. Nothing touches the governance data. The only write is the
# optional S3 leg, which puts and then deletes one throwaway object in the recordings bucket — that
# is object storage, not the record, and it is the only way to prove the presign path end to end.
#
# It reads responses rather than exit codes wherever it can. This project has been bitten repeatedly
# by checks that pass for the wrong reason: a bootstrap that reported Success with every container
# unhealthy (DEF-024), health probes green on a box nobody could log into (DEF-023), and a certbot
# run whose SSM invocation said Success while no certificate had been issued.
set -uo pipefail

HOST="${1:-}"
[ -n "$HOST" ] || { echo "usage: smoke.sh <host>   e.g. smoke.sh acmp.anas7ammo.dev" >&2; exit 2; }
BASE="https://$HOST"
fails=0
pass() { printf '  \033[32mOK\033[0m    %s\n' "$*"; }
fail() { printf '  \033[31mFAIL\033[0m  %s\n' "$*"; fails=$((fails + 1)); }
note() { printf '        %s\n' "$*"; }

echo "--- ACMP smoke: $BASE ---"

# 1) DNS -----------------------------------------------------------------------------------------
# The awk here skips everything before the "Name:" line ON PURPOSE. nslookup prints the RESOLVER's
# address first, so the naive `/^Address: /{print $2; exit}` returns the DNS server's IP and reports
# it as the host's — it printed 103.86.96.100 for a host that is actually 52.23.105.56, and "OK" with
# a plausible-looking address is the worst possible failure for a check nobody re-reads.
ip="$(getent hosts "$HOST" 2>/dev/null | awk '{print $1; exit}')"
[ -n "$ip" ] || ip="$(nslookup "$HOST" 2>/dev/null | tr -d '\r' | awk '/^Name:/{seen=1} seen && /^Address(es)?: /{print $2; exit}')"
if [ -n "$ip" ]; then pass "DNS $HOST -> $ip"; else fail "DNS $HOST does not resolve"; fi

# 2) TLS ------------------------------------------------------------------------------------------
# The SUBJECT is read, not just the handshake. A box between bootstrap and certbot serves a
# self-signed CN=ACMP-PLACEHOLDER-DO-NOT-TRUST so nginx can start at all; it terminates TLS
# perfectly well and every browser rejects it. A handshake-only check calls that a pass.
cert="$(echo | openssl s_client -connect "$HOST:443" -servername "$HOST" 2>/dev/null \
        | openssl x509 -noout -subject -issuer -enddate 2>/dev/null)"
if [ -z "$cert" ]; then
  fail "TLS: no certificate presented"
else
  subj="$(printf '%s' "$cert" | sed -n 's/^subject=//p')"
  issuer="$(printf '%s' "$cert" | sed -n 's/^issuer=//p')"
  end="$(printf '%s' "$cert" | sed -n 's/^notAfter=//p')"
  case "$subj" in
    *PLACEHOLDER*) fail "TLS: still the BOOTSTRAP PLACEHOLDER ($subj) — certbot has not run" ;;
    *"$HOST"*)     pass "TLS subject $subj" ;;
    *)             fail "TLS subject does not match host: $subj" ;;
  esac
  note "issuer: $issuer"
  end_epoch="$(date -u -d "$end" +%s 2>/dev/null || echo 0)"
  if [ "$end_epoch" -gt 0 ]; then
    days=$(( (end_epoch - $(date -u +%s)) / 86400 ))
    if [ "$days" -lt 14 ]; then fail "TLS expires in ${days}d ($end) — renewal is not working"
    else pass "TLS valid ${days}d ($end)"; fi
  fi
fi

code() { curl -s -o /dev/null -w '%{http_code}' --max-time 25 "$1"; }

# 3) HSTS -----------------------------------------------------------------------------------------
if curl -sI --max-time 25 "$BASE/" | grep -qi '^strict-transport-security:'; then
  pass "HSTS present"
else
  fail "HSTS header missing"
fi

# 4) App + API ------------------------------------------------------------------------------------
# NOT /api/healthz. The runbooks tell you to curl that and expect 200; it has never been reachable in
# this topology and returns 404. `proxy_pass http://api:8080;` carries NO trailing slash, so the URI
# passes through unchanged and the API — which maps health at ROOT /healthz — never sees a match. The
# health endpoints are internal by design: the container healthcheck hits them inside the network.
#
# NOR /healthz DIRECTLY, which is the trap that makes this worth spelling out. It returns 200 from
# OUTSIDE — but the body is index.html, because the SPA location has a try_files fallback that
# answers 200 for ANY unmatched path. A nonexistent path returns exactly the same thing. So a
# /healthz check would pass with the API stone dead.
#
# A PROTECTED ROUTE RETURNING 401 IS THE REAL LIVENESS SIGNAL, and it asserts three things at once:
# nginx routes /api/ to the API, the API process is up and answering, and authorization is switched
# on. 200 there would be a security failure, not a pass; 404 means the SPA fallback swallowed it;
# 502/504 means the API is down.
got="$(code "$BASE/")"
if [ "$got" = "200" ]; then pass "SPA / -> 200"; else fail "SPA / -> $got (want 200)"; fi

for path in /api/members /api/topics; do
  got="$(code "$BASE$path")"
  case "$got" in
    401) pass "API $path -> 401 (reachable AND protected)" ;;
    200) fail "API $path -> 200 UNAUTHENTICATED — authorization is not being enforced" ;;
    404) fail "API $path -> 404 — not routed to the API (SPA fallback swallowed it)" ;;
    *)   fail "API $path -> $got (want 401)" ;;
  esac
done

# 5) Keycloak reachability AND the admin deny (AC-081) --------------------------------------------
# Both halves matter. Discovery must be reachable or nobody can log in; /kc/admin and the master
# realm must NOT be, and that deny is what AC-076's method had to tunnel around rather than relax.
for probe in "/kc/realms/acmp/.well-known/openid-configuration:200:realm discovery" \
             "/kc/admin:404:admin console DENIED" \
             "/kc/realms/master:404:master realm DENIED"; do
  path="${probe%%:*}"; rest="${probe#*:}"; want="${rest%%:*}"; label="${rest#*:}"
  got="$(code "$BASE$path")"
  if [ "$got" = "$want" ]; then pass "$label -> $got"; else fail "$label -> $got (want $want)"; fi
done

# 6) S3 round trip, opt-in ------------------------------------------------------------------------
# Skipped LOUDLY rather than silently: DEF-022 was a capability that skipped itself quietly and
# reported success, and the cure for that is not another silent skip.
if [ -n "${ACMP_S3_BUCKET:-}" ]; then
  key="smoke/$(date -u +%Y%m%dT%H%M%SZ)-$$.txt"
  tmp="$(mktemp)"; echo "acmp smoke $(date -u)" > "$tmp"
  if aws s3api put-object --bucket "$ACMP_S3_BUCKET" --key "$key" --body "$tmp" >/dev/null 2>&1; then
    url="$(aws s3 presign "s3://$ACMP_S3_BUCKET/$key" --expires-in 120 2>/dev/null)"
    got="$(code "$url")"
    if [ "$got" = "200" ]; then pass "S3 upload -> presign -> GET 200 ($ACMP_S3_BUCKET)"
    else fail "S3 presigned GET -> $got (want 200)"; fi
    aws s3api delete-object --bucket "$ACMP_S3_BUCKET" --key "$key" >/dev/null 2>&1 \
      && pass "S3 throwaway object deleted" || fail "S3 delete failed — $key left behind"
  else
    fail "S3 put-object failed on $ACMP_S3_BUCKET"
  fi
  rm -f "$tmp"
else
  note "S3 leg SKIPPED — set ACMP_S3_BUCKET (and AWS credentials) to include it."
fi

echo "---"
if [ "$fails" -eq 0 ]; then echo "SMOKE PASSED for $HOST"; exit 0; fi
echo "SMOKE FAILED for $HOST — $fails check(s) failed"; exit 1
