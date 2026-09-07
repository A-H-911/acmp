#!/usr/bin/env bash
# ACMP image promotion (P24 part 2 / SL-024). Promotes a commit's published images into an
# environment by pinning that environment's .env to them, after PROVING every image exists.
#
#   deploy/scripts/promote-image.sh <uat|prod> <commit-sha> [path-to-env-file]
#
# This script exists because SL-024 originally said the CI digest artefact was "consumed by
# promote.sh". It cannot be: deploy/scripts/promote.sh is the P18b ON-PREM warm-standby VM
# failover script -- it restores a backup, runs up.sh --prod and stops before DNS cutover -- and
# its premise, a standby VM, does not exist in the cloud topology. Image promotion needed its own
# script; this is it.
#
# WHAT "PROMOTE BY DIGEST" DOES AND DOES NOT MEAN HERE (the subtlety that matters):
#   api / worker / sqlserver-fts are environment-AGNOSTIC. The SAME BYTES that UAT tested are what
#   production runs, and this script prints their digests so that identity is auditable.
#   `web` CANNOT be promoted that way and it is not a defect that it cannot: ADR-0037 bakes
#   VITE_OIDC_AUTHORITY into the bundle at build time, so the UAT and prod web images are
#   DIFFERENT ARTEFACTS BY DESIGN, built from the same commit. Promoting web therefore means
#   selecting the <sha>-prod build, never re-pointing the <sha>-uat one. Getting this wrong ships
#   a production bundle that authenticates against UAT -- which fails in the browser with nothing
#   in the API logs, the same silent shape as the P22 CSP bug.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
REGION="${AWS_REGION:-us-east-1}"
ACCOUNT_ID="${ACMP_ACCOUNT_ID:-565393059398}"
REGISTRY="${ACMP_REGISTRY:-${ACCOUNT_ID}.dkr.ecr.${REGION}.amazonaws.com/acmp}"
export AWS_PAGER=""

log() { printf '[promote-image %s] %s\n' "$(date +%H:%M:%S)" "$*"; }
die() { printf '[promote-image ERROR] %s\n' "$*" >&2; exit 1; }

env_name="${1:-}"; sha="${2:-}"; env_file="${3:-$ROOT/deploy/.env}"
case "$env_name" in uat|prod) ;; *) die "usage: promote-image.sh <uat|prod> <commit-sha> [env-file]";; esac
[ -n "$sha" ] || die "usage: promote-image.sh <uat|prod> <commit-sha> [env-file]"
command -v aws >/dev/null 2>&1 || die "aws CLI not found on PATH"

# Resolve a tag to its immutable digest, or fail loudly. A missing tag is EXACTLY the DEF-019
# failure -- the deploy would otherwise discover it mid-pull, on the box.
digest_of() { # repo tag
  aws ecr describe-images --region "$REGION" --repository-name "acmp/$1" \
      --image-ids imageTag="$2" --query 'imageDetails[0].imageDigest' --output text 2>/dev/null \
    || die "acmp/$1:$2 does not exist in ECR -- CI never published it. Refusing to promote a tag that cannot be pulled."
}

log "verifying every image for $sha exists before touching $env_file"
d_api=$(digest_of api "$sha")
d_worker=$(digest_of worker "$sha")
d_sql=$(digest_of sqlserver-fts "$sha")
d_web=$(digest_of web "$sha-$env_name")

# Guard the mistake this script is most likely to be used to make. Promoting to prod while the
# prod web image is missing must fail here, not silently fall back to the uat one.
[ -n "$d_web" ] && [ "$d_web" != "None" ] || die "acmp/web:$sha-$env_name missing -- never point $env_name at another environment's bundle"

cat <<SUMMARY

  promoting commit $sha -> $env_name
  ------------------------------------------------------------------
  environment-AGNOSTIC (identical bytes across environments):
    api             $d_api
    worker          $d_worker
    sqlserver-fts   $d_sql
  environment-SPECIFIC (a different artefact per environment, ADR-0037):
    web:$sha-$env_name
                    $d_web
  ------------------------------------------------------------------
SUMMARY

[ -f "$env_file" ] || die "env file not found: $env_file"
cp "$env_file" "$env_file.bak"

