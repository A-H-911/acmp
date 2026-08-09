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
# The SSM parameter name starts with "/", which MSYS/Git Bash rewrites into a Windows path before
# the native aws.exe sees it — the preflight then reports the parameter as MISSING when it exists,
# which sends you off publishing something that is already published. No file:// arguments are used
# here, so disabling conversion is safe; on Linux the variable is ignored.
export MSYS_NO_PATHCONV=1
# The remote output is echoed back through the AWS CLI at the end of this script, and it contains
# non-ASCII (docker compose emits "→" among others). On Windows the CLI's default cp1252 console
# encoding cannot render that and dies with "'charmap' codec can't encode character '→'" —
# killing the script AFTER a bootstrap that succeeded, so a green deployment reports as a failure.
# Found by the AC-075 test. Harmless on Linux, where the locale is already UTF-8.
export PYTHONIOENCODING=utf-8 PYTHONUTF8=1
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

# The parameter path the box reads its environment from. Published by 09-put-env.sh, which strips
# comments to fit the 4 KB Standard-tier limit (a raw .env.cloud is 5,483 B and does NOT fit) and
# enforces the AC-083 Webex rule for uat.
param="/acmp/${env_name}/env"
env_body="$(aws ssm get-parameter --region "$REGION" --name "$param" --with-decryption \
  --query Parameter.Value --output text 2>/dev/null)" \
  || die "SSM parameter $param not found — publish it first:  bash deploy/aws/09-put-env.sh $env_name <path-to-env-file>"

# IMAGE-TAG DRIFT GUARD (found by the AC-075 rebuild). The box checks out the sha passed here, but it
# RUNS whatever ACMP_IMAGE_TAG/ACMP_WEB_TAG the SSM parameter pins — and those were frozen when the
# environment was last published. The first UAT rebuild therefore ran 749071e images against a 2ec8c14
# checkout, and NOTHING detected it. Harmless on a deploy-scripts-only commit; on an app-code commit it
# silently deploys the wrong build and every health check still passes. Same seam class as DEF-019.
#
# This is a GUARD rather than a runbook step on purpose: a runbook step is exactly what the operator
# skips, and the drift is invisible once it has happened.
#
# Compared as a git-style prefix in both directions, because CI tags the FULL 40-char sha while this
# script is routinely handed a short one. web is per-environment (DEF-019), hence the suffix.
tag_of() { printf '%s\n' "$env_body" | tr -d '\r' | sed -n "s/^$1=//p" | head -n1; }
prefix_match() { case "$1" in "$2"*) return 0;; esac; case "$2" in "$1"*) return 0;; esac; return 1; }

img_tag="$(tag_of ACMP_IMAGE_TAG)"; web_tag="$(tag_of ACMP_WEB_TAG)"
if [ "${ACMP_ALLOW_TAG_DRIFT:-0}" = "1" ]; then
  log "WARNING: tag-drift guard DISABLED — deploying $sha with images $img_tag / $web_tag"
elif ! prefix_match "$img_tag" "$sha" || ! prefix_match "${web_tag%-$env_name}" "$sha"; then
  die "IMAGE-TAG DRIFT: $param pins ACMP_IMAGE_TAG=$img_tag and ACMP_WEB_TAG=$web_tag, but you asked to
     deploy commit $sha. The box would check out $sha and RUN a different build, silently.
     Re-publish the environment with the tags for this commit (the tags CI writes are the FULL sha,
     and web carries the -$env_name suffix):
       ACMP_IMAGE_TAG=<full-sha>  ACMP_WEB_TAG=<full-sha>-$env_name
       bash deploy/aws/09-put-env.sh $env_name <path-to-env-file>
     Deliberately deploying older images (a rollback) is ACMP_ALLOW_TAG_DRIFT=1."
else
  log "image tags match the commit ($img_tag / $web_tag)"
fi

# Heredoc is quoted so NOTHING here expands locally; the only interpolated values are the three
# injected explicitly below. An unquoted heredoc would expand this host's variables into the
# remote script, which is how a local shell's state silently leaks into a deployment.
remote="$(cat <<'REMOTE'
set -euo pipefail
exec 2>&1
command -v docker >/dev/null || { dnf install -y docker; systemctl enable --now docker; }
docker compose version >/dev/null 2>&1 || {
  mkdir -p /usr/local/lib/docker/cli-plugins
  # PINNED, not "latest". A bootstrap that silently picks up whatever compose released this morning
  # is not reproducible: two boxes built from the same commit could run different compose versions,
  # and "it worked last week" stops being evidence of anything. Bump this deliberately.
  curl -fsSL https://github.com/docker/compose/releases/download/v2.32.4/docker-compose-linux-x86_64 \
    -o /usr/local/lib/docker/cli-plugins/docker-compose
  chmod +x /usr/local/lib/docker/cli-plugins/docker-compose
}
command -v git >/dev/null || dnf install -y git

