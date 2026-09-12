#!/usr/bin/env bash
# 04 — ECR repositories (SL-020 / ADR-0037). Images are built in CI (P24) and pulled on the
# box; building on a 4 GiB instance that also runs SQL Server would OOM. One repo per image,
# scan-on-push on, immutable-by-digest promotion, and a lifecycle policy keeping the last 10 commits
# BEHIND a rule that protects whatever an environment is pinned to (DEF-143).
set -euo pipefail
. "$(dirname "$0")/_common.sh"

REPOS=(api web worker sqlserver-fts)

# Retention is expressed in COMMITS, not images, and the two are not the same number. The
# lifecycle rule counts images (`imageCountMoreThan`), and `web` is the one repo that receives
# TWO images per merge — <sha>-uat and <sha>-prod — because ADR-0037 bakes the OIDC authority
# into the bundle. A flat "keep 10 images" therefore gives web only FIVE commits of rollback
# depth while api/worker/sqlserver-fts get ten. That asymmetry fails exactly where it hurts:
# promotion deploys BY DIGEST, so rolling back ~6 merges would find api and worker present and
# the matching web images already expired — a partial rollback missing the one image that
# carries the environment identity. Keep the depth uniform by scaling the count instead.
KEEP_COMMITS=10
images_per_commit() { [ "$1" = "web" ] && echo 2 || echo 1; }

# DEF-143 / LL-069: the flat count rule EXPIRED the very images prod and uat were PINNED to (every
# repository, both environments, found 2026-09-07), because the count never sees the pin. Rule 1
# therefore selects images carrying an environment ALIAS tag -- `prod` / `uat`, set by
# deploy/scripts/promote-image.sh on the images it promotes -- with a count no promotion history
# reaches. ECR evaluates rules in priority order and an image is subject only to the FIRST rule that
# selects it, so the count rule at priority 2 never sees an aliased image. The alias moves with each
# promotion; the image it leaves falls back under the count rule, which is the rollback depth working
# as designed. `<sha>-prod` web tags do not start with `prod`, so the prefix selects aliases only.
PROTECTED_TAG_PREFIXES='"prod","uat"'
policy_for() { # <images-to-keep>
  printf '{"rules":[{"rulePriority":1,"description":"never expire an image an environment is pinned to (DEF-143)","selection":{"tagStatus":"tagged","tagPrefixList":[%s],"countType":"imageCountMoreThan","countNumber":9999},"action":{"type":"expire"}},{"rulePriority":2,"description":"keep last %s commits (%s images)","selection":{"tagStatus":"any","countType":"imageCountMoreThan","countNumber":%s},"action":{"type":"expire"}}]}' "$PROTECTED_TAG_PREFIXES" "$KEEP_COMMITS" "$1" "$1"
}
# ACMP_ECR_PRINT_POLICY=1 prints each repo's policy text and exits WITHOUT touching AWS -- feed it to
# `aws ecr start-lifecycle-policy-preview --lifecycle-policy-text` to see what it would expire first.
if [ "${ACMP_ECR_PRINT_POLICY:-0}" = "1" ]; then
  for r in "${REPOS[@]}"; do printf '%s ' "$r"; policy_for "$(( KEEP_COMMITS * $(images_per_commit "$r") ))"; echo; done
  exit 0
fi
require_aws

for r in "${REPOS[@]}"; do
  name="${PROJECT}/${r}"
  if aws ecr describe-repositories --repository-names "$name" >/dev/null 2>&1; then
    log "ECR repo $name exists"
  else
    log "creating ECR repo $name"
    aws ecr create-repository --repository-name "$name" \
      --image-scanning-configuration scanOnPush=true \
      --tags Key=Project,Value=ACMP >/dev/null
  fi
  keep=$(( KEEP_COMMITS * $(images_per_commit "$r") ))
  log "  lifecycle: keep last $keep images (= $KEEP_COMMITS commits)"
  aws ecr put-lifecycle-policy --repository-name "$name" --lifecycle-policy-text "$(policy_for "$keep")" >/dev/null
done
log "done. Registry: ${ACCOUNT_ID}.dkr.ecr.${REGION}.amazonaws.com/${PROJECT}/<image>"