# Rewrite in place when present, append when absent, so this works on a fresh .env copied from the
# example and on one already pinned to an older commit.
set_var() { # name value
  if grep -Eq "^${1}=" "$env_file"; then
    sed -i.tmp -E "s|^${1}=.*|${1}=${2}|" "$env_file" && rm -f "$env_file.tmp"
  else
    printf '%s=%s\n' "$1" "$2" >> "$env_file"
  fi
}
set_var ACMP_IMAGE_TAG "$sha"
set_var ACMP_WEB_TAG   "$sha-$env_name"

log "pinned $env_file (previous kept as $(basename "$env_file").bak)"

# DEF-143 / LL-069: the alias tag `<env>` is what deploy/aws/04-ecr.sh's protective rule keys on, so an
# image an environment is pinned to can no longer be expired by the count rule. Set AFTER the env file
# is pinned and the digests printed; idempotent (an alias already on this digest is left alone). The
# alias MOVES: the image it leaves falls back under the count rule -- rollback depth as designed.
# ⛔⛔ DEF-148: THE MANIFEST GOES THROUGH A FILE, NEVER THROUGH A SHELL $( ). ECR keys an image on the
# DIGEST OF THE MANIFEST BYTES, so a command substitution -- which strips trailing newlines, and whose
# text rendering need not reproduce what was stored -- makes put-image create a SECOND image object
# carrying the alias while the original keeps the <sha> tag. Measured 2026-09-08 on all four
# repositories: acmp/api:prod resolved to ca1fb2b8 while acmp/api:<sha> resolved to 4771b141, so the
# protective rule guarded an image nothing is pinned to and the PINNED tag stayed on the count rule --
# the exact condition DEF-143 exists to remove, restored silently while every log line below said
# success. With file:// the bytes survive, the digest is preserved, and put-image ADDS the tag to the
# existing image. VERIFY IN THE REGISTRY, NOT ON THE EXIT CODE (DEF-147): one image must carry BOTH
# tags, and `aws ecr start-lifecycle-policy-preview` must list no prod/uat-tagged image for expiry.
alias_tag() { # repo tag
  local want have manifest mtype mfile
  want=$(digest_of "$1" "$2")
  have=$(aws ecr describe-images --region "$REGION" --repository-name "acmp/$1" --image-ids imageTag="$env_name" --query 'imageDetails[0].imageDigest' --output text 2>/dev/null || true)
  if [ "$have" = "$want" ]; then log "acmp/$1:$env_name already -> $want"; return; fi
  # ⚠ CWD-RELATIVE, NOT mktemp: on Git Bash for Windows mktemp returns /tmp/... , which the Windows
  # aws CLI cannot open (ParamValidation: Unable to load paramfile). A relative path resolves for both.
  mfile="./.promote-manifest.$$.$1"
  trap 'rm -f "$mfile"' RETURN
  aws ecr batch-get-image --region "$REGION" --repository-name "acmp/$1" --image-ids imageTag="$2" --query 'images[0].imageManifest' --output text > "$mfile"
  # --output text appends a newline that the stored manifest does not have; strip it or the digest moves.
  printf '%s' "$(cat "$mfile")" > "$mfile.exact" && mv "$mfile.exact" "$mfile"
  mtype=$(aws ecr batch-get-image --region "$REGION" --repository-name "acmp/$1" --image-ids imageTag="$2" --query 'images[0].imageManifestMediaType' --output text)
  aws ecr put-image --region "$REGION" --repository-name "acmp/$1" --image-tag "$env_name" --image-manifest "file://$mfile" --image-manifest-media-type "$mtype" >/dev/null
  rm -f "$mfile"
  got=$(aws ecr describe-images --region "$REGION" --repository-name "acmp/$1" --image-ids imageTag="$env_name" --query 'imageDetails[0].imageDigest' --output text)
  [ "$got" = "$want" ] || die "acmp/$1:$env_name landed on $got, not the pinned $want -- the alias split (DEF-148); the pinned tag is NOT protected"
  log "acmp/$1:$env_name -> $want (same image object as the pinned tag; protected by the priority-1 rule, DEF-143)"
}
alias_tag api "$sha"; alias_tag worker "$sha"; alias_tag sqlserver-fts "$sha"; alias_tag web "$sha-$env_name"
log "next: on the box -> docker compose -f deploy/docker-compose.cloud.yml --env-file $env_file pull && ... up -d"
log "the three agnostic digests above are the bytes UAT tested; record them with the deployment."