# CRON IS NOT INSTALLED ON AMAZON LINUX 2023. Nothing in the base AMI provides crontab, so runbook
# section 8 -- `crontab -e`, the ONLY thing that schedules deploy/scripts/backup.sh -- died with
# `crontab: command not found` and the backup schedule simply never existed. The nightly and
# every-4h backups NFR-056 (RPO <= 4h) and NFR-058 (off-instance copy) depend on are the entire
# content of that step, so a box provisioned from the runbook was running with NO scheduled backup
# at all while looking completely healthy.
#
# It hid the same way DEF-024 hid: the step was delivered over `aws ssm send-command`, whose command
# list has no set -e, so `crontab` failing left the following commands running and the invocation
# still reported Success. A manual backup.sh run in the same batch then succeeded and copied to S3,
# which made the whole thing look verified -- the manual run WAS real, but it is evidence for
# backup.sh, not for the schedule. Surfaced only once the invocation was run under `set -eu`.
#
# Installed here rather than documented in the runbook because everything else the deployment needs
# (docker, compose, git) is a bootstrap prerequisite, and a documented prerequisite is what gets
# skipped. enable --now so the daemon survives the AC-082 stop/start, like the swapfile's fstab entry.
command -v crontab >/dev/null || dnf install -y cronie
systemctl enable --now crond

# 4 GiB swapfile (RISK-013). docker-compose.cloud.yml ALREADY states "The P25 bootstrap adds a 4 GiB
# swapfile as the spike absorber" and budgets 3584 MiB of container limits against 4 GiB of RAM,
# leaving ~512 MiB for the host. It did not exist. Without it a memory spike is resolved by the OOM
# killer choosing a container rather than by swapping, and AC-084's headroom argument is fiction.
# The fstab line matters as much as the swapon: AC-082 stop/start must come back with swap present.
if ! swapon --show=NAME --noheadings 2>/dev/null | grep -q '^/swapfile$'; then
  fallocate -l 4G /swapfile 2>/dev/null || dd if=/dev/zero of=/swapfile bs=1M count=4096 status=none
  chmod 600 /swapfile
  mkswap /swapfile >/dev/null
  swapon /swapfile
  grep -q '^/swapfile ' /etc/fstab || echo '/swapfile none swap sw 0 0' >> /etc/fstab
fi

install -d -m 0755 /opt/acmp
if [ -d /opt/acmp/.git ]; then git -C /opt/acmp fetch --all --tags --quiet; else git clone --quiet "__REPO__" /opt/acmp; fi
git -C /opt/acmp checkout --quiet "__SHA__"
cd /opt/acmp

# The .bak path DEF-017 requires: SQL Server writes the backup itself as uid 10001, and Docker
# would otherwise create this mount root-owned 0755 and every BACKUP DATABASE would fail at 02:00
# in a cron log while the deployment looks backed up.
#
# THE PATH MATTERS. This used to create /opt/acmp-backups while the compose mount is
# ${ACMP_BACKUP_DIR:-/opt/acmp/backups} — so Docker would have auto-created the REAL path
# root-owned 0755 and reproduced DEF-017 exactly, in the very lines written to prevent it. The
# directory this creates must be the directory the compose file mounts.
install -d -o 10001 -g 10001 -m 0755 /opt/acmp/backups

umask 077
aws ssm get-parameter --name "__PARAM__" --with-decryption --query Parameter.Value --output text > deploy/.env.cloud
ACMP_ENV_FILE=/opt/acmp/deploy/.env.cloud sh deploy/scripts/gen-secrets.sh

# ECR auth that does not expire mid-life. `get-login-password | docker login` mints a 12-HOUR token;
# with restart: unless-stopped on every service, the first reboot or re-pull past that window fails
# to authenticate with nothing having changed. The credential helper re-signs from the instance
# profile on each pull instead. Scoped with credHelpers (not credsStore) so only this registry is
# affected. Falls back loudly rather than silently if the package is unavailable.
if command -v docker-credential-ecr-login >/dev/null 2>&1 || dnf install -y amazon-ecr-credential-helper; then
  install -d -m 0700 /root/.docker
  printf '{"credHelpers":{"__REGISTRY__":"ecr-login"}}\n' > /root/.docker/config.json
else
  echo "WARN: amazon-ecr-credential-helper unavailable — falling back to a 12h docker login token"
  aws ecr get-login-password --region "__REGION__" | docker login --username AWS --password-stdin "__REGISTRY__"
