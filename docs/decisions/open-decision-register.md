---
status: Approved
version: 1.0.0
updated: 2026-07-06
owner: lead-secretary
---

# Open-Decision Register — ACMP

These entries mirror the settled canon from `docs/decisions/open-decision-register.md §A` (confirmed by the secretary 2026-06-24). Every row is settled and overrides anything contradicting it elsewhere. They map one-to-one from the former `R-##` ids: **R-01 → DEC-001, R-02 → DEC-002, … R-27 → DEC-027** (add 100-based numbering: `R-0N → DEC-0NN`). "Promoted to" carries the source ADR where one exists; `n/a (canon)` means the decision is canon recorded in the README/CON register with no dedicated ADR.

| ID | Decision | Status | Rationale | Promoted to | Trigger to revisit |
|---|---|---|---|---|---|
| DEC-001 | Deployment model — on-prem VM(s) + Docker Compose; no Kubernetes, service mesh, or cloud PaaS | Approved | Right-sized for an on-prem, low-traffic ≤20-user footprint (CON-003) | ADR-0001 | Reopen only via new ADR |
| DEC-002 | Self-contained constraint — ACMP bundles its own background processing, observability, and notification channels; no org Hangfire, ELK, Seq, or notification platform | Approved | Zero external runtime dependency in v1 (CON-001) | n/a (canon) | Reopen only via new ADR |
| DEC-003 | Object storage — self-hosted MinIO (S3-compatible) as an ACMP-owned container; `IFileStore` abstraction in code; metadata in SQL Server | Approved | S3-compatible storage without a cloud dependency (DEP-003) | ADR-0003 | Reopen only via new ADR |
| DEC-004 | Background jobs — app-owned Hangfire on ACMP's own SQL schema; dashboard for Administrator; SQL outbox for durability | Approved | Durable jobs without the org's Hangfire | n/a (canon) | Reopen only via new ADR |
| DEC-005 | Email in v1 — none; the in-app notification center is the sole channel; `INotificationChannel` keeps adding email a zero-code change | Approved | No confirmed SMTP relay yet (CON-006) | ADR-0005 | Reopen only via new ADR |
| DEC-006 | Identity / roles — Keycloak (OIDC, auth-code + PKCE) is the IdP; global roles sourced only from Keycloak claims; no self-registration | Approved | ACMP stores or assigns no roles (CON-007, CON-009) | ADR-0004 | Reopen only via new ADR |
| DEC-007 | Voting attribution — always attributed in v1; every voter's choice is recorded and visible to Auditor and Chairman | Approved | No anonymous voting (CON-011) | ADR-0010 | Reopen only via new ADR |
| DEC-008 | Stream visibility — all members read topics across all streams; write, create, and edit are restricted by role and ownership | Approved | Read-broad, write-scoped default | n/a (canon) | Reopen only via new ADR |
| DEC-009 | Single committee — exactly one committee; no multi-committee, tenant discriminator, or sub-committee generalization in v1 | Approved | No multi-tenant need at this scale (CON-010) | n/a (canon) | Reopen only via new ADR |
| DEC-010 | Webex integration phase — Webex is Phase 2 (notification card adapter + recording/transcript retrieval); no Webex calls in PH-1 code | Approved | Phased scope (CON-014) | ADR-0005 | Reopen only via new ADR |
| DEC-011 | Recordings and transcripts — PH-1 Secretary pastes a link or uploads manually; PH-2 uses the Webex Recordings + Transcripts APIs | Approved | Manual-first, phased delivery (D-02) | n/a (canon) | Reopen only via new ADR |
| DEC-012 | AI extraction — Phase 3 only, manual first, always human-reviewed; off by default; Admin activation requires data-residency confirmation | Approved | No unreviewed AI content enters the record (RISK-012, CON-015) | ADR-0007 | Reopen only via new ADR |
| DEC-013 | Retention policy — keep all records, no auto-purge in v1; retention configurable; votes, decisions, ADRs, and minutes are immutable | Approved | Legal/compliance can set periods later (FR-155) | n/a (canon) | Reopen only via new ADR |
| DEC-014 | Availability target — 24×7 / 99.9% via standby VM + nightly backups; no HA cluster, no horizontal scaling | Approved | Meets the SLA at ≤20-user scale (ASM-008) | n/a (canon) | Reopen only via new ADR |
| DEC-015 | Calendar / dates — Gregorian only, no Hijri in v1; localized Gregorian formatting in AR is acceptable | Approved | Simplicity at v1 scope (CON-012) | n/a (canon) | Reopen only via new ADR |
| DEC-016 | Keystone integration — optional; the Research module works fully standalone; Keystone import is a Phase 2 enhancement, never a hard dependency | Approved | No hard external dependency (CON-013) | ADR-0007 | Reopen only via new ADR |
| DEC-017 | Scale ceiling — on-prem, low traffic, ≤20 total users; reject architectural additions whose only justification is scaling beyond 20 | Approved | Right-size everything (CON-004) | n/a (canon) | Reopen only via new ADR |
| DEC-018 | Audit hash chain — SHA-256 chained hash over vote and issued-decision audit records | Approved | Enables independent integrity verification by Auditor (BL-067) | ADR-0009 | Reopen only via new ADR |
| DEC-019 | Tarseem integration phase — the diagram sidecar is Phase 2; the JSON spec is the version-controlled source of truth; a broken render never blocks | Approved | Phased; spec-as-source-of-truth | ADR-0006 | Reopen only via new ADR |
| DEC-020 | Primary datastore — Microsoft SQL Server only (transactional + columnstore reporting + FTS); no second database in v1 | Approved | One ACMP-owned store | ADR-0003 | Reopen only via new ADR |
| DEC-021 | Backend stack — .NET 8 (LTS), ASP.NET Core, REST, Clean Architecture per module, vertical-slice MediatR handlers, EF Core | Approved | Settled stack (CON-002) | ADR-0002 | Reopen only via new ADR |
| DEC-022 | Frontend stack — React 18 + TypeScript + Vite; react-i18next (EN/AR); RTL via CSS logical properties + `dir`; light/dark; `@dnd-kit` DnD | Approved | Settled stack (CON-002, CON-008) | ADR-0012 | Reopen only via new ADR |
| DEC-023 | Architecture style — modular monolith, single deployable; in-process contracts only; no cross-module DB access; microservices rejected for v1 | Approved | Right-sized module boundaries | ADR-0001 | Reopen only via new ADR |
| DEC-024 | Search in v1 — SQL Server Full-Text Search; an app-owned self-hosted search (e.g. OpenSearch) only if FTS outgrows scale, never the org's ELK | Approved | Self-contained search | ADR-0011 | Reopen only via new ADR |
| DEC-025 | Traceability model — explicit typed relationship model (directed edges over shared Artifact identity); graph traversal in SQL | Approved | Typed edges give semantic precision | ADR-0008 | Reopen only via new ADR |
| DEC-026 | Observability — Serilog + self-hosted Seq container (ACMP-owned) + OpenTelemetry traces/metrics; no dependency on the org's ELK/Seq | Approved | Self-contained observability | n/a (canon) | Reopen only via new ADR |
| DEC-027 | Optimistic concurrency — every mutable aggregate root carries `RowVersion`; a stale write throws `DbUpdateConcurrencyException` → API returns 409 | Approved | Chosen over last-writer-wins and pessimistic locking; implemented 2026-06-30 (resolves OQ-043) | ADR-0018 | Reopen only via new ADR |
