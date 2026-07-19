# ADR-0032: File-backed Docker secrets for all runtime credentials

- Status: Accepted
- Date: 2026-07-19 (Proposed); ratified 2026-07-19
- Deciders: Architecture Committee execution; operator ratified 2026-07-19
- Context: P18 (Deployment) — Batch 1, realizing `deployment.md §3.3` (externalized/secret config)

## Context and Problem Statement

Until now the bundled stack injected every credential as a **plain environment variable** interpolated by Docker
Compose from a git-ignored `deploy/.env` (`${MSSQL_SA_PASSWORD}`, `${MINIO_ROOT_PASSWORD}`, the DB password inside
`ConnectionStrings__Acmp`, the Keycloak DB/admin passwords, Webex secrets). No secret *value* is committed, but the
values are visible to anyone with Docker access via `docker inspect` / `/proc/<pid>/environ`, and the release
Definition of Done requires that no secret material live in `docker-compose.yml` or the images. `deployment.md §3.3`
specifies Docker secrets mounted at `/run/secrets`. This ADR records adopting that mechanism.

## Decision Drivers

- **Release DoD** — "all secrets externalized; none in `docker-compose.yml`/Dockerfiles" (Level-3 DoD, Security).
- **ASVS L2 secret management** (NFR-024, NFR-018) — reduce credential exposure surface on the host.
- **Operator scope decision** — the operator chose secrets **everywhere** (base + prod), accepting the dev-loop cost
  knowing the dev/CI values are public placeholders, to keep one mechanism across every environment.
- **Proportionality / self-containment (CON-001)** — no external secret manager (Vault/KMS); file-backed secrets on
  the VM, the operator owns key custody (§3.3), consistent with the on-prem, ≤20-user posture.

## Considered Options

1. **Env-var from git-ignored `.env`** (status quo). Rejected: satisfies "no secret in source" but not "no secret in
   the running compose/inspect surface"; misses the DoD wording and ASVS intent.
2. **File-backed Docker secrets everywhere** (chosen). Each credential is a file under `deploy/secrets/` mounted at
   `/run/secrets/<name>`. The .NET hosts read them natively via `AddKeyPerFile("/run/secrets")` (`__` → `:`); the
   infra images via their `_FILE` convention (Postgres, MinIO) or a thin entrypoint shim (SQL Server, Keycloak).
3. **External secret manager (Vault/KMS/Docker Swarm secrets service).** Rejected: violates self-containment
   (CON-001/INV-003) and is disproportionate for ≤20 users on a single host.

## Decision Outcome

Chosen: **option 2.**

- **Generation** — `deploy/scripts/gen-secrets.sh` materializes `deploy/secrets/*` (git-ignored, mode 600) from
  `deploy/.env`(.example) before compose parses. `deploy/scripts/up.sh` wraps gen-secrets + `docker compose up` as
  the supported single command (NFR-052). CI runs gen-secrets from the committed placeholder `.env.example`.
- **.NET hosts** — `builder.Configuration.AddKeyPerFile("/run/secrets", optional: true)` (first-party
  `Microsoft.Extensions.Configuration.KeyPerFile`), added last so a mounted secret outranks appsettings/env, and
  optional so a local `dotnet run` without the mount is unaffected. Secret files are named by config key
  (`ConnectionStrings__Acmp`, `Minio__SecretKey`). **`gen-secrets` writes with `printf '%s'` (never `echo`)** because
  KeyPerFile uses the file content verbatim — a trailing newline would corrupt the connection string.
- **Infra images** — Postgres and MinIO use their native `POSTGRES_PASSWORD_FILE` / `MINIO_ROOT_PASSWORD_FILE`. SQL
  Server (no native `_FILE`) uses an image entrypoint shim (`deploy/scripts/mssql-entrypoint.sh`) that exports the
  password from `MSSQL_SA_PASSWORD_FILE` then `exec`s `sqlservr` — a **no-op when the var is unset**, so non-secret
  usage is unchanged. Keycloak uses a compose entrypoint wrapper that exports its DB + bootstrap passwords from the
  mounted files, then execs `kc.sh` (its `_FILE` support is unreliable across versions).

## Consequences

- **Positive:** no credential value appears in `docker-compose.yml`, the images, or `docker inspect`; verified by a
  rendered-config scan. One mechanism across base/e2e/prod. The `Webex__*` secrets are materialized only when
  `WEBEX_ENABLED=true`, so a disabled adapter leaves no empty files.
- **Negative / accepted:** a bare `docker compose up` is no longer sufficient — the secret files must exist first, so
  `up.sh`/`gen-secrets.sh` (or the CI step) is mandatory; the mssql/Keycloak shims add a thin, tested entrypoint layer;
  and dev/CI still use public placeholder values (zero hardening benefit there, accepted as the cost of one uniform
  mechanism). `chmod 600` is honored on Linux/CI; on a Windows dev host NTFS ignores it (cosmetic — the dir is
  git-ignored regardless).

## Traceability

Realizes `deployment.md §3.3` (secret handling). Supports NFR-024 (0 secrets in source/images), NFR-018 (ASVS L2),
NFR-052 (single-command bring-up via `up.sh`). Pairs with ADR-0031 (the least-priv DB login whose password is one of
these secrets) and the P18a Batch-3 migrator/runtime split. No AC verdict change (INV-007 strengthened).