fi

# --- TLS placeholder so first boot cannot deadlock ----------------------------------------------
# The cloud stack mounts a 443 server block, and nginx REFUSES TO START if ssl_certificate points at
# a file that is not there. certbot cannot run before the box is up, and the box cannot come up
# without a certificate — so the first boot would deadlock on itself. A self-signed placeholder
# breaks the cycle; certbot-deploy-hook.sh replaces it with the real chain and reloads nginx.
#
# The CN is deliberately alarming. A placeholder that looked like a normal certificate could sit
# there for months: nginx serves it, the healthcheck passes, the stack reports healthy, and every
# browser refuses the connection. Anyone running `openssl s_client` sees what this is immediately.
install -d -m 0755 deploy/nginx/certs
if [ ! -s deploy/nginx/certs/fullchain.pem ]; then
  echo "no certificate yet — generating a self-signed PLACEHOLDER so nginx can start"
  openssl req -x509 -newkey rsa:2048 -nodes -days 3650 \
    -subj "/CN=ACMP-PLACEHOLDER-DO-NOT-TRUST" \
    -keyout deploy/nginx/certs/privkey.pem \
    -out    deploy/nginx/certs/fullchain.pem >/dev/null 2>&1
  # UID 101 is the nginx user inside the container; the bind mount preserves host ownership, so a
  # root-owned 0600 key would be unreadable to nginx exactly as a real Let's Encrypt key would be.
  chown 101:101 deploy/nginx/certs/privkey.pem deploy/nginx/certs/fullchain.pem
  chmod 0640 deploy/nginx/certs/privkey.pem
  chmod 0644 deploy/nginx/certs/fullchain.pem
fi

CF="-f deploy/docker-compose.cloud.yml --env-file deploy/.env.cloud"
# --quiet-pull is not cosmetic. SSM caps StandardOutputContent at 24,000 characters, and Docker's
# per-layer progress bars fill that entirely on a cold box — the first real run ended with
# "--output truncated--" and the actual error scrolled out of reach, so the failure could only be
# diagnosed by going back to the instance. Quiet pull keeps the tail of the log meaningful.
docker compose $CF pull --quiet
docker compose $CF up -d --quiet-pull

# --- ASSERT the stack is actually up ------------------------------------------------------------
# This used to run `docker compose ps` and let SSM's exit status decide. A stack where every
# container was unhealthy still reported "bootstrap Success": printing state is not checking it.
# Same silent-success family as DEF-017 (backups never written), DEF-018 (search returns nothing)
# and DEF-020 (Seq crash-looping behind condition: service_started).
# 15 minutes TOTAL across every service. This is a COLD box doing first-ever schema migration and
# realm import, so it is deliberately more generous than AC-082's 10-minute stop/start budget —
# that budget is a different measurement, taken later by an external probe against a warm box.
DEADLINE=$(( $(date +%s) + 900 ))
fail=0

wait_healthy() {  # a service with a healthcheck must reach `healthy`; one without must be `running`
  while [ "$(date +%s)" -lt "$DEADLINE" ]; do
    cid="$(docker compose $CF ps -q "$1" 2>/dev/null || true)"
    if [ -n "$cid" ]; then
      st="$(docker inspect -f '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' "$cid" 2>/dev/null || echo unknown)"
      case "$st" in healthy|running) echo "  OK    $1 ($st)"; return 0 ;; esac
    fi
    sleep 5
  done
  echo "  FAIL  $1 never became healthy (last: ${st:-absent})"; fail=1; return 1
}

wait_oneshot() {  # must have EXITED, and exited 0 — reading the code while it still runs is the
  while [ "$(date +%s)" -lt "$DEADLINE" ]; do   # bug PE-154 recorded in the local boot gate
    cid="$(docker compose $CF ps -aq "$1" 2>/dev/null || true)"
    if [ -n "$cid" ]; then
      st="$(docker inspect -f '{{.State.Status}}:{{.State.ExitCode}}' "$cid" 2>/dev/null || echo unknown:x)"
      case "$st" in
        exited:0)  echo "  OK    $1 (exited 0)"; return 0 ;;
        exited:*)  echo "  FAIL  $1 ($st)"; fail=1; return 1 ;;
      esac
    fi
    sleep 5
  done
  echo "  FAIL  $1 never finished (last: ${st:-absent})"; fail=1; return 1
}

