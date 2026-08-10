#!/usr/bin/env bash
# ACMP committee user seeding (PH-5 / SL-026, AC-079). Creates the initial Keycloak accounts for a
# fresh environment and assigns each one its realm role, so a newly provisioned UAT or production
# box has people who can actually log in.
#
#   bash deploy/scripts/seed-users.sh                          # uses deploy/.env
#   ACMP_ENV_FILE=/path/.env bash deploy/scripts/seed-users.sh
#   ACMP_SEED_USERS="ali:Chairman sara:Secretary" bash deploy/scripts/seed-users.sh
#
# THREE PROPERTIES THIS SCRIPT MUST HAVE, and why each is load-bearing:
#
# 1. IDEMPOTENT. It runs on every provision and re-provision, and a fresh box is often rebuilt
#    several times before it settles. Creating a user that exists must be a no-op, not a 409 that
#    aborts the run half-seeded -- a partially seeded realm is worse than an empty one because it
#    looks done. Every step is check-then-act and re-running changes nothing.
#
# 2. TEMPORARY PASSWORDS ONLY. Each account is created with a temporary password AND the
#    UPDATE_PASSWORD required action, so the seeded secret cannot become the account's real
#    credential. Without it the operator's chosen string silently becomes a permanent shared
#    password for a governance system where every vote is attributed by identity (ADR-0010) --
#    attribution is meaningless if several people can hold one login.
#
# 3. ROLE-CORRECT. ACMP maps Keycloak realm roles to its own authorization (INV-002); a user seeded
#    without a role logs in successfully and then sees nothing, which reads as a broken app rather
#    than a seeding mistake. The role is verified after assignment, not assumed from a 2xx.
#
# The app JIT-provisions its CommitteeMember row on first login from the token's `sub`, so this
# script deliberately does NOT touch the application database -- Keycloak is the identity source
# and duplicating membership here would create two records that can disagree.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
# A MISSING ACMP_ENV_FILE IS A HARD ERROR, not a quiet fallback. This used to drop to
# .env.example whenever the named file was absent, so a typo'd path or a cloud box (where the file
# is .env.cloud) got template values instead — and the only symptom was "could not obtain an admin
# token" fifty lines later, which reads as "Keycloak is down". Third instance of this shape here
# after DEF-022 (backup.sh) and DEF-033 (check-backup-freshness.sh): the fallback IS the bug.
if [ -n "${ACMP_ENV_FILE:-}" ]; then
  ENV_FILE="$ACMP_ENV_FILE"
  [ -f "$ENV_FILE" ] || { printf '[seed-users ERROR] ACMP_ENV_FILE=%s does not exist\n' "$ENV_FILE" >&2; exit 1; }
else
  ENV_FILE="$ROOT/deploy/.env"
  [ -f "$ENV_FILE" ] || ENV_FILE="$ROOT/deploy/.env.example"
fi
# Captured BEFORE the env file is sourced. `set -a; . "$ENV_FILE"` below overwrites the process
# environment, so a value set in the file silently beats the one the operator passed inline — the
# same precedence trap that made backup.sh's failure drill pass for the wrong reason (PE-196).
ROSTER_ARG="${ACMP_SEED_ROSTER:-}"

log()  { printf '[seed-users %s] %s\n' "$(date +%H:%M:%S)" "$*"; }
die()  { printf '[seed-users ERROR] %s\n' "$*" >&2; exit 1; }
have() { command -v "$1" >/dev/null 2>&1; }
have curl || die "curl not found"

set -a; . "$ENV_FILE"; set +a

REALM="${ACMP_REALM:-acmp}"
KC_URL="${ACMP_KC_INTERNAL_URL:-http://localhost:8088/kc}"
KC_ADMIN="${KC_BOOTSTRAP_ADMIN_USERNAME:-admin}"
KC_PW="${KC_BOOTSTRAP_ADMIN_PASSWORD:-}"
[ -n "$KC_PW" ] || die "KC_BOOTSTRAP_ADMIN_PASSWORD is not set in $ENV_FILE"

