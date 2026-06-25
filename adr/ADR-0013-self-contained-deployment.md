# ADR-0013: Self-Contained On-Premises Deployment (CON-001)

- Status: Accepted
- Date: 2026-06-24
- Deciders: Architecture Committee (secretary-confirmed)

## Context and Problem Statement

The organization operates a shared runtime infrastructure: Hangfire (background jobs), ELK+Seq (observability), centralized notification platform (Email/SMS/Firebase), shared databases. ACMP is a high-sensitivity governance tool — its availability, data integrity, and audit trail must not depend on the operational state or upgrade schedule of shared infrastructure. The deployment model must be specified explicitly to avoid accidental coupling.

## Decision Drivers

- CON-001 (hard constraint): ACMP must not depend on the org's shared Hangfire, ELK/Seq, or notification platform. It builds and bundles its own equivalents.
- Scale: ≤20 users, low traffic, on-prem VM. Kubernetes, service mesh, and horizontal auto-scaling are unjustified operational complexity at this scale.
- Availability target: 24×7 / 99.9% — achievable via a single redundant VM with nightly backups and Docker Compose restart policies, without a full HA cluster.
- Docker Compose is the right abstraction for a single-VM deployment of 4–6 containers; it provides reproducibility, environment isolation, and declarative service definition without orchestration overhead.
- Two deliberate exceptions are explicitly allowed: SQL Server (the org-mandated datastore, running as ACMP's own instance) and Keycloak (SSO identity provider, federated per brief mandate). Webex is a Phase-2 external SaaS integration, not shared runtime infra.

## Considered Options

1. **On-prem VM + Docker Compose; ACMP bundles own Hangfire (on ACMP SQL) + Seq + MinIO; SQL Server own instance; Keycloak federated** — self-contained, operable, low complexity.
2. **Kubernetes (K8s)** — unjustified for ≤20 users and a single team; no horizontal scaling requirement; adds cert-manager, ingress controllers, RBAC, etcd backup complexity; rejected.
3. **Bare-metal / IIS deployment (no containers)** — less reproducible; harder to add sidecars (Tarseem Phase 2, Seq, MinIO); rejected.
4. **Cloud-hosted (Azure, AWS)** — contradicts on-prem constraint and data-sovereignty requirements for a sensitive gov governance tool; rejected.
5. **Use org's shared Hangfire + ELK + notification platform** — explicitly excluded by CON-001.

## Decision Outcome

Chosen option: "On-prem VM + Docker Compose; all ACMP infrastructure bundled as its own containers", because CON-001 is a hard constraint, Docker Compose is the correct orchestration tier for this scale, and bundling every runtime dependency (except SQL Server and Keycloak) in the Docker Compose stack makes the platform self-contained, reproducible, and operationally simple.

### Consequences

- Good: ACMP is deployable on any VM that can run Docker without org infrastructure dependencies; upgrades of shared org infrastructure do not affect ACMP; the deployment is fully reproducible from the `docker-compose.yml` + env files; availability target is achievable with Docker Compose restart policies and VM-level redundancy; no K8s operational expertise needed.
- Bad / trade-off: SQL Server and Keycloak are external to the Docker Compose stack (they run on the VM or a nearby server but are not bundled in the ACMP compose file) — ACMP has a startup dependency on both. If Keycloak is down, ACMP login fails (mitigated: JWT validation is local; existing sessions continue). If SQL Server is down, ACMP is fully unavailable — SQL Server's own HA (mirroring, AlwaysOn) is the mitigation, not ACMP's architecture.

## Validation

- Deployment test: fresh VM with Docker installed, `docker compose up`, all services start and pass health checks within 2 minutes.
- Self-contained check: `docker compose` file contains no references to org-shared services (linted); any external hostname must be Keycloak, SQL Server, or Webex (Phase 2 only).
- Backup/restore test: nightly backup script tested on staging — restore from backup produces a working ACMP instance within the defined RTO.
- Health check endpoints: each container exposes `/health` or equivalent; Docker Compose `healthcheck` stanzas confirm readiness before dependent containers start.

## Links / Notes

- Docker Compose service inventory (v1): `acmp-app` (.NET), `acmp-sqlserver` (SQL Server), `acmp-seq` (Serilog/OpenTelemetry backend), `acmp-minio` (object storage). Phase 2 adds: `acmp-tarseem` (render sidecar).
- Availability: 99.9% ≈ 8.7 hours downtime/year — achievable via VM-level redundancy (hot standby or snapshot restore), nightly SQL backups, and Docker Compose restart-always policies.
- Secrets management: environment variables injected via Docker secrets or `.env` files (never committed to source); documented in `docs/33-containerization-and-deployment.md`.
- CON-001 is a governing constraint for all ADRs in this package; violations require explicit secretary approval and a new ADR.
- Related: ADR-0001 (modular monolith → single deployable), ADR-0003 (SQL Server — app-owned instance), ADR-0004 (Keycloak — federated, not bundled), ADR-0014 (Hangfire + Seq + MinIO bundled).
