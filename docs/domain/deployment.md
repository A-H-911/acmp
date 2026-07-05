# 33 — Containerization and Deployment Plan (Deliverable 41)

**Purpose:** Define Docker image design, Compose topology, config externalisation, health/readiness, EF migration strategy, backup/restore, warm-standby availability, and the numbered deployment runbook for ACMP on-prem.

> Constraints: on-prem VM + Docker Compose (no K8s, no service mesh — C-DEPLOY); self-contained (CON-001); ≤20 users; 99.9% availability via redundancy + nightly backup, not clustering (C-SCALE); SQL Server + MinIO + Seq + Hangfire + self-hosted Keycloak (ACMP-owned realm, bundled — ADR-0015); Tarseem sidecar Phase 2.

---

## 1. Docker Image Design

### 1.1 ACMP API image (multi-stage)

```dockerfile
# Stage 1: build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY *.sln .
COPY src/ ./src/
RUN dotnet restore
RUN dotnet publish src/Acmp.Api/Acmp.Api.csproj -c Release -o /app/publish

# Stage 2: runtime (non-root, minimal)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
RUN addgroup --system acmp && adduser --system --ingroup acmp acmpuser
WORKDIR /app
COPY --from=build /app/publish .
USER acmpuser
EXPOSE 8080
HEALTHCHECK --interval=15s --timeout=5s --start-period=30s --retries=3 \
  CMD wget -qO- http://localhost:8080/healthz || exit 1
ENTRYPOINT ["dotnet", "Acmp.Api.dll"]
```

**Image hardening:**
- Non-root user `acmpuser`.
- No shell (`/bin/sh`) in final layer [unverified: ASP.NET runtime image includes wget; use `curl` or `wget` whichever is present].
- `ReadOnlyRootFilesystem: true` in Compose where possible (override `/tmp` and `/app/logs` as tmpfs volumes).
- Labels: `org.opencontainers.image.version`, `org.opencontainers.image.revision`, `org.opencontainers.image.created`.

### 1.2 ACMP Web / nginx image (multi-stage)

```dockerfile
# Stage 1: build React SPA
FROM node:20-alpine AS web-build
WORKDIR /app
COPY src/Acmp.Web/package*.json ./
RUN npm ci --prefer-offline
COPY src/Acmp.Web/ .
RUN npm run build

# Stage 2: nginx serve
FROM nginx:1.27-alpine AS web-runtime
COPY deploy/nginx/nginx.conf /etc/nginx/nginx.conf
COPY --from=web-build /app/dist /usr/share/nginx/html
EXPOSE 80 443
HEALTHCHECK --interval=15s --timeout=5s --start-period=10s --retries=3 \
  CMD wget -qO- http://localhost/healthz || exit 1
```

**nginx responsibilities:** TLS termination (cert mounted as secret); serve SPA static assets with long-lived cache headers; reverse-proxy `/api/` to `acmp-api:8080`; security headers (`X-Frame-Options`, `Content-Security-Policy`, `Referrer-Policy`, `Strict-Transport-Security`).

**Alternative:** If a single-container deployment is preferred, the API can serve the SPA static files via `app.UseStaticFiles()` — keep this a deploy-time toggle via `ACMP_SERVE_SPA=true` env var. Recommended default: separate nginx container (cleaner caching, security headers, TLS handling).

---

## 2. Docker Compose Topology

### 2.1 Service inventory

| Service name | Image | Port(s) exposed (external) | Notes |
|---|---|---|---|
| `acmp-web` | `registry.internal/acmp-web:<tag>` | 80, 443 (only public ports) | nginx; TLS term; serves SPA; proxies /api |
| `acmp-api` | `registry.internal/acmp-api:<tag>` | internal 8080 only | ASP.NET Core 8; Hangfire in-process |
| `sqlserver` | `mcr.microsoft.com/mssql/server:2022-latest` | internal 1433 only | App-owned SQL Server; data volume |
| `seq` | `datalust/seq:latest` [unverified: exact tag] | internal 5341 (ingest) + 80 (UI; restrict to admin) | Self-hosted log/trace backend |
| `minio` | `minio/minio:latest` | internal 9000 (API) + 9001 (console; restrict to admin) | S3-compatible object store |
| `tarseem` | `registry.internal/tarseem:<tag>` (Phase 2) | internal 8001 only | FastAPI wrapper over tarseem.generate() |

