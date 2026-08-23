#!/usr/bin/env bash
# 09 — publish an environment's .env to SSM Parameter Store as a SecureString (SL-025).
#
#   bash deploy/aws/09-put-env.sh <uat|prod> <path-to-env-file>
#
# WHY THIS EXISTS AT ALL
# 08-bootstrap-box.sh reads /acmp/<env>/env with --with-decryption ON THE BOX and dies if it is
# absent. Nothing ever created that parameter — the only instruction in the repo was a comment
# inside 08 saying "populate it once, out of band". So the delivery mechanism for the entire cloud
# deployment had no first step, and `aws ssm describe-parameters` confirms /acmp/* is empty.
#
# WHY THE OBVIOUS ONE-LINER DOES NOT WORK
# `put-parameter --value "$(cat deploy/.env.cloud)"` FAILS. A Standard-tier parameter caps at 4 KB
# and deploy/.env.cloud.example is 5,483 bytes — the file is mostly the comments that make it
# readable. Advanced tier would take 8 KB but costs $0.05/parameter/month and, with the account
# default tier set to Intelligent-Tiering, an oversized value is silently PROMOTED to Advanced and
# starts billing with no error. Stripping full-line comments and blank lines takes the same content
# to ~1.1 KB, which fits the free tier with room for real secret values. The box keeps the documented
# template anyway: 08 clones the repo, so deploy/.env.cloud.example travels with it.
#
# --tier Standard is passed EXPLICITLY so an oversized payload fails loudly here instead of quietly
# becoming a billed Advanced parameter.
set -euo pipefail
# SSM parameter names start with "/", which MSYS/Git Bash rewrites into a Windows filesystem path
# before the native aws.exe ever sees it — PutParameter then rejects it with "Parameter name must be
# a fully qualified name", an error that says nothing about the actual cause. Harmless on Linux,
# where the variable is simply ignored.
export MSYS_NO_PATHCONV=1
. "$(dirname "$0")/_common.sh"
# NOTE: require_aws is deliberately NOT called here. Every check below is on the FILE, so an
# operator can validate an env file offline — and a credentials error should never be what you get
# back when the real problem is a CHANGE_ME you forgot to fill in.

env_name="${1:-}"
src="${2:-}"
case "$env_name" in uat|prod) ;; *) die "usage: 09-put-env.sh <uat|prod> <path-to-env-file>";; esac
[ -n "$src" ] || die "usage: 09-put-env.sh <env> <path-to-env-file>"
[ -f "$src" ] || die "env file not found: $src"

param="/${PROJECT}/${env_name}/env"

# Strip FULL-LINE comments and blank lines ONLY. Never touch a trailing "#": a generated password
# can legitimately contain one, and stripping inline would corrupt the secret rather than the file.
payload="$(sed -e '/^[[:space:]]*#/d' -e '/^[[:space:]]*$/d' "$src")"
bytes="$(printf '%s' "$payload" | wc -c | tr -d ' ')"

# --- refuse an unfinished env file -------------------------------------------------------------
# The template ships CHANGE_ME for every password. Publishing one produces a stack that boots and
# then fails somewhere unhelpful (gen-secrets rejects the Seq hash; SQL Server rejects the sa
# password) long after the operator has stopped watching. Catch it before it reaches the box.
if printf '%s' "$payload" | grep -q 'CHANGE_ME'; then
  printf '%s\n' "$payload" | grep -n 'CHANGE_ME' >&2
  die "$src still contains CHANGE_ME placeholders (listed above) — fill them in before publishing"
fi

# --- cross-environment isolation for Webex (AC-083, OQ-062) ------------------------------------
# AC-083 requires that "UAT Webex posts never reach the prod space", and OQ-062 decided (2026-07-24)
# that Webex stays OFF in UAT until a separate UAT space and bot exist. Until now that was true only
# because the TEMPLATE happens to say so — PE-148 recorded the leg as satisfied "only at template
# level ... a template is not a deployed configuration". This is the deployed configuration, so the
# guarantee belongs here: a UAT box physically cannot be handed Webex credentials.
if [ "$env_name" = "uat" ]; then
  enabled="$(printf '%s\n' "$payload" | sed -n 's/^[[:space:]]*WEBEX_ENABLED[[:space:]]*=[[:space:]]*//p' | tr -d '\r' | tail -1)"
  case "${enabled:-false}" in
    false|False|FALSE) ;;
    *) die "AC-083: WEBEX_ENABLED must be false in uat (found '${enabled}') — OQ-062 keeps Webex off in UAT until it has its own space and bot";;
  esac
  if leaked="$(printf '%s\n' "$payload" | grep -E '^[[:space:]]*WEBEX_(SPACE_ID|BOT_TOKEN|WEBHOOK_SECRET|OAUTH_[A-Z_]+|TOKEN_ENCRYPTION_KEY)[[:space:]]*=[[:space:]]*[^[:space:]]' || true)"; [ -n "$leaked" ]; then
    printf '%s\n' "$leaked" >&2
    die "AC-083: uat env carries Webex credentials (above) — these must stay unset so UAT cannot post into the prod space"
  fi
