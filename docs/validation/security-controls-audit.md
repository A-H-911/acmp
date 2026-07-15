---
status: Living
version: 0.1.0
updated: 2026-07-16
owner: Claude Code execution agent
generation: derived
---

# Security Controls Audit — ACMP (OWASP ASVS 5.0 L2)

The evidence ledger for the P16 security-hardening slice. Each control in
[`../domain/security-controls.md`](../domain/security-controls.md) is mapped to its **implementation status**,
the **code path** that realizes it, the **test** that proves it, and the **threat** it addresses (the
control→threat binding itself is authored in the control + threat-model docs; this register records *whether and
where* each control is implemented). P16 runs as GO-gated batches — rows carry the batch that delivers/hardens
them.

**Status vocabulary:** `Met` (implemented + tested) · `Partial` (implemented, with a stated residual) ·
`Operator/P18` (in-repo scaffold + doc; effective only after operator action) · `Batch N` (scheduled later in
P16) · `Deferred-feature` (out of hardening scope — logged as a feature).

## Batch 1 — Audit & vote crypto core (this slice)

| Control | Threat | Status | Code | Test |
|---|---|---|---|---|
| **C-IMM-04** per-ballot hash chain (D-13) | T-04, T-21, AB-1 | **Met** | `Decisions.Domain/BallotChain.cs`, `Vote.SealBallotChain`/`VerifyBallotChain` (sealed at Close) | `VoteBallotChainTests` (content/link/recusal/tally tamper, legacy skip) |
| **C-IMM-04** tally-vs-ballots recompute | T-04, AB-1 | **Met** | `Vote.ComputeTally`/`VerifyTally` | `VoteBallotChainTests`, `VoteChainIntegrityCheckTests` |
| **C-INS-02** nightly integrity verify job (D-16) | T-06, AB-5 | **Met** | `IIntegrityVerifier`/`IntegrityVerifier` + `IIntegrityCheck` seam; `AuditChainIntegrityCheck`, `VoteChainIntegrityCheck`; `Acmp.Worker` `Cron.Daily(3)` | `IntegrityVerifierTests`, `VoteChainIntegrityCheckTests` |
| **C-AUDIT-04** DB-permission audit immutability (D-16) | T-06, AB-5 | **Partial** (Operator/P18) | migration `Audit_DenyMutation` (`DENY UPDATE,DELETE ON SCHEMA::audit TO acmp_app`) | `AuditImmutabilityDbPermissionTests` (Testcontainers, restricted login) — **inert until app runs least-priv (P18), see ADR-0031** |
| **C-AUDIT-01/02/03** append-only, per-transition, server-derived | T-06, T-05, T-21 | **Met** (prior) | `AuditEvent` (no mutators), `SqlAuditSink.EmitEnrichedAsync` | `AuditEventEnrichmentTests`, `AuditAtomicityTests` |
| **C-AUDIT-05/06** privileged-action + deny auditing | T-07, T-08 | **Met** (prior) | `AuthorizationBehavior`, governed emit sites | `SegregationOfDutiesTests`, `AuditApiTests` |
| **Security test suite** | — | **Met** | `[Trait("Category","Security")]` across authz/SoD/audit/immutability + Batch-1 tests | `dotnet test --filter "Category=Security"` → 339 tests green |

> The AuditEvent hash-chain + on-demand verify (`GET /api/audit/verify`) predate P16 (AC-017/018/019/020 already
> `Met`). Batch 1 **closes the "→ P16" residual notes** on those rows (nightly job + DB-permission) and adds the
> per-ballot chain (D-13) — **no AC verdict changes**; per-ballot chaining maps to no single AC (it is tech-debt).

## Authentication / Authorization / SoD (prior — verified this slice)

| Control | Threat | Status | Evidence |
|---|---|---|---|
| C-AUTH-01/03/04 OIDC PKCE, JWT-per-request, claims-only roles | T-01, T-02, T-11 | Met | `RealJwtAuthTests`, `PermissionMatrixTests` |
| C-AUTHZ-01/02/03 policy deny-by-default, resource-based, stream-scope ABAC | T-07, T-08 | Met | `PermissionMatrixTests`, `AbacHandlerTests`, `ResourceAuthorizerTests` |
| C-AUTH-05 SoD-1/3/5 hard guards | T-04, T-05, T-07 | Met | `SegregationOfDutiesTests` |
| C-AUTH-05 SoD-2 (approver≠author) | T-05 | Met (soft, per spec `warn+audit`) | `MinutesHandlerTests` (AC-014) |
| C-AUTH-05 SoD-4 (recorder≠owner/presenter + COI) | T-05 | **Partial — no distinct hard guard found; verify in a later batch** | — |
| **C-AUTHZ-04 Confidentiality ABAC (Restricted topics)** | T-08, AB-6 | **Deferred-feature** | No `Confidentiality`/`Restricted` field exists in code — requires domain field + migration + UI + search-exclusion + handler; logged in deferred-work / open-question |
| C-IMM-01/02/03 vote/decision/MoM/ADR immutability + 409 | T-04, T-05 | Met | `VoteTests`, `RowVersionConcurrencyTests` |
| C-INP-01/02/03 validation, no-SQLi, output encoding | T-12, T-13 | Met | `ValidationBehavior`; EF-only; `MarkdownView` DOMPurify allowlist |

## Later P16 batches (scheduled — not this slice)

| Control group | Threat | Target |
|---|---|---|
| C-SUP-01/02, C-SAST-01 SAST/secret/image scan + SBOM + gate CVE + digest-pin | T-03, T-17, AB-10 | **Batch 2** (report-only → triage → gate, so `main` never reds) |
| C-CRYPTO-01/02/03 TLS-everywhere, SQL TDE, MinIO SSE, backup encryption | T-20, T-09, T-23 | **Batch 3** (scaffold + doc; key custody = Operator/P18) |
| C-WEB-01/02 strict CSP (drop `style-src` unsafe-inline), HSTS, CSRF | T-13, T-14 | **Batch 3** |
| C-CON-001/002/003 non-root, read-only FS, cap_drop, no-new-privileges | T-17, T-23 | **Batch 4** |
| C-API-03 rate limiting | T-10, T-22 | **Batch 4** |
| C-FILE-01/02 magic-byte sniffing; ClamAV = operator opt-in (OQ-026 default: whitelist v1) | T-15, AB-9 | **Batch 4** (sniffing); ClamAV = Operator opt-in |
| C-PRIV-01/02 Serilog PII/secret redaction | T-19, AB-8 | **Batch 4** (no destructuring policy found today) |
| C-NOTIF-01/02 notification-body minimization | T-19 | **Batch 4** (audit) |
| C-INS-01 Seq anomaly-alert rules (OQ-025: audit+alert, no dual-control) | AB-1/2/5/6 | **Batch 4** |
| C-WX-*, C-AI-* Webex (P2) / AI extraction (P3) | T-18, T-16 | Out of v1 phase scope |

## Traceability
Implements the P16 evidence leg of Deliverable 33. Controls: `../domain/security-controls.md`; threats:
`../domain/security-threat-model.md`; decisions: ADR-0009/0026/0027 (audit), **ADR-0030** (per-ballot chain),
**ADR-0031** (DB-permission immutability). Deferred: D-13 (closed this batch), D-16 (job done; DB-permission
Operator/P18), Confidentiality ABAC (new feature). OQ defaults honored: OQ-024 (no mTLS v1), OQ-025 (no
dual-control), OQ-026 (ClamAV opt-in), OQ-028 (strict CSP).
