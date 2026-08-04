#!/usr/bin/env bash
# 02 — S3 buckets (SL-020 / ADR-0035; AC-078, AC-080, AC-083). Per environment:
#   <project>-<env>-recordings  — meeting recordings + topic attachments (replaces MinIO)
#   <project>-<env>-backups     — off-instance SQL .bak copies (NFR-058, RISK-015)
# Every bucket: Block Public Access ON, SSE-S3 default encryption, versioning ON
# (versioning replaces MinIO's mc-mirror backup leg), and an AbortIncompleteMultipartUpload
# lifecycle rule (RISK/M-6: the Minio SDK multiparts a 2 GiB upload; a failed part bills forever).
set -euo pipefail
. "$(dirname "$0")/_common.sh"
require_aws

make_bucket() { # bucket-name [expire-current-after-days]
  local b="$1" expire="${2:-}"
  if aws s3api head-bucket --bucket "$b" >/dev/null 2>&1; then
    log "bucket $b exists"
  else
    log "creating bucket $b"
    # us-east-1 must NOT pass a LocationConstraint (it is the API default); other regions must.
    if [ "$REGION" = "us-east-1" ]; then
      aws s3api create-bucket --bucket "$b" >/dev/null
    else
      aws s3api create-bucket --bucket "$b" \
        --create-bucket-configuration LocationConstraint="$REGION" >/dev/null
    fi
    aws s3api put-bucket-tagging --bucket "$b" \
      --tagging 'TagSet=[{Key=Project,Value=ACMP}]' >/dev/null
  fi
  aws s3api put-public-access-block --bucket "$b" --public-access-block-configuration \
    BlockPublicAcls=true,IgnorePublicAcls=true,BlockPublicPolicy=true,RestrictPublicBuckets=true >/dev/null
  aws s3api put-bucket-encryption --bucket "$b" --server-side-encryption-configuration \
    '{"Rules":[{"ApplyServerSideEncryptionByDefault":{"SSEAlgorithm":"AES256"}}]}' >/dev/null
  aws s3api put-bucket-versioning --bucket "$b" --versioning-configuration Status=Enabled >/dev/null
  # Recordings are the system of record for a meeting — nothing expires them. BACKUPS are different: a
  # nightly .bak per database, uncompressed (Express rejects WITH COMPRESSION), uploaded forever would grow
  # without bound on an account whose whole budget is ~$45/mo and whose 100%/120% budget actions cut access
  # and stop instances. So the backup bucket alone expires CURRENT versions, on the same clock as the local
  # BACKUP_RETENTION_DAYS prune in deploy/scripts/backup.sh.
  local expire_rule=""
  [ -n "$expire" ] && expire_rule=",
       {\"ID\":\"expire-current\",\"Status\":\"Enabled\",\"Filter\":{},
        \"Expiration\":{\"Days\":${expire}}}"
  aws s3api put-bucket-lifecycle-configuration --bucket "$b" --lifecycle-configuration \
    "{\"Rules\":[
       {\"ID\":\"abort-incomplete-mpu\",\"Status\":\"Enabled\",\"Filter\":{},
        \"AbortIncompleteMultipartUpload\":{\"DaysAfterInitiation\":7}},
       {\"ID\":\"expire-noncurrent\",\"Status\":\"Enabled\",\"Filter\":{},
        \"NoncurrentVersionExpiration\":{\"NoncurrentDays\":90}}${expire_rule}]}" >/dev/null
  log "  $b: public-access blocked, SSE-S3, versioning on, mpu-abort 7d, noncurrent-expire 90d${expire:+, current-expire ${expire}d}"
}

for env in "${ENVS[@]}"; do
  make_bucket "$(bucket_for "$env")"
  make_bucket "$(backup_bucket_for "$env")" "${BACKUP_RETENTION_DAYS:-30}"
done
log "done. Buckets are private; access is via the per-env IAM users created in 03-iam.sh."
