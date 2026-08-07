#!/usr/bin/env bash
# certbot --deploy-hook: publish a freshly issued/renewed certificate to the nginx container and
# reload it (SL-025 / AC-081). certbot runs this ONLY when a certificate actually changed.
#
#   certbot certonly --dns-route53 -d uat.acmp.anas7ammo.dev \
#     --deploy-hook /opt/acmp/deploy/scripts/certbot-deploy-hook.sh
#
# WHY A HOOK IS REQUIRED AND NOT A CONVENIENCE
# Pointing nginx straight at /etc/letsencrypt/live/<host>/ does not work here, for three reasons
# that each fail on their own:
#
#   1. OWNERSHIP. privkey.pem is 0600 root:root. The web container runs as UID 101 with a read-only
#      root filesystem, and a bind mount preserves the host's mode and ownership — so nginx would
#      be unable to read the key and would refuse to start. Not a permissions warning: a dead site.
#   2. SYMLINKS. /etc/letsencrypt/live/<host>/*.pem are symlinks into ../../archive/<host>/. A bind
#      mount of `live/` alone exposes dangling links inside the container, because the archive
#      directory it points at is not mounted. The files must be DEREFERENCED, not linked.
#   3. RELOAD. nginx reads certificates once, at startup. A renewed certificate that is never
#      reloaded changes nothing until something restarts the container — which, 60 days later, is
#      nobody. The site then serves an expired certificate having "renewed" successfully.
#
# The failure mode of getting this wrong is the one this project keeps meeting: everything reports
# success and the thing itself is broken. certbot says renewed, the container says healthy, and the
# browser says the certificate expired three weeks ago.
set -euo pipefail

CERTS_DIR="${ACMP_CERTS_DIR:-/opt/acmp/deploy/nginx/certs}"
COMPOSE_DIR="${ACMP_COMPOSE_DIR:-/opt/acmp}"
# UID/GID 101 is the nginx user in nginxinc/nginx-unprivileged (see deploy/Dockerfile.web).
NGINX_UID="${ACMP_NGINX_UID:-101}"

# certbot exports RENEWED_LINEAGE for deploy hooks; fall back to an explicit argument so the hook
# can be run by hand during a rehearsal or a first issuance check.
lineage="${RENEWED_LINEAGE:-${1:-}}"
[ -n "$lineage" ] || { echo "certbot-deploy-hook: no RENEWED_LINEAGE and no lineage argument given" >&2; exit 1; }
[ -d "$lineage" ] || { echo "certbot-deploy-hook: lineage directory not found: $lineage" >&2; exit 1; }

for f in fullchain.pem privkey.pem; do
  [ -e "$lineage/$f" ] || { echo "certbot-deploy-hook: $lineage/$f is missing" >&2; exit 1; }
done

install -d -m 0755 "$CERTS_DIR"

# Clean up the staging files on ANY exit. Found by testing: if chown fails part-way (a bad UID, or
# simply a platform without a root user), `set -e` aborts between the copy and the move and leaves
# .privkey.new — a fragment of a PRIVATE KEY — lying in the certs directory with whatever mode it
# had at that moment. The failure is loud, but the residue is silent, so it needs its own cleanup.
trap 'rm -f "$CERTS_DIR/.fullchain.new" "$CERTS_DIR/.privkey.new"' EXIT

# --dereference is the whole point: copy the FILE the symlink points at, not the link.
# Written to a temp name and moved into place so nginx can never observe a half-written key.
umask 077
cp --dereference "$lineage/fullchain.pem" "$CERTS_DIR/.fullchain.new"
cp --dereference "$lineage/privkey.pem"   "$CERTS_DIR/.privkey.new"

# 0640 root:101 — readable by the container's nginx user and by nobody else. Not 0644: this is a
# private key, and the directory is inside a git checkout that other processes can read.
chown "root:${NGINX_UID}" "$CERTS_DIR/.privkey.new"
chmod 0640 "$CERTS_DIR/.privkey.new"
chown "root:${NGINX_UID}" "$CERTS_DIR/.fullchain.new"
chmod 0644 "$CERTS_DIR/.fullchain.new"

mv -f "$CERTS_DIR/.fullchain.new" "$CERTS_DIR/fullchain.pem"
mv -f "$CERTS_DIR/.privkey.new"   "$CERTS_DIR/privkey.pem"

subject="$(openssl x509 -in "$CERTS_DIR/fullchain.pem" -noout -subject 2>/dev/null || echo '?')"
expiry="$(openssl x509 -in "$CERTS_DIR/fullchain.pem" -noout -enddate 2>/dev/null || echo '?')"
echo "certbot-deploy-hook: installed $subject ($expiry) into $CERTS_DIR"

# Reload rather than restart: nginx re-reads the certificate without dropping connections, and a
# restart would take the site down for the duration of a container start.
if command -v docker >/dev/null 2>&1 && [ -f "$COMPOSE_DIR/deploy/docker-compose.cloud.yml" ]; then
  cd "$COMPOSE_DIR"
  CF="-f deploy/docker-compose.cloud.yml --env-file deploy/.env.cloud"
  # `nginx -t` first: reloading a bad config leaves the OLD one running and returns success, so a
  # broken certificate would be reported as a successful reload.
  if docker compose $CF exec -T web nginx -t 2>/dev/null; then
    docker compose $CF exec -T web nginx -s reload
    echo "certbot-deploy-hook: nginx reloaded"
  else
    echo "certbot-deploy-hook: nginx -t FAILED with the new certificate — NOT reloading" >&2
    exit 1
  fi
else
  echo "certbot-deploy-hook: docker/compose not found — certificate is in place but nginx was NOT reloaded" >&2
  exit 1
fi
