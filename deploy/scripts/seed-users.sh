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
ENV_FILE="${ACMP_ENV_FILE:-$ROOT/deploy/.env}"
[ -f "$ENV_FILE" ] || ENV_FILE="$ROOT/deploy/.env.example"

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

api() { # method path [json]
  local m="$1" p="$2" body="${3:-}"
  if [ -n "$body" ]; then
    curl -sS -X "$m" "$KC_URL/admin/realms/$REALM$p" \
      -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' -d "$body" -w '\n%{http_code}'
  else
    curl -sS -X "$m" "$KC_URL/admin/realms/$REALM$p" \
      -H "Authorization: Bearer $TOKEN" -w '\n%{http_code}'
  fi
}

log "authenticating to $KC_URL as $KC_ADMIN"
TOKEN="$(curl -sS -X POST "$KC_URL/realms/master/protocol/openid-connect/token" \
  -d 'grant_type=password' -d 'client_id=admin-cli' \
  --data-urlencode "username=$KC_ADMIN" --data-urlencode "password=$KC_PW" \
  | sed -n 's/.*"access_token":"\([^"]*\)".*/\1/p')"
[ -n "$TOKEN" ] || die "could not obtain an admin token -- check KC_BOOTSTRAP_ADMIN_* and that Keycloak is up"

# Fail before creating anything if a requested role does not exist, so a typo cannot leave the
# realm half-seeded with role-less accounts.
existing_roles="$(api GET /roles | sed '$d')"
for pair in $SEED_USERS; do
  role="${pair##*:}"
  printf '%s' "$existing_roles" | grep -q "\"name\":\"$role\"" \
    || die "realm role '$role' does not exist in realm '$REALM' -- fix ACMP_SEED_USERS"
done
log "all requested roles exist"

created=0; skipped=0
for pair in $SEED_USERS; do
  user="${pair%%:*}"; role="${pair##*:}"

  found="$(api GET "/users?username=$user&exact=true" | sed '$d')"
  uid="$(printf '%s' "$found" | sed -n 's/.*"id":"\([^"]*\)".*/\1/p' | head -1)"

  if [ -n "$uid" ]; then
    log "user '$user' already exists ($uid) -- leaving it untouched"
    skipped=$((skipped+1))
  else
    # requiredActions UPDATE_PASSWORD + temporary:true is the whole point: the seeded secret is a
    # one-time hand-off, never the account's real credential.
    resp="$(api POST /users "{\"username\":\"$user\",\"enabled\":true,\"emailVerified\":false,
      \"email\":\"$user@example.invalid\",\"firstName\":\"$user\",\"lastName\":\"Seeded\",
      \"requiredActions\":[\"UPDATE_PASSWORD\"],
      \"credentials\":[{\"type\":\"password\",\"value\":\"$TEMP_PW\",\"temporary\":true}]}")"
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
  api POST "/users/$uid/role-mappings/realm" "[{\"id\":\"$rid\",\"name\":\"$role\"}]" >/dev/null

  # VERIFY rather than trust the 2xx -- a role that did not stick produces a user who logs in and
  # then sees an empty application, which reads as a broken app rather than a seeding bug.
  assigned="$(api GET "/users/$uid/role-mappings/realm" | sed '$d')"
  printf '%s' "$assigned" | grep -q "\"name\":\"$role\"" \
    || die "role '$role' did not stick on '$user'"

  # Equally important: the temporary-password contract must hold on re-runs too, or an
  # already-existing account could quietly be a permanent shared login.
  ra="$(api GET "/users/$uid" | sed '$d')"
  printf '%s' "$ra" | grep -q 'UPDATE_PASSWORD' \
    || log "  WARNING: '$user' has no UPDATE_PASSWORD pending (already changed it, or seeded elsewhere)"

  log "  '$user' -> $role (verified)"
done

log "done: $created created, $skipped already present. Temporary password must be changed at first login."