### 2.2 Illustrative `docker-compose.yml` skeleton

```yaml
version: "3.9"

networks:
  acmp-net:
    driver: bridge
    internal: false   # only nginx has external access; rest are effectively internal

volumes:
  sqldata:
  seqdata:
  miniodata:
  minio-config:
  kcdata:          # self-hosted Keycloak datastore (ADR-0015; final store per OQ-038)

services:

  acmp-web:
    image: registry.internal/acmp-web:${IMAGE_TAG:-latest}
    restart: unless-stopped
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./deploy/nginx/certs:/etc/nginx/certs:ro
      - ./deploy/nginx/nginx.conf:/etc/nginx/nginx.conf:ro
    networks:
      - acmp-net
    depends_on:
      acmp-api:
        condition: service_healthy
    healthcheck:
      test: ["CMD", "wget", "-qO-", "http://localhost/healthz"]
      interval: 15s
      timeout: 5s
      retries: 3

  acmp-api:
    image: registry.internal/acmp-api:${IMAGE_TAG:-latest}
    restart: unless-stopped
    env_file:
      - ./deploy/env/acmp-api.env          # non-secret config
    secrets:
      - db_password
      - minio_secret_key
      - keycloak_client_secret
    environment:
      ACMP_ConnectionStrings__Default: "Server=sqlserver,1433;Database=acmp;User Id=sa;Password=${DB_PASSWORD};TrustServerCertificate=True"
      ACMP_Seq__ServerUrl: "http://seq:5341"
      ACMP_Minio__Endpoint: "minio:9000"
      ACMP_Keycloak__Authority: "${KEYCLOAK_AUTHORITY}"   # in-stack: e.g. http://keycloak:8080/realms/acmp (ADR-0015)
    networks:
      - acmp-net
    depends_on:
      sqlserver:
        condition: service_healthy
      minio:
        condition: service_healthy
      seq:
        condition: service_started
      keycloak:
        condition: service_healthy
    healthcheck:
      test: ["CMD", "wget", "-qO-", "http://localhost:8080/healthz"]
      interval: 15s
      timeout: 5s
      start_period: 30s
      retries: 3

  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    restart: unless-stopped
    volumes:
      - sqldata:/var/opt/mssql
    environment:
      ACCEPT_EULA: "Y"
      MSSQL_SA_PASSWORD_FILE: /run/secrets/db_password
    secrets:
      - db_password
    networks:
      - acmp-net
    healthcheck:
      test: ["CMD", "/opt/mssql-tools/bin/sqlcmd", "-S", "localhost", "-U", "sa", "-P", "$$SA_PASSWORD", "-Q", "SELECT 1"]
      interval: 20s
      timeout: 10s
      start_period: 60s
      retries: 5

  seq:
    image: datalust/seq:latest
    restart: unless-stopped
    volumes:
      - seqdata:/data
    environment:
      ACCEPT_EULA: "Y"
      SEQ_FIRSTRUN_ADMINPASSWORDHASH: "${SEQ_ADMIN_HASH}"
    networks:
      - acmp-net

  minio:
    image: minio/minio:latest
    restart: unless-stopped
    command: server /data --console-address ":9001"
    volumes:
      - miniodata:/data
      - minio-config:/root/.minio
    secrets:
      - minio_secret_key
    environment:
      MINIO_ROOT_USER: "${MINIO_ROOT_USER}"
      MINIO_ROOT_PASSWORD_FILE: /run/secrets/minio_secret_key
    networks:
      - acmp-net
    healthcheck:
      test: ["CMD", "mc", "ready", "local"]
      interval: 20s
      timeout: 10s
      retries: 3

  # Self-hosted, bundled IdP — ACMP-owned realm (ADR-0015). Imports the realm bootstrap
  # from ./deploy/keycloak/ on first start; datastore (this volume vs a dedicated
  # Postgres-for-Keycloak) is decided by the PH-0 spike — OQ-038.
  keycloak:
    image: quay.io/keycloak/keycloak:latest
    restart: unless-stopped
    command: ["start", "--import-realm"]
    volumes:
      - ./deploy/keycloak:/opt/keycloak/data/import:ro
      - kcdata:/opt/keycloak/data
    environment:
      KC_HEALTH_ENABLED: "true"
      KC_HOSTNAME_STRICT: "false"
    networks:
      - acmp-net
    healthcheck:
      test: ["CMD-SHELL", "exec 3<>/dev/tcp/localhost/9000; echo -e 'GET /health/ready HTTP/1.1\\r\\nHost: localhost\\r\\nConnection: close\\r\\n\\r\\n' >&3; cat <&3 | grep -q 200"]
      interval: 20s
      timeout: 10s
      start_period: 60s
      retries: 5

  # Phase 2 only — comment out for PH-1
  # tarseem:
  #   image: registry.internal/tarseem:1.0.0
  #   restart: unless-stopped
  #   networks:
  #     - acmp-net

secrets:
  db_password:
    file: ./deploy/secrets/db_password.txt
  minio_secret_key:
    file: ./deploy/secrets/minio_secret_key.txt
  keycloak_client_secret:
    file: ./deploy/secrets/keycloak_client_secret.txt
```