# username:Role pairs. Roles must exist in the realm (Chairman, Secretary, Member, Reviewer,
# Auditor, Administrator, Submitter, Guest) -- a typo is caught below rather than silently
# producing a user with no role.
SEED_USERS="${ACMP_SEED_USERS:-chairman:Chairman secretary:Secretary member:Member auditor:Auditor}"
TEMP_PW="${ACMP_SEED_TEMP_PASSWORD:-ChangeMe_Acmp#2026}"

# --- roster mode (ACMP_SEED_ROSTER) --------------------------------------------------------------
# The pair syntax above cannot carry a real person: it is whitespace-split, and "Abu Sharkh" has a
# space in it. A real committee needs first name, last name, email and a per-person password, so
# roster mode reads them from the operator's CSV instead. Pair mode is untouched and still the
# default for dev and UAT.
#
# BOTH MODES NORMALISE INTO ONE TAB-SEPARATED RECORD LIST so there is a single seeding loop. Pair
# mode fills the identity columns with exactly the values it used before, so its behaviour is
# unchanged by construction rather than by promise.
if [ -n "${ACMP_SEED_ROSTER:-}" ] && [ "${ACMP_SEED_ROSTER:-}" != "$ROSTER_ARG" ]; then
  die "ACMP_SEED_ROSTER is set in $ENV_FILE -- it must come from the environment, not the env file"
fi
ROSTER="$ROSTER_ARG"
RECORDS="$(mktemp)"; trap 'rm -f "$RECORDS"' EXIT

if [ -n "$ROSTER" ]; then
  [ -f "$ROSTER" ] || die "roster not found: $ROSTER"
  # Strip a UTF-8 BOM and CR endings, drop the header, skip blanks. Fields: username, first, last,
  # email, role, password.
  sed '1s/^\xEF\xBB\xBF//' "$ROSTER" | tr -d '\r' | tail -n +2 | while IFS=, read -r u f l e r p rest; do
    [ -n "${u:-}" ] || continue
    # Markers go to STDOUT so they land in $RECORDS and the checks below can actually see them.
    # Written to stderr they would be invisible to grep and every guard would pass vacuously.
    case "$u$f$l$e$r$p" in *'"'*|*'\'*) printf 'BADFIELD\t%s\n' "$u"; continue;; esac
    [ -n "${p:-}" ] || { printf 'NOPASSWORD\t%s\n' "$u"; continue; }
    [ -n "${e:-}" ] || { printf 'NOEMAIL\t%s\n' "$u"; continue; }
    printf '%s\t%s\t%s\t%s\t%s\t%s\n' "$u" "$r" "$f" "$l" "$e" "$p"
  done > "$RECORDS"
  grep -q . "$RECORDS" || die "roster '$ROSTER' produced no usable rows"
  ! grep -q '^BADFIELD' "$RECORDS" \
    || die "roster contains a quote or backslash -- refusing to build JSON from it"
  # An empty password cell would create a real account with an empty credential. Refuse, and name
  # the fix, rather than seeding something nobody can log into and nobody can tell is wrong.
  ! grep -q '^NOPASSWORD' "$RECORDS" \
    || die "roster rows have no password ($(grep -c '^NOPASSWORD' "$RECORDS")) -- run deploy/scripts/make-roster-passwords.sh first"
  ! grep -q '^NOEMAIL' "$RECORDS" \
    || die "roster rows have no email ($(grep -c '^NOEMAIL' "$RECORDS"))"
  log "roster mode: $(wc -l < "$RECORDS" | tr -d ' ') row(s) from $ROSTER"
else
  for pair in $SEED_USERS; do
    printf '%s\t%s\t%s\t%s\t%s\t%s\n' \
      "${pair%%:*}" "${pair##*:}" "${pair%%:*}" "Seeded" "${pair%%:*}@example.invalid" "$TEMP_PW"
  done > "$RECORDS"
  log "pair mode: $(wc -l < "$RECORDS" | tr -d ' ') entry(ies)"
