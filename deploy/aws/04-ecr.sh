#!/usr/bin/env bash
# 04 — ECR repositories (SL-020 / ADR-0037). Images are built in CI (P24) and pulled on the
# box; building on a 4 GiB instance that also runs SQL Server would OOM. One repo per image,
# scan-on-push on, immutable-by-digest promotion, and a lifecycle policy keeping the last 10.
set -euo pipefail
. "$(dirname "$0")/_common.sh"
require_aws

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
  aws ecr put-lifecycle-policy --repository-name "$name" --lifecycle-policy-text \
    "{\"rules\":[{\"rulePriority\":1,\"description\":\"keep last $KEEP_COMMITS commits ($keep images)\",\"selection\":{\"tagStatus\":\"any\",\"countType\":\"imageCountMoreThan\",\"countNumber\":$keep},\"action\":{\"type\":\"expire\"}}]}" >/dev/null
done
log "done. Registry: ${ACCOUNT_ID}.dkr.ecr.${REGION}.amazonaws.com/${PROJECT}/<image>"
