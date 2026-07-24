#!/usr/bin/env bash
# ACMP AWS landing-zone — shared config + helpers (PH-5 / SL-020, ADR-0034).
# Sourced by every deploy/aws/NN-*.sh script. Nothing here calls AWS; it only defines
# names, ids and small helpers so the numbered scripts stay short and consistent.
#
# These scripts are OPERATOR-RUN, not CI-run and not run by the assistant. They are
# idempotent (check-then-create), so re-running is safe. Run them in numeric order once,
# from an IAM admin identity (00-account.sh helps you create one) — NOT as root.
set -euo pipefail

# --- account / region / domain (verified against the live account 2026-07-23) ----------
export AWS_PAGER=""                                   # never open a pager in a script
REGION="${AWS_REGION:-us-east-1}"                     # cheapest region (OQ-060, ADR-0034)
export AWS_DEFAULT_REGION="$REGION"
ACCOUNT_ID="${ACMP_ACCOUNT_ID:-565393059398}"
PROJECT="acmp"                                        # resource name prefix + Project tag
HOSTED_ZONE_ID="${ACMP_HOSTED_ZONE_ID:-Z00837029D8Y00HHFWDA}"
DOMAIN="${ACMP_DOMAIN:-anas7ammo.dev}"

# Environments and their public subdomains (subdomains = zero source changes, ADR-0037).
ENVS=(uat prod)
host_for()   { case "$1" in uat) echo "uat.${PROJECT}.${DOMAIN}";; prod) echo "${PROJECT}.${DOMAIN}";; *) die "unknown env '$1'";; esac; }
bucket_for() { echo "${PROJECT}-$1-recordings"; }     # DEF-015: one bucket per env
backup_bucket_for() { echo "${PROJECT}-$1-backups"; }

# Common resource tags — Project=ACMP is the selector the budget auto-stop action targets.
TAGS="Key=Project,Value=ACMP Key=ManagedBy,Value=deploy-aws-scripts"

# Spend controls (RISK-022). Ceiling used for the budget thresholds in 00-account.sh.
BUDGET_LIMIT_USD="${ACMP_BUDGET_LIMIT_USD:-60}"
ALERT_EMAIL="${ACMP_ALERT_EMAIL:-}"                   # set in your shell before running 00

# --- helpers ---------------------------------------------------------------------------
log()  { printf '[aws %s] %s\n' "$(date +%H:%M:%S)" "$*"; }
die()  { printf '[aws ERROR] %s\n' "$*" >&2; exit 1; }
have() { command -v "$1" >/dev/null 2>&1; }

require_aws() {
  have aws || die "aws CLI not found on PATH"
  local who; who="$(aws sts get-caller-identity --query Arn --output text 2>/dev/null)" \
    || die "not authenticated — run 'aws login' / configure credentials first"
  log "identity: $who"
  case "$who" in
    *:root) printf '[aws WARN] you are running as ROOT. Run 00-account.sh first and switch to the IAM admin.\n' >&2 ;;
  esac
}

# Print a value only if non-empty; used to surface generated credentials to the operator.
emit_secret() { # label value
  printf '\n  >>> %s (put in deploy/.env, git-ignored — shown once):\n      %s\n\n' "$1" "$2"
}