fi

# --- in-app user management preflight (ADR-0038, DW-024, DEF-055) ------------------------------
# WHAT THIS ACTUALLY ENFORCES: no GUESSABLE OR PLACEHOLDER credential reaches a machine. reconcile.sh
# pushes this value to the Keycloak client, so shipping a published literal like CHANGE_ME would have
# both sides agree on a credential anyone can read. This is the last point before the value reaches a
# machine, so it is refused HERE.
#
# ⚠ IT DOES NOT ENFORCE "the host would not boot", AND THE MESSAGE USED TO SAY SO (DEF-055). For the
# ABSENT case that claim was simply false: gen-secrets.sh writes KeycloakAdmin__ClientSecret
# UNCONDITIONALLY, generating a random value when the variable is unset, compose mounts it as a real
# secret, and AddKeyPerFile binds it — so ValidateOnStart PASSES and the host boots fine. An operator
# told the wrong reason looks in the wrong place.
#
# The behaviour is deliberately UNCHANGED (DEC-047): an unpinned secret silently rotates on every
# boot, so pinning one is what an operator wants regardless. The honest cost, now stated out loud, is
# that enabling this feature in a deployed environment is TWO variables, not one.
kc_admin_enabled="$(printf '%s\n' "$payload" | sed -n 's/^[[:space:]]*KEYCLOAK_ADMIN_ENABLED[[:space:]]*=[[:space:]]*//p' | tr -d '\r' | tail -1)"
case "${kc_admin_enabled:-false}" in
  false|False|FALSE) ;;
  *)
    kc_admin_secret="$(printf '%s\n' "$payload" | sed -n 's/^[[:space:]]*KEYCLOAK_ADMIN_CLIENT_SECRET[[:space:]]*=[[:space:]]*//p' | tr -d '\r' | tail -1)"
    case "${kc_admin_secret:-}" in
      ''|CHANGE_ME|ChangeMe_KCAdmin#2026)
        die "KEYCLOAK_ADMIN_ENABLED is '${kc_admin_enabled}' but KEYCLOAK_ADMIN_CLIENT_SECRET is '${kc_admin_secret:-<unset>}' — this environment must pin a real secret, because reconcile.sh pushes this value to the Keycloak client and a placeholder would publish the credential. Enabling the feature is TWO variables, not one: set KEYCLOAK_ADMIN_CLIENT_SECRET to a strong value (an unset one is generated fresh on every boot, so it would not stay valid), or leave the feature off.";;
    esac
    ;;
esac

# --- size gate ----------------------------------------------------------------------------------
# 4096 is the hard Standard-tier limit. Fail here with the number rather than letting put-parameter
# return a less obvious error, or letting Intelligent-Tiering promote it to a billed Advanced tier.
if [ "$bytes" -gt 4096 ]; then
  die "payload is ${bytes} B, over the 4096 B Standard-tier limit. Trim the env file — do NOT switch to Advanced tier (it bills \$0.05/parameter/month)"
fi

# Everything above is offline validation; credentials are only needed from here on.
require_aws
log "publishing $param  (${bytes} B of $(printf '%s' "$payload" | grep -c . ) settings, Standard tier)"
# The value is passed as an argument rather than a temp file: the plaintext already sits at $src on
# this machine, so a temp copy adds exposure without removing any, and this way nothing is left
# behind if the command is interrupted.
aws ssm put-parameter --region "$REGION" \
  --name "$param" --type SecureString --tier Standard --overwrite \
  --value "$payload" \
  --description "ACMP ${env_name} runtime environment (published by deploy/aws/09-put-env.sh)" >/dev/null

log "published $param"
log "verify from the box's identity later with: aws ssm get-parameter --name $param --with-decryption"
