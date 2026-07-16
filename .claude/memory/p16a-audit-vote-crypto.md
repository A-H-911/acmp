---
name: p16a-audit-vote-crypto
description: "P16 security-hardening Batch 1 (audit & vote crypto core) — D-13 per-ballot chaining, D-16 nightly verify + DB-perm immutability"
metadata: 
  node_type: memory
  type: project
  originSessionId: 2031363d-7960-40fd-b178-0a9f137f06d0
---

P16 (Security hardening, ASVS 5.0 L2) is a **verify-and-close-gaps sweep** split into GO-gated batches (controls→threats map already lives in `security-controls.md` + `security-threat-model.md`). **Batch 1 = audit & vote crypto core**, branch `feat/P16a-audit-vote-crypto`, plan `~/.claude/plans/apply-the-controls-in-giggly-volcano.md` (built with a devil's-advocate review that re-verified every sub-agent claim against source).

**Shipped (all tested):**
- **D-13 per-ballot chaining (ADR-0030).** Ballots are `vote_ballots` **child rows** (NOT JSON — Options/Tally are JSON). SHA-256 chain sealed at `Vote.Close` over ALL ballot rows ordered by voter `sub` (position index in payload → insert/delete/reorder detectable), mirroring `AuditEvent` hashing; `Decisions.Domain/BallotChain.cs` self-contained (no Shared coupling). Seal-at-close because ballots are mutable until close (`ChangeBallot`). `Vote.VerifyBallotChain()` + `VerifyTally()` (recompute vs frozen `tally_json`). Legacy pre-P16 closes = `ChainSealedAt` null → unsealed/skipped (no backfill). Migration `Decisions_BallotChain`.
- **D-16 nightly job (ADR-0030, C-INS-02).** `IIntegrityCheck` seam (each module verifies its OWN aggregate, ADR-0001) + `IIntegrityVerifier` (plain service, not MediatR — avoids forcing MediatR to scan Acmp.Shared) fanning out over `AuditChainIntegrityCheck` (Shared) + `VoteChainIntegrityCheck` (Decisions); alert = high-importance Serilog + durable `AuditEvent`. `Acmp.Worker` `Cron.Daily(3)`.
- **D-16 DB-perm (ADR-0031, C-AUDIT-04).** Migration `Audit_DenyMutation`: `DENY UPDATE,DELETE ON SCHEMA::audit TO acmp_app`. **INERT until app runs least-priv (it connects as `sa`; db_owner/sysadmin bypass DENY) → P18 operator residual.** Verdict Partial, not Met. Proven by `AuditImmutabilityDbPermissionTests` (Testcontainers, restricted `probe` login).

**Security test suite = `dotnet test --filter "Category=Security"`** (339 tests). W5 evidence ledger = `docs/validation/security-controls-audit.md`.

**Scope forks (operator-confirmed):** transit/at-rest crypto → Batch 3 scaffold + P18; **Confidentiality ABAC (C-AUTHZ-04) = deferred feature [D-20]** (verified: NO `Confidentiality`/`Restricted` field in code); SoD-2 soft flag is per-spec (`warn+audit`) — untouched; SoD-4 has no hard guard (verify later batch).

**No AC verdict flips** — AC-017/018/019/020 were already Met; Batch 1 closes their "→ P16" notes. Gates green: BE build/format/1429 tests/coverage 99.67%; no FE change (no i18n delta). **MERGED to main (PR #124, squash `87dfa40`); ADR-0030/0031 ratified → Accepted 2026-07-16; status-report v1.8.0 + acceptance-audit refreshed.** AC rollup verified: 74 · 36 Met · 37 Partial · 1 Pending (AC-004) · 0 Not-met. Next: Batch 2 (CI security gates, report-only→gate so main never reds), then P14. See [[p9-voting-plan]], [[audit-slice-literal-ac017]], [[coverage-and-e2e-mandate]], [[ci-gates-run-locally-pre-push]].