### 2.3 Networks and volumes

| Network / Volume | Type | Purpose |
|---|---|---|
| `acmp-net` | Bridge | Internal service communication; only `acmp-web` has external port binding |
| `sqldata` | Named volume | SQL Server data files; persisted on host |
| `seqdata` | Named volume | Seq log data; persisted on host |
| `miniodata` | Named volume | MinIO object data; persisted on host |
| `minio-config` | Named volume | MinIO config |
| `kcdata` | Named volume | Self-hosted Keycloak data (ADR-0015); final datastore decided by OQ-038. Covered by backup/restore. |

**Security note:** No service except `acmp-web` binds to the host network. Seq, MinIO, and the self-hosted Keycloak admin consoles are accessible only via SSH tunnel or a restricted internal VLAN — never exposed to the public interface.

---

## 3. Externalized Configuration

### 3.1 Config layers (highest precedence last)

1. `appsettings.json` — defaults, non-sensitive, committed to VCS.
2. `appsettings.{Environment}.json` — environment-specific overrides (dev/staging/prod); committed (no secrets).
3. Environment variables (`ACMP_*` prefix, `__` separator for nested keys) — set via `env_file` + Docker secrets.
4. Docker secrets files (mounted at `/run/secrets/`) — passwords, API keys, certificates.

### 3.2 Environment-specific config files

| File | Location | Contains | VCS? |
|---|---|---|---|
| `appsettings.json` | `src/Acmp.Api/` | Defaults, logging config, feature flags | Yes |
| `appsettings.Development.json` | `src/Acmp.Api/` | Local dev overrides (LocalDB, no TLS) | Yes (no secrets) |
| `appsettings.Staging.json` | `src/Acmp.Api/` | Staging-specific (Seq URL, MinIO host) | Yes (no secrets) |
| `appsettings.Production.json` | `src/Acmp.Api/` | Prod-specific feature flags | Yes (no secrets) |
| `deploy/env/acmp-api.env` | `deploy/env/` | Non-secret env vars per environment | Yes (template); actual = `.gitignore`d |
| `deploy/env/acmp-api.env.example` | `deploy/env/` | Documented template with placeholder values | Yes |

### 3.3 Secret handling

