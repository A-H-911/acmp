# ACMP Architecture Decision Records

This folder contains the Architecture Decision Records (ADRs) for the Architecture Committee Management Platform (ACMP). All records use the **MADR** (Markdown Architectural Decision Records) format. See: https://adr.github.io/madr/

---

## ADR Index

| ID | Title | Status | Date |
|----|-------|--------|------|
| [ADR-0001](ADR-0001-modular-monolith.md) | Modular Monolith as Macro-Architecture | Accepted | 2026-06-24 |
| [ADR-0002](ADR-0002-dotnet-clean-architecture.md) | .NET 8 + ASP.NET Core + Clean Architecture + Vertical Slice + EF Core + REST | Accepted | 2026-06-24 |
| [ADR-0003](ADR-0003-sql-server-single-datastore.md) | Microsoft SQL Server as the Single Datastore | Accepted | 2026-06-24 |
| [ADR-0004](ADR-0004-keycloak-oidc-identity.md) | Keycloak (OIDC) as Identity Provider | Accepted | 2026-06-24 |
| [ADR-0005](ADR-0005-notification-channel-abstraction.md) | Notification Channel Abstraction (INotificationChannel) | Accepted | 2026-06-24 |
| [ADR-0006](ADR-0006-tarseem-render-sidecar.md) | Tarseem as Containerized Render Sidecar (JSON Spec = Source of Truth) | Accepted | 2026-06-24 |
| [ADR-0007](ADR-0007-keystone-optional-companion.md) | Keystone as Optional Companion (Not Embedded, Not a Hard Dependency) | Accepted | 2026-06-24 |
| [ADR-0008](ADR-0008-traceability-relationship-model.md) | Typed Directed Relationship Edges as the Traceability Model | Accepted | 2026-06-24 |
| [ADR-0009](ADR-0009-audit-immutability.md) | Append-Only Audit Log with Immutability and Hash-Chain Integrity | Accepted | 2026-06-24 |
| [ADR-0010](ADR-0010-voting-model.md) | Always-Attributed Voting Model | Accepted | 2026-06-24 |
| [ADR-0011](ADR-0011-search-sqlserver-fts.md) | SQL Server Full-Text Search in v1; Self-Hosted OpenSearch if Outgrown | Accepted | 2026-06-24 |
| [ADR-0012](ADR-0012-react-typescript-frontend.md) | React 18 + TypeScript + Vite Frontend (amended by ADR-0015) | Accepted | 2026-06-24 |
| [ADR-0013](ADR-0013-self-contained-deployment.md) | Self-Contained On-Premises Deployment (CON-001) | Accepted | 2026-06-24 |
| [ADR-0014](ADR-0014-background-jobs-observability.md) | Background Jobs (Hangfire), Observability (Serilog + OpenTelemetry + Seq), Object Storage (MinIO) | Accepted | 2026-06-24 |
| [ADR-0015](ADR-0015-react-19-amends-0012.md) | Adopt React 19 (amends ADR-0012) | Accepted | 2026-06-25 |

---

## Format

All ADRs in this repository use the **MADR** (Markdown Architectural Decision Records) template. Canonical template reference: https://adr.github.io/madr/

---

## In-Repository ADR Process

### Numbering

ADRs are numbered sequentially: `ADR-0001`, `ADR-0002`, etc. Numbers are never reused. When an ADR is superseded, the old number is retained and the old ADR's status field is updated to `Superseded by ADR-XXXX`. The superseding ADR gets the next available number.

### Lifecycle

```
Draft → Proposed → Accepted → Superseded | Deprecated
```

| Status | Meaning |
|--------|---------|
| Draft | Being written; not yet reviewed. |
| Proposed | Complete; under review by the Architecture Committee. |
| Accepted | Approved by the Architecture Committee; binding on the project. |
| Superseded | Replaced by a later ADR (referenced in status field); original preserved. |
| Deprecated | No longer applicable but not replaced (context has changed); preserved for historical record. |

**Superseded ADRs are never edited.** The status field is updated to `Superseded by ADR-XXXX` and the body is left intact. All context, trade-offs, and rejected alternatives from the original remain readable.

### Proposing a New ADR

1. Create a new file: `ADR-NNNN-short-title.md` using the next available number.
2. Copy the blank MADR template below.
3. Set status to `Draft` and fill in all sections.
4. Set status to `Proposed` and submit for Architecture Committee review.
5. On approval, the secretary updates status to `Accepted` and records the decision date.

---

## Blank MADR Template

```markdown
# ADR-00NN: <Title>

- Status: Draft | Proposed | Accepted | Superseded by ADR-XXXX | Deprecated
- Date: YYYY-MM-DD
- Deciders: <names or "Architecture Committee">

## Context and Problem Statement

<Describe the context and problem this decision addresses. What is the question being decided?>

## Decision Drivers

- <Driver 1 — constraint, requirement, principle>
- <Driver 2>

## Considered Options

1. **<Option A>** — <brief description>
2. **<Option B>** — <brief description>
3. **<Option C>** — <brief description>

## Decision Outcome

Chosen option: "<Option X>", because <justification tied to decision drivers>.

### Consequences

- Good: <positive outcome>
- Good: <positive outcome>
- Bad / trade-off: <negative or trade-off>
- Bad / trade-off: <negative or trade-off>

## Validation

<How will you verify this decision is working as intended? Tests, metrics, review gates.>

## Links / Notes

- <Related ADR or document>
- <External reference URL>
```