# --- INSTALL THE SCHEDULE ITSELF, not just the daemon -------------------------------------------
# Lines 126-127 install cronie and enable crond, and then the ONLY thing that actually schedules a
# backup was left as a manual runbook step (`crontab deploy/scripts/crontab.example`, runbooks
# README.md step 6 / cloud-provisioning.md section 8). That is DEF-026 half-fixed: the comment above
# argues "a documented prerequisite is what gets skipped" and then documents this one. A box rebuilt
# from this script alone has a running cron daemon and an EMPTY crontab, which looks identical to a
# scheduled box from every angle except the one that matters.
#
# It also silently decided whether OQ-068's @reboot backup exists at all: editing crontab.example
# without this line ships a schedule that never reaches any box.
#
# Root's crontab, because every line writes to /var/log/acmp-backup.log and drives docker. Idempotent
# by construction — `crontab <file>` REPLACES the table rather than appending, so re-bootstrapping
# converges instead of accumulating duplicate entries. The flip side, stated because it will surprise
# somebody: any line hand-added on the box is DISCARDED on the next bootstrap. That is the intended
# direction (the file is the source of truth) — put lasting changes in crontab.example, not in
# `crontab -e`. Installed BEFORE the health assertions so a stack that comes up unhealthy still
# leaves a box that is scheduled.
crontab -u root deploy/scripts/crontab.example
echo "--- backup schedule installed (root crontab) ---"
crontab -u root -l | grep -E '^[0-9@]' || true

echo "--- asserting stack health (up to 15 min) ---"
for s in sqlserver-init db-migrate keycloak-config; do wait_oneshot "$s" || true; done
for s in sqlserver keycloak seq api worker web;     do wait_healthy "$s" || true; done

docker compose $CF ps
[ "$fail" -eq 0 ] || { echo "BOOTSTRAP FAILED — see the failures above"; exit 1; }
echo "--- all services healthy and all one-shots exited 0 ---"
REMOTE
)"
remote="${remote//__REPO__/$repo_url}"
remote="${remote//__SHA__/$sha}"
remote="${remote//__PARAM__/$param}"
remote="${remote//__REGION__/$REGION}"
remote="${remote//__REGISTRY__/${ACCOUNT_ID}.dkr.ecr.${REGION}.amazonaws.com}"

log "sending bootstrap via SSM (this takes several minutes on a cold box)"
# --parameters must be REAL JSON from a file, not the CLI's shorthand `commands=[...]` form. The
# shorthand parser splits on commas and newlines, so a multi-line script is torn apart before it
# ever reaches the instance — the box then fails with "line 2: syntax error: unexpected end of
# file", which reads like a bug in the script rather than in how it was transported.
params="$(mktemp)"
printf '%s' "$remote" | python3 -c 'import json,sys; print(json.dumps({"commands":[sys.stdin.read()]}))' > "$params"
# MSYS does not rewrite paths inside a file:// URL, so hand the CLI the native form explicitly.
params_uri="$params"
command -v cygpath >/dev/null 2>&1 && params_uri="$(cygpath -m "$params")"
cmd_id="$(aws ssm send-command --region "$REGION" --instance-ids "$instance_id" \
  --document-name AWS-RunShellScript --comment "acmp bootstrap $env_name $sha" \
  --parameters "file://$params_uri" \
  --query 'Command.CommandId' --output text)"
rm -f "$params"
log "command $cmd_id — waiting"
# `aws ssm wait command-executed` gives up after ~100 seconds (20 polls x 5s), and the `|| true`
# that swallowed its timeout meant the NEXT line read a status that was not final yet. A cold-box
# bootstrap installs Docker, clones, pulls four images, migrates a database and imports a realm —
# it takes many minutes, so the wait ALWAYS expired and the script always reported "finished with
# status InProgress" for a run that was proceeding perfectly well. Reporting a non-final state as a
# result is the same defect PE-154 recorded in the local boot gate, one layer up.
#
# Poll to a genuinely terminal state instead, with a deadline generous enough for a cold box.
status=""
deadline=$(( $(date +%s) + 1500 ))
while [ "$(date +%s)" -lt "$deadline" ]; do
  status="$(aws ssm get-command-invocation --region "$REGION" --command-id "$cmd_id" \
    --instance-id "$instance_id" --query Status --output text 2>/dev/null || echo Pending)"
  case "$status" in Success|Failed|Cancelled|TimedOut) break ;; esac
  sleep 15
done
[ -n "$status" ] || status=Unknown
aws ssm get-command-invocation --region "$REGION" --command-id "$cmd_id" --instance-id "$instance_id" \
  --query StandardOutputContent --output text | tail -40
[ "$status" = "Success" ] || die "bootstrap finished with status $status — see the output above"
log "bootstrap Success. Next: 05-route53.sh, then certbot for TLS (AC-081)."