- **Never commit secrets** to VCS. `.gitignore` excludes all `deploy/secrets/`, `.env`, `*.env.local`.
- Secrets stored as plain text files in `deploy/secrets/` on the VM (directory chmod 700, owner = deploy user).
- Docker Compose `secrets:` mounts them at `/run/secrets/<name>`; the API reads via `SecretFileConfigProvider` or custom configuration provider.
- Secret rotation: update the file, `docker compose up -d acmp-api` — no image rebuild needed.
- **Gitleaks pre-commit hook** and CI scan guard against accidental commits (ADR: `docs/domain/devsecops-plan.md` §4.3).

---

## 4. Health and Readiness Endpoints

Implemented via `Microsoft.Extensions.Diagnostics.HealthChecks` (ASP.NET Core built-in).

| Endpoint | Path | Checks | Used by |
|---|---|---|---|
| **Liveness** | `GET /healthz` | Application started; no deadlock | Docker HEALTHCHECK; nginx upstream check |
| **Readiness** | `GET /readyz` | SQL Server reachable; MinIO reachable; Seq reachable; Hangfire scheduler running | Deploy smoke test; startup gate |
| **Hangfire dashboard** | `/hangfire` | (UI, protected — Secretary/Admin role only) | Ops monitoring |

Response shape:
```json
{
  "status": "Healthy",
  "results": {
    "sqlserver": { "status": "Healthy", "duration": "00:00:00.012" },
    "minio":     { "status": "Healthy", "duration": "00:00:00.008" },
    "seq":       { "status": "Degraded", "duration": "00:00:01.002" }
  }
}
```

`/readyz` returns 503 if any critical check is `Unhealthy`; deploy script fails and triggers rollback.

---

## 5. EF Core Migration Strategy

**Decision:** Run migrations **as a dedicated migration job at container startup**, before the API begins serving traffic. **Not** at runtime on every request; **not** as a separate migration container.

**Mechanism:**
```csharp
// Program.cs (or a startup extension)
using var scope = app.Services.CreateScope();
foreach (var context in scope.ServiceProvider.GetServices<DbContext>())
{
    await context.Database.MigrateAsync();   // idempotent; skips applied migrations
}
```

**Rationale:**
- Keeps migration and runtime in the same deployable (simpler for on-prem single-VM).
- `MigrateAsync()` is idempotent — safe on every restart.
- Migration runs before `app.Run()` so the API never serves traffic against a stale schema.
- Each module has its own `DbContext` with its own SQL schema; migrations are per-context and run in dependency order (Shared Kernel → platform modules → domain modules).

**Migration order (enforced in startup):**
```
PlatformDbContext (schema: platform)  →  first (base tables, outbox)
MembershipDbContext                   →  second
TopicsDbContext, MeetingsDbContext, … →  remaining domain modules
AuditDbContext                        →  last (references subject IDs from other schemas)
```

**Guards:**
- Migration test in CI (stage 10) runs full migrate-from-0 + idempotency check.
- If `MigrateAsync` throws → startup fails → container unhealthy → Compose restarts → deploy pipeline detects unhealthy and aborts.
- Never use `EnsureCreated()` in production; only migrations.
- Rollback: restore SQL backup (§6) — there is no `MigrateDown()` in production.

---

## 6. Backup and Restore

### 6.1 Backup strategy

| Data | Method | Frequency | Destination | Retention |
|---|---|---|---|---|
| SQL Server | Native `BACKUP DATABASE` to `.bak` file | Nightly (Hangfire job, 02:00) | Standby VM (scp/rsync) + MinIO bucket `acmp-backups` | 30 days rolling |
| MinIO objects | `mc mirror` [unverified: MinIO Client `mc` version] from primary to standby MinIO data path | Nightly after SQL backup | Standby VM MinIO data dir | 30 days rolling |
| Config + secrets | Manual: compressed archive of `deploy/` directory | On every deploy + monthly | Encrypted storage (team password manager / org secure store) | Indefinite |

