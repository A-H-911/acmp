# ADR-0031: Database-permission audit immutability (least-priv role, DENY UPDATE/DELETE on schema::audit)

- Status: Proposed
- Date: 2026-07-16
- Deciders: Architecture Committee execution (awaiting operator ratification)
- Context: P16 (Security hardening) — Batch 1, closing part of deferred-work item D-16

## Context and Problem Statement

The `AuditEvent` store is immutable **at the application layer**: the entity has no public setters and no
delete path, `SaveChanges` only ever `Add`s, and the hash chain makes tampering detectable (ADR-0009/0026).
But the *database* does not yet enforce append-only: a principal with direct SQL access (persona P3 / P4,
threat T-06, abuse-case AB-5) could `UPDATE`/`DELETE` `audit.AuditEvents` rows out-of-band. Control
**C-AUDIT-04** requires DB-permission segregation — the application's SQL principal must hold **no UPDATE/DELETE
grant on `audit.*`**. This is the DB-permission half of deferred-work item **D-16** (the nightly verify job is
the other half, delivered in the same batch under ADR-0030/C-INS-02).

## Decision Drivers

- **Defense-in-depth for the evidence layer** (asset A3): if the audit log can be silently altered, every other
  integrity claim collapses. This warrants L3-grade treatment even though the system targets L2.
- **Insider/DBA threat** (P3), on-prem, ≤20 users; no HSM (RISK-SEC-002).
- **Proportional** — reuse SQL Server's native permission model; avoid heavy machinery.

## Considered Options

1. **App-layer immutability only** (status quo). Rejected: does not defend a direct-SQL write by a privileged
   principal — the actual C-AUDIT-04 gap.
2. **`DENY UPDATE, DELETE ON SCHEMA::audit` to a least-priv application role** (chosen). The DB itself refuses
   audit mutation from that principal; the app keeps `SELECT` + `INSERT` (append-only).
3. **Triggers / temporal / SQL Server ledger tables.** Rejected: heavier and more coupling; ledger is a
   later-edition/enterprise feature and duplicates the hash-chain guarantee we already have — disproportionate.

## Decision Outcome

Chosen: **option 2.** Migration `Audit_DenyMutation` (on `AuditDbContext`, schema `audit`), idempotent:

```
IF DATABASE_PRINCIPAL_ID('acmp_app') IS NULL CREATE ROLE acmp_app;
GRANT  SELECT, INSERT ON SCHEMA::audit TO acmp_app;   -- append + read only
DENY   UPDATE, DELETE ON SCHEMA::audit TO acmp_app;   -- no mutation, no deletion
```

**Operator / P18 responsibility (the effectiveness boundary):** the control is only *effective* once the runtime
application connects as a login **mapped to `acmp_app` and NOT a member of `sysadmin` / not the `dbo` user** —
because `sysadmin` and `dbo` bypass `DENY`. In the shipped dev and e2e stacks the app currently connects as
**`sa`**, so the DENY is **inert there today**. Provisioning the least-priv app login (and its secret) is
**P18/operator** work; this migration ships the role + grants now so the switch is a configuration change, not a
code change (NFR-053).

## Consequences

- **Positive:** once the app runs least-priv, the database refuses any UPDATE/DELETE on `audit.AuditEvents` from
  the application principal — closing the C-AUDIT-04 / D-16 DB-permission gap behind the already-`Met`
  AC-017/018. Harmless on current stacks (the `acmp_app` role has no members yet). Proven under a restricted
  login by `AuditImmutabilityDbPermissionTests` (Testcontainers, real SQL Server).
- **Negative / accepted:** **inert until the operator switches the app login** (tracked as a P18/operator
  residual and stated in the `security-controls-audit` register — the verdict is **Partial**, not Met, to avoid
  false assurance). Migrations themselves run as a privileged deploy principal (not the runtime login), so
  schema evolution is unaffected. Full host compromise still defeats it (RISK-SEC-002, accepted).

## Traceability

Implements the DB-permission half of D-16. Threat T-06, abuse-case AB-5 (`security-threat-model.md`); control
C-AUDIT-04 (`security-controls.md`, ASVS V16/V13). Builds on ADR-0009/0026 (audit design), ADR-0027 (audit read
RBAC). Verified by `AuditImmutabilityDbPermissionTests` (`Category=Security`).
