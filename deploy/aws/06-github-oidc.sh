#!/usr/bin/env bash
# 06 — GitHub Actions OIDC trust + a push-only ECR role (SL-024 / ADR-0037).
#
# CI needs to push images to ECR. The obvious way is an IAM user with an access key in GitHub
# secrets; this deliberately does NOT do that. A long-lived key in CI is a credential that never
# expires, is readable by every workflow in the repo, and survives the laptop it was pasted from —
# and this project has already lost one set of keys to a temp file today. GitHub's OIDC provider
# lets Actions exchange a short-lived, workflow-scoped token for a role instead. Nothing to rotate,
# nothing to leak.
#
#   bash deploy/aws/06-github-oidc.sh
#
# Idempotent. Safe to re-run after changing the branch/repo scope below.
set -euo pipefail
. "$(dirname "$0")/_common.sh"
require_aws

GH_ORG="${ACMP_GH_ORG:-A-H-911}"
GH_REPO="${ACMP_GH_REPO:-acmp}"
OIDC_HOST="token.actions.githubusercontent.com"
OIDC_ARN="arn:aws:iam::${ACCOUNT_ID}:oidc-provider/${OIDC_HOST}"
ROLE="${PROJECT}-github-ecr-push"

# 1) The OIDC identity provider ------------------------------------------------------------
# The thumbprint is a historical artefact: since 2023 AWS validates GitHub's token endpoint against
# its own trusted root CAs and ignores this value, but the API still demands the parameter. Passing
# the long-published GitHub thumbprint keeps it valid for older behaviour without pretending it is
# a security control we maintain.
if aws iam get-open-id-connect-provider --open-id-connect-provider-arn "$OIDC_ARN" >/dev/null 2>&1; then
  log "OIDC provider $OIDC_HOST already registered"
else
  log "registering OIDC provider $OIDC_HOST"
  aws iam create-open-id-connect-provider \
    --url "https://${OIDC_HOST}" \
    --client-id-list "sts.amazonaws.com" \
    --thumbprint-list "6938fd4d98bab03faadb97b34396831e3780aea1" \
    --tags $TAGS >/dev/null
fi

# 2) The role CI assumes -------------------------------------------------------------------
# Scoped to ONE repo and to EXACTLY ONE subject: a push to the default branch. `aud` is pinned too,
# so a token minted for another audience is refused.
#
# StringEquals, not StringLike, and no wildcards — that is deliberate. An earlier revision also
# allowed `:ref:refs/tags/*` and `:environment:*`, neither of which the publish job uses. The
# environment entry was the dangerous one: a job that names ANY GitHub Environment gets
# sub=repo:...:environment:NAME, so `environment:*` would let a subject other than a main-branch
# push assume a role that can write to the registry the servers pull from — including environments
# created later by anyone with write access, silently widening this grant with no change here.
# Unused permissions are not free; they are the ones nobody re-reads. Add a subject when a workflow
# actually needs one.
TRUST="$(cat <<JSON
{ "Version": "2012-10-17", "Statement": [ {
  "Effect": "Allow",
  "Principal": { "Federated": "$OIDC_ARN" },
  "Action": "sts:AssumeRoleWithWebIdentity",
  "Condition": {
    "StringEquals": {
      "${OIDC_HOST}:aud": "sts.amazonaws.com",
      "${OIDC_HOST}:sub": "repo:${GH_ORG}/${GH_REPO}:ref:refs/heads/main"
    }
  } } ] }
JSON
)"
if aws iam get-role --role-name "$ROLE" >/dev/null 2>&1; then
  log "role $ROLE exists — refreshing its trust policy"
  aws iam update-assume-role-policy --role-name "$ROLE" --policy-document "$TRUST" >/dev/null
else
  log "creating role $ROLE"
  aws iam create-role --role-name "$ROLE" --assume-role-policy-document "$TRUST" --tags $TAGS >/dev/null
fi

# 3) Push-only permissions ------------------------------------------------------------------
# Push and read, on THIS project's repositories only. Deliberately absent: ecr:DeleteRepository,
# ecr:BatchDeleteImage and any lifecycle-policy write — CI publishes images, it does not get to
# remove the one production is currently running. GetAuthorizationToken cannot be resource-scoped
# (it is an account-level token mint), which is why it is a separate statement.
aws iam put-role-policy --role-name "$ROLE" --policy-name ecr-push --policy-document "$(cat <<JSON
{ "Version": "2012-10-17", "Statement": [
  { "Sid": "EcrAuth", "Effect": "Allow", "Action": "ecr:GetAuthorizationToken", "Resource": "*" },
  { "Sid": "EcrPush", "Effect": "Allow",
    "Action": [ "ecr:BatchCheckLayerAvailability", "ecr:InitiateLayerUpload", "ecr:UploadLayerPart",
                "ecr:CompleteLayerUpload", "ecr:PutImage", "ecr:BatchGetImage",
                "ecr:GetDownloadUrlForLayer", "ecr:DescribeImages" ],
    "Resource": "arn:aws:ecr:${REGION}:${ACCOUNT_ID}:repository/${PROJECT}/*" } ] }
JSON
)"

ROLE_ARN="$(aws iam get-role --role-name "$ROLE" --query Role.Arn --output text)"
log "done. Add this as the repository variable AWS_ROLE_ARN (a variable, NOT a secret — it is not one):"
log "  $ROLE_ARN"
log "  gh variable set AWS_ROLE_ARN --body '$ROLE_ARN'"