**Backup job (Hangfire):**
```
RecurringJob.AddOrUpdate("nightly-backup", () => backupService.RunAsync(), Cron.Daily(2, 0));
```
`IBackupService` implementation:
1. `BACKUP DATABASE [acmp] TO DISK = '/tmp/acmp_<date>.bak' WITH COMPRESSION, STATS = 10`
2. `scp /tmp/acmp_<date>.bak standby-vm:/opt/acmp-backups/`
3. `mc mirror /data standby-vm:/opt/minio-mirror/`
4. Log result to Seq; send in-app notification to Administrator if failure.

### 6.2 Restore runbook

**Scenario: primary VM failure, restore to standby VM.**

1. Confirm primary VM unreachable.
2. SSH to standby VM.
3. Verify latest backup file: `ls -lt /opt/acmp-backups/*.bak | head -3`.
4. Start SQL Server on standby: `docker compose -f /opt/acmp/docker-compose.yml up -d sqlserver`.
5. Restore database:
   ```sql
   RESTORE DATABASE [acmp]
   FROM DISK = '/opt/acmp-backups/acmp_<date>.bak'
   WITH REPLACE, STATS = 10;
   ```
6. Verify: `SELECT COUNT(*) FROM acmp.decisions.Decisions` (expect non-zero).
7. Verify MinIO data: `du -sh /opt/minio-mirror/` (expect non-zero).
8. Update DNS (or nginx upstream) to point to standby VM IP.
9. Start full stack: `docker compose up -d`.
10. Run smoke test: `curl https://<standby-ip>/healthz` and `curl https://<standby-ip>/readyz`.
11. Announce restoration to committee (in-app notification + direct message).
12. Estimated RTO: ≤ 8 hours (NFR-056). RPO: ≤ 4 hours (last successful nightly backup, NFR-057).

---

## 7. Warm Standby and 99.9% Availability

**Target:** 24×7 / 99.9% (~8.76 hours downtime per year — NFR-014). **Mechanism:** warm standby VM + nightly backups + fast container restart; **not** an HA cluster (disproportionate for ≤20 users; violates C-SCALE).

```
Primary VM (active)        Standby VM (warm)
────────────────────       ────────────────────
Docker Compose stack  →    Nightly SQL + MinIO backup synced
nginx + acmp-api           Docker Compose files present
sqlserver, seq, minio      Docker images pre-pulled (from registry)
                           Standby is NOT running the stack (cold-warm)
                           Can be started in < 15 min (RTO: 8h budget)
```

**Downtime budget breakdown (99.9% = 8.76 h/yr):**
- Planned maintenance windows: 2 × 30-min monthly = 12 h/yr → **use 99.9% target for unplanned only; maintenance in low-use windows.**
- Container auto-restart on crash: ≤ 2 min (NFR-016; `restart: unless-stopped`).
- Full-VM failure → standby restore: ≤ 8 h (NFR-056).

**Improvement path (if 99.9% proves insufficient):** add a second compose stack on standby with replication; this is an architecture review item, not a v1 concern.

---

## 8. Upgrade and Rollback Procedure

### 8.1 Upgrade

1. Build new images → CI pipeline tags with `v<semver>-<sha>`.
2. Push images to local registry.
3. Update `IMAGE_TAG` in `deploy/env/acmp-api.env`.
4. Commit the change → pipeline promotes to staging → E2E pass → lead approval → prod deploy.
5. Prod deploy: `docker compose pull && docker compose up -d --remove-orphans`.
6. Smoke test (§6.2 steps 9–10 equivalent).
7. Tag release in VCS.

### 8.2 Rollback

1. Identify previous `IMAGE_TAG` from release history.
2. `IMAGE_TAG=<previous-tag> docker compose up -d` on prod VM.
3. Wait for `service_healthy` (≤ 60 s).
4. Smoke test.
5. If DB schema changed: restore from last-known-good backup (§6.2). This is the only safe schema rollback.
6. Post-mortem created; incident documented in Seq + audit log.

---

## 9. Resource Sizing (≤20 Users)

**Right-sized for low-traffic, on-prem, single committee.** No horizontal scaling; vertical only.

