#!/usr/bin/env bash
# 08 — put the deployment onto an instance and start it (P24 part 2, SL-024/SL-025).
#
#   bash deploy/aws/08-bootstrap-box.sh <uat|prod> <commit-sha>
#
# ############################################################################################
# # UNVERIFIED. This script has NEVER RUN, because no instance exists yet. Everything below   #
# # is reasoned from the live API shapes and the compose file, not observed. Treat its first  #
# # run as a test OF THIS SCRIPT, not as a deployment. That warning is not boilerplate: the   #
# # two worst defects of this phase, DEF-019 and DEF-020, were both in deploy code that had   #
# # been carefully written, reviewed and never executed, and both would have surfaced         #
# # mid-deploy on a paid box. Run it, expect it to be wrong somewhere, and fix it there.      #
# ############################################################################################
#
# DELIVERY MECHANISM: SSM Run Command, not SSH (OQ-063). The instances have no inbound SSH rule
# and the security groups are 443-only, so SSM is the only way in. It also means no key material
# to distribute or rotate, and every command is logged in the SSM history.
#
# WHAT GOES ON THE BOX AND WHAT DOES NOT
#   * the repository at a PINNED COMMIT -- cloned on the box rather than copied from here, so what
#     runs is a named revision anyone can check out, not an untracked pile of files someone rsynced.
#   * the environment file, ASSEMBLED ON THE BOX from SSM Parameter Store SecureStrings. Secrets
#     are deliberately NOT passed as Run Command parameters: those are visible in the SSM command
#     history to anyone with ssm:GetCommandInvocation, which would put database and Keycloak
#     passwords into an audit log that is not a secret store.
#   * NOT the images -- those are pulled from ECR by the instance profile (P20), never built on the
#     box; npm ci and dotnet publish need ~2 GiB each and would OOM a 4 GiB host.
set -euo pipefail
. "$(dirname "$0")/_common.sh"
require_aws

env_name="${1:-}"; sha="${2:-}"
case "$env_name" in uat|prod) ;; *) die "usage: 08-bootstrap-box.sh <uat|prod> <commit-sha>";; esac
[ -n "$sha" ] || die "usage: 08-bootstrap-box.sh <uat|prod> <commit-sha>"
repo_url="${ACMP_REPO_URL:-https://github.com/A-H-911/acmp.git}"
name="${PROJECT}-${env_name}"

instance_id="$(aws ec2 describe-instances --region "$REGION" \
  --filters "Name=tag:Name,Values=$name" "Name=instance-state-name,Values=running" \
  --query 'Reservations[0].Instances[0].InstanceId' --output text 2>/dev/null)"
[ -n "$instance_id" ] && [ "$instance_id" != "None" ] || die "no RUNNING instance tagged $name — run 07-launch.sh first"
log "target $instance_id ($env_name), pinning commit $sha"

# The parameter path the box reads its environment from. Populate it once, out of band:
#   aws ssm put-parameter --type SecureString --name /acmp/uat/env --value "$(cat my.env)"
param="/acmp/${env_name}/env"
aws ssm get-parameter --region "$REGION" --name "$param" --with-decryption >/dev/null 2>&1 \
  || die "SSM parameter $param not found — create it (SecureString) with this environment's .env content first"

# Heredoc is quoted so NOTHING here expands locally; the only interpolated values are the three
# injected explicitly below. An unquoted heredoc would expand this host's variables into the
# remote script, which is how a local shell's state silently leaks into a deployment.
remote="$(cat <<'REMOTE'
set -euo pipefail
exec 2>&1
command -v docker >/dev/null || { dnf install -y docker; systemctl enable --now docker; }
docker compose version >/dev/null 2>&1 || {
  mkdir -p /usr/local/lib/docker/cli-plugins
  curl -fsSL https://github.com/docker/compose/releases/latest/download/docker-compose-linux-x86_64 \
    -o /usr/local/lib/docker/cli-plugins/docker-compose
  chmod +x /usr/local/lib/docker/cli-plugins/docker-compose
}
command -v git >/dev/null || dnf install -y git

install -d -m 0755 /opt/acmp
if [ -d /opt/acmp/.git ]; then git -C /opt/acmp fetch --all --tags --quiet; else git clone --quiet "__REPO__" /opt/acmp; fi
git -C /opt/acmp checkout --quiet "__SHA__"
cd /opt/acmp

# The .bak path DEF-017 requires: SQL Server writes the backup itself as uid 10001, and Docker
# would otherwise create this mount root-owned 0755 and every BACKUP DATABASE would fail at 02:00
# in a cron log while the deployment looks backed up.
install -d -o 10001 -g 10001 -m 0755 /opt/acmp-backups

umask 077
aws ssm get-parameter --name "__PARAM__" --with-decryption --query Parameter.Value --output text > deploy/.env.cloud
ACMP_ENV_FILE=/opt/acmp/deploy/.env.cloud sh deploy/scripts/gen-secrets.sh

aws ecr get-login-password --region "__REGION__" | docker login --username AWS --password-stdin "__REGISTRY__"
docker compose -f deploy/docker-compose.cloud.yml --env-file deploy/.env.cloud pull
docker compose -f deploy/docker-compose.cloud.yml --env-file deploy/.env.cloud up -d
docker compose -f deploy/docker-compose.cloud.yml --env-file deploy/.env.cloud ps
REMOTE
)"
remote="${remote//__REPO__/$repo_url}"
remote="${remote//__SHA__/$sha}"
remote="${remote//__PARAM__/$param}"
remote="${remote//__REGION__/$REGION}"
remote="${remote//__REGISTRY__/${ACCOUNT_ID}.dkr.ecr.${REGION}.amazonaws.com}"

log "sending bootstrap via SSM (this takes several minutes on a cold box)"
cmd_id="$(aws ssm send-command --region "$REGION" --instance-ids "$instance_id" \
  --document-name AWS-RunShellScript --comment "acmp bootstrap $env_name $sha" \
  --parameters "commands=[$(printf '%s' "$remote" | python -c 'import json,sys; print(json.dumps(sys.stdin.read()))')]" \
  --query 'Command.CommandId' --output text)"
log "command $cmd_id — waiting"
aws ssm wait command-executed --region "$REGION" --command-id "$cmd_id" --instance-id "$instance_id" 2>/dev/null || true
status="$(aws ssm get-command-invocation --region "$REGION" --command-id "$cmd_id" --instance-id "$instance_id" --query Status --output text)"
aws ssm get-command-invocation --region "$REGION" --command-id "$cmd_id" --instance-id "$instance_id" \
  --query StandardOutputContent --output text | tail -40
[ "$status" = "Success" ] || die "bootstrap finished with status $status — see the output above"
log "bootstrap Success. Next: 05-route53.sh, then certbot for TLS (AC-081)."