fi

_api_call() { # method path [json]
  local m="$1" p="$2" body="${3:-}"
  if [ -n "$body" ]; then
    curl -sS -X "$m" "$KC_URL/admin/realms/$REALM$p" \
      -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' -d "$body" -w '\n%{http_code}'
  else
    curl -sS -X "$m" "$KC_URL/admin/realms/$REALM$p" \
      -H "Authorization: Bearer $TOKEN" -w '\n%{http_code}'
  fi
}

# THE ADMIN TOKEN EXPIRES MID-RUN, AND EVERY SYMPTOM OF THAT LOOKS LIKE SOMETHING ELSE. Keycloak's
# admin-cli access token is short-lived; a real committee is ~26 accounts and each needs several
# calls, so a run over an SSM tunnel comfortably outlives one token. Because every check here greps
# a response BODY, an expired token does not surface as "401" -- it surfaces as "role did not stick",
# as a spurious "no UPDATE_PASSWORD pending" warning, and finally as a hard failure partway through,
# leaving a half-seeded realm. All three were observed on the first 26-user run.
#
# So the token is refreshed on demand: any 401 re-authenticates once and replays the call. A second
# 401 is a real authorization problem and is allowed to fail.
authenticate() {
  TOKEN="$(curl -sS -X POST "$KC_URL/realms/master/protocol/openid-connect/token" \
    -d 'grant_type=password' -d 'client_id=admin-cli' \
    --data-urlencode "username=$KC_ADMIN" --data-urlencode "password=$KC_PW" \
    | sed -n 's/.*"access_token":"\([^"]*\)".*/\1/p')"
  [ -n "$TOKEN" ] || die "could not obtain an admin token -- check KC_BOOTSTRAP_ADMIN_* and that Keycloak is up"
}

api() { # method path [json] -- transparently re-authenticates on 401
  local out code
  out="$(_api_call "$@")"
  code="$(printf '%s' "$out" | tail -1)"
  if [ "$code" = "401" ]; then
    authenticate
    out="$(_api_call "$@")"
  fi
  printf '%s' "$out"
}

log "authenticating to $KC_URL as $KC_ADMIN"
authenticate

# Fail before creating anything if a requested role does not exist, so a typo cannot leave the
# realm half-seeded with role-less accounts.
existing_roles="$(api GET /roles | sed '$d')"
while IFS="$(printf '\t')" read -r user role first last email pw; do
  printf '%s' "$existing_roles" | grep -q "\"name\":\"$role\"" \
    || die "realm role '$role' does not exist in realm '$REALM' -- fix the roster/ACMP_SEED_USERS"
done < "$RECORDS"
log "all requested roles exist"

created=0; skipped=0; placeholders=0
while IFS="$(printf '\t')" read -r user role first last email pw; do
  [ -n "$user" ] || continue

  found="$(api GET "/users?username=$user&exact=true" | sed '$d')"
  uid="$(printf '%s' "$found" | sed -n 's/.*"id":"\([^"]*\)".*/\1/p' | head -1)"

  if [ -n "$uid" ]; then
    log "user '$user' already exists ($uid) -- leaving it untouched"
    skipped=$((skipped+1))
    # An existing account is deliberately NOT rewritten -- it may carry edits this script knows
    # nothing about. But say so when it still looks seeded, because a re-run does NOT repair
    # identity, and "ahammo Seeded" reaching a governance record is the failure roster mode exists
    # to prevent. The app derives a member's displayed name from Keycloak's `name` claim and
    # refreshes it on EVERY login (CommitteeMember.SyncFromClaims), so fixing it in Keycloak and
    # logging in again is the remedy.
    detail="$(api GET "/users/$uid" | sed '$d')"
    case "$detail" in
      *'"lastName":"Seeded"'*|*'@example.invalid'*)
        log "  WARNING: '$user' still has PLACEHOLDER identity -- fix it in Keycloak, then have them log in again"
        placeholders=$((placeholders+1)) ;;
    esac
  else
    # requiredActions UPDATE_PASSWORD + temporary:true is the whole point: the seeded secret is a
    # one-time hand-off, never the account's real credential.
    resp="$(api POST /users "{\"username\":\"$user\",\"enabled\":true,\"emailVerified\":false,
      \"email\":\"$email\",\"firstName\":\"$first\",\"lastName\":\"$last\",
      \"requiredActions\":[\"UPDATE_PASSWORD\"],
      \"credentials\":[{\"type\":\"password\",\"value\":\"$pw\",\"temporary\":true}]}")"
    code="$(printf '%s' "$resp" | tail -1)"
    case "$code" in
      201) log "created '$user'";;
      409) log "user '$user' raced into existence -- treating as present";;
      *)   die "creating '$user' failed with HTTP $code";;
    esac
    uid="$(api GET "/users?username=$user&exact=true" | sed '$d' | sed -n 's/.*"id":"\([^"]*\)".*/\1/p' | head -1)"
    [ -n "$uid" ] || die "created '$user' but cannot resolve its id"
    created=$((created+1))
  fi

  # Assign the role every run, not only on creation: a user that exists with the WRONG role (or
  # none) is the failure this is meant to prevent, and role assignment is itself idempotent.
  role_json="$(api GET "/roles/$role" | sed '$d')"
  rid="$(printf '%s' "$role_json" | sed -n 's/.*"id":"\([^"]*\)".*/\1/p' | head -1)"
  [ -n "$rid" ] || die "could not resolve the realm-role id for '$role'"
  assign_resp="$(api POST "/users/$uid/role-mappings/realm" "[{\"id\":\"$rid\",\"name\":\"$role\"}]")"
  assign_code="$(printf '%s' "$assign_resp" | tail -1)"
  case "$assign_code" in
    200|201|204) ;;
    *) die "assigning '$role' to '$user' failed with HTTP $assign_code" ;;
  esac

  # VERIFY rather than trust the 2xx -- a role that did not stick produces a user who logs in and
  # then sees an empty application, which reads as a broken app rather than a seeding bug.
  #
  # BUT RETRY THE READ. A single failed GET is not evidence that the write did not land, and this
  # check used to treat it as fatal: on the 11th user of a 26-user run over an SSM tunnel it aborted
  # with "role 'Member' did not stick on 'fareeshi'" -- and fareeshi HAD the role when looked at a
  # moment later. The cost of that false negative is a half-seeded realm, which the preflight above
  # exists to prevent and which this then reintroduced at a later stage. The POST status is now
  # checked separately, so a genuine assignment failure still fails immediately and loudly; only the
  # read-back is given room to be flaky.
  stuck=0
  for _ in 1 2 3; do
    assigned="$(api GET "/users/$uid/role-mappings/realm" | sed '$d')"
    if printf '%s' "$assigned" | grep -q "\"name\":\"$role\""; then stuck=1; break; fi
    sleep 2
  done
  [ "$stuck" = 1 ] || die "role '$role' did not stick on '$user' (re-read 3x)"

  # Equally important: the temporary-password contract must hold on re-runs too, or an
  # already-existing account could quietly be a permanent shared login.
  ra="$(api GET "/users/$uid" | sed '$d')"
  printf '%s' "$ra" | grep -q 'UPDATE_PASSWORD' \
    || log "  WARNING: '$user' has no UPDATE_PASSWORD pending (already changed it, or seeded elsewhere)"

  log "  '$user' -> $role (verified)"
done < "$RECORDS"

log "done: $created created, $skipped already present. Temporary password must be changed at first login."
[ "$placeholders" -eq 0 ] \
  || log "WARNING: $placeholders existing account(s) still carry placeholder identity (see above)."