| Service | CPU (cores) | RAM | Disk |
|---|---|---|---|
| `acmp-api` (+ Hangfire) | 2 (limit 4) | 512 MB–1 GB | Minimal (logs to Seq) |
| `sqlserver` | 2 (limit 4) | 2 GB (limit 4 GB) | 50 GB SSD (data + backups) |
| `acmp-web` (nginx) | 0.5 | 128 MB | Minimal |
| `seq` | 1 | 512 MB | 10 GB (log retention 90 days) |
| `minio` | 1 | 512 MB | 100 GB HDD (attachments, recordings) |
| `tarseem` (Phase 2) | 1 | 512 MB | Minimal |
| **Total per VM** | **~8 cores** | **~6–8 GB RAM** | **~200 GB** |

**Recommended VM spec (primary + standby):** 8–16 vCPU, 16 GB RAM, 500 GB SSD (primary), 500 GB HDD (standby). These are generous headroom figures; actual usage at ≤20 users will be a fraction [unverified: actual usage profile pending load test — see `docs/validation/test-strategy.md` §3.11].

---

## 10. Deployment Runbook (Numbered)

### First-time deployment (initial install)

1. Provision primary VM: install Docker Engine + Docker Compose plugin; create `deploy` OS user.
2. Clone repo to `/opt/acmp/`; checkout `main`.
3. Configure secrets: create `deploy/secrets/` (chmod 700); populate `db_password.txt`, `minio_secret_key.txt`, `keycloak_client_secret.txt`.
4. Copy TLS certificates to `deploy/nginx/certs/` (cert + key).
5. Configure `deploy/env/acmp-api.env` from `acmp-api.env.example`; set `KEYCLOAK_AUTHORITY` (points at the in-stack `keycloak` service — ADR-0015), `IMAGE_TAG`.
6. Pull images: `docker compose pull`.
7. Start infrastructure first: `docker compose up -d keycloak sqlserver seq minio`. Keycloak is a bundled in-stack service (ADR-0015): on first start it imports the ACMP realm from the realm bootstrap under `deploy/keycloak/` and uses its own datastore (per OQ-038).
8. Wait for healthy: `docker compose ps` until all show `healthy` (up to 90 s).
9. Create MinIO buckets: `docker compose run --rm acmp-api dotnet Acmp.Api.dll --migrate-only` (or a dedicated setup command [unverified: exact CLI flag]).
10. Start full stack: `docker compose up -d`.
11. Run smoke test: `curl https://localhost/healthz` → 200; `curl https://localhost/readyz` → 200.
12. Seed initial Administrator user via the self-hosted Keycloak admin console (ADR-0015); assign `Administrator` realm role.
13. Verify login: open browser → ACMP URL → redirected to Keycloak → log in → land on dashboard.
14. Run backup test: trigger nightly-backup Hangfire job manually from dashboard; verify `.bak` file created.
15. Provision standby VM: copy `deploy/` dir; pull images; confirm network reachability.
16. Document completion in `deploy/runbooks/install-log.md`.

### Routine upgrade (subsequent releases)

1. Merge feature PRs → staging auto-deploy + E2E passes.
2. Lead approves release in CI.
3. `docker compose pull` on prod VM (new images from registry).
4. `docker compose up -d --remove-orphans` (rolling restart; EF migrations run on API startup).
5. Smoke test.
6. Tag release in VCS (`git tag v<version>`).

---

## Traceability

Links: `docs/domain/devsecops-plan.md` (CI pipeline that builds and scans these images) · `docs/validation/test-strategy.md` (container smoke tests, backup/restore tests) · `docs/domain/repository-structure.md` (`deploy/` directory layout, Dockerfiles) · `docs/domain/architecture-detail.md` §4 (container topology C4 Level 2) · `docs/requirements/non-functional.md` (NFR-014 availability, NFR-055 no K8s, NFR-056/057 RTO/RPO) · `../README.md` §A (CON-001, ADR-0003 SQL Server, C-DEPLOY, C-SCALE).
