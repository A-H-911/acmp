#!/usr/bin/env bash
# 04 — ECR repositories (SL-020 / ADR-0037). Images are built in CI (P24) and pulled on the
# box; building on a 4 GiB instance that also runs SQL Server would OOM. One repo per image,
# scan-on-push on, immutable-by-digest promotion, and a lifecycle policy keeping the last 10.
set -euo pipefail
. "$(dirname "$0")/_common.sh"
require_aws

REPOS=(api web worker sqlserver-fts)
LIFECYCLE='{"rules":[{"rulePriority":1,"description":"keep last 10","selection":{"tagStatus":"any","countType":"imageCountMoreThan","countNumber":10},"action":{"type":"expire"}}]}'

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
  aws ecr put-lifecycle-policy --repository-name "$name" --lifecycle-policy-text "$LIFECYCLE" >/dev/null
done
log "done. Registry: ${ACCOUNT_ID}.dkr.ecr.${REGION}.amazonaws.com/${PROJECT}/<image>"
