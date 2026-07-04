---
name: p11-adrs-invariants-plan
description: "P11 Governance module (ADRs + Invariants) — GO-gated slice plan, operator decisions, reconciliations."
metadata: 
  node_type: memory
  type: project
  originSessionId: 07679861-1914-4e35-b95d-1fc54ba0a04d
---

**P11 = ADRs & Invariants (Governance module).** Next phase after [[p10-risks-deps-traceability-plan]] (P10 COMPLETE). New bounded context `Acmp.Modules.Governance`. Skeleton mirrors **Risks** (folders/DbContext/key-gen/register-query/endpoint style); lifecycle/immutability mirrors **Decisions** (supersede-not-edit, `SupersededBy…Id`, AuditEvent-per-transition via existing `IAuditSink`, no new store, NO hash-chain for ADR). `ArtifactType` already has `Adr=10`/`Invariant=11`; only `TraceabilityLinks` key-resolvers missing.

**Usage Map phase labels DRIFTED** — it calls this "P10 Governance/Invariants"; execution reality = P11. Design content still authoritative (guardrail #14), only the label drifted.

**Trace targets (EPIC-17, all priority S / Phase-2; NO dedicated AC-### — AC-less like Risks; audit-immutability AC-017/018 apply):** ADR = FR-099/100/101/102(FTS deferred)/103/104. Invariant = FR-106/107/108/109. Promotion = FR-068.

**GO-gated slices:**
- **P11a DONE (pre-merge, branch `feat/P11a-adr-backend`)** — Governance module + Adr aggregate built + all CI gates green locally (1007 BE tests, +24 Governance; per-file cov ≥95% global 99.76%; format exit 0; ArchUnit 40/40; migration `Governance_Init`). Authz: standalone ADR create = Chairman/Secretary only (AiO needs an owner resource, denied at bare create — like Risks). No new Traceability seam (edges self-describe, ArtifactType already has Adr). Next = operator GO → squash-merge → P11b.
- **P11a** ADR backend — Governance module + `Adr` aggregate (MADR-lite: Title/Context/Decision/Consequences pos+neg/Options-with-chosen/DecisionDrivers; Author/Reviewers/AffectedSystems; `SourceDecisionId` nullable). Lifecycle Draft→Proposed→Approved→(Superseded|Deprecated) + Proposed→Draft(requestChanges). Policies Adr.Create(AiO)/Approve/Supersede. Key `ADR-YYYY-###` (NOT planning `ADR-####`). ArchUnit rule, TraceabilityLinks ADR resolver, migration, ≥95% cov. Notifications: reviewers on Proposed, stakeholders on Approved.
- **P11b** ADR UI — `/adrs` register(adrs tab) + `/adrs/:key` MADR detail + Create-ADR dialog; retires `/adrs` PlaceholderPage. Fidelity: `ACMP Decision, Voting & ADR.dc.html` `isAdr` + `Lists & Registers`(adrs) + `Create Flows`(adr). Export-MD = client-side .md from fields (FR-104; md read-render still deferred per DV-04).
- **P11c** Invariant backend — `Invariant` aggregate same module: Kind{Principle,Standard,Policy,Constraint}+Category+Scope+Statement+Rationale+Owner+ExceptionsPolicy(text). Lifecycle Draft→Proposed→Active→(Retired|Superseded). Policies Invariant.Create(AiO)/Approve. Key `AIV-YYYY-###`. Notifications: stream owners on Activate.
- **P11d** Invariant UI — invariants tab on `/adrs` + **Invariant detail (NEW no-reference composition — FLAG)** + Create-Invariant dialog.
- **P11e** Decision→ADR promotion (FR-068/W17) — shared `IDecisionReader` seam, pre-fill ADR from Decision, `SourceDecisionId` + bidirectional `Relationship(DerivesFrom)`, wire the "Convert to ADR" button already in the Decision design.

**OPERATOR DECISIONS (2026-07-04):**
1. **Violations (FR-108/109) = DEFER.** Show count 0/placeholder. Model is CONTESTED → raise OQ: FR-108 implies a Violation sub-entity owned by Invariant, but docs/12 §9 + W18 say violations = Risk/Action/AuditEvent (not a new type). `/governance/violations` already unbuilt in Usage Map.
2. **SoD on Approve = SOFT** (author may approve; recorded in audit payload). Mirror Decisions SoD-2 soft. `SegregationOfDuties` helper exists.
3. **P11e promotion = BUILD in P11.**

**Design↔behavior reconciliations to FLAG:** (1) ADR design tabs proposed/accepted/superseded (3) vs canon 5 states + label "Approved" not "accepted" — implement 5, canon label, design owes Draft/Deprecated chips. (2) Invariant detail+violations = NEW screen. (3) FR-106 omits Kind but docs/12/W18+design form require it — include Kind AND Category, flag FR gap; also FR-106 categories "unverified — validate w/ committee" (OQ). (4) FTS→Search phase.

**Deferred:** FTS search, ADR templates (FR-105→Knowledge), invariant exception workflow (FR-110→Phase 3), diagram/research links.

**Audit seam:** `IAuditSink.EmitAsync("Governance.AdrApproved", sub, payload, ct)` — mirror `IssueDecision.cs`. Immutability guard mirrors `Decision.Supersede(...)`. Follow [[ci-gates-run-locally-pre-push]] + [[always-stage-claude-memory-in-commits]].
