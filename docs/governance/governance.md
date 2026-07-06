---
status: Approved
version: 1.0.0
updated: 2026-07-06
owner: lead-secretary
---

# Governance — ACMP

How ACMP is governed as an engineering artefact: the concepts it reasons about, the formal standards it holds itself to, and the rules that keep its documentation trustworthy over time. The authoritative concept definitions below are the single source of truth referenced from `README.md` §G and must not be re-defined elsewhere.

Related: the [glossary](glossary.md) (committee terms), [contributing](contributing.md) (git + CI gates), and the Architecture Decision Records in [`../adrs/`](../adrs/).

## Concept disambiguation

Seven concepts are distinct domain entities in ACMP. Conflating them produces ambiguous governance artefacts and breaks traceability. This is the one authoritative place they are defined; carried over faithfully from `docs/domain/standards-and-best-practices.md` §A.

### Principle

A high-level, enduring belief about how the organization should build and operate its systems. Prescriptive in direction but not in implementation. It does not expire and is rarely retired unless the organization's values fundamentally change.

- **Example:** "Security is a first-class concern at every layer, not a bolt-on."
- **Lifecycle:** `Draft → Proposed → Approved → (Revised | Retired)`. Revision produces a new version; the old version is archived. Never silently edited once approved.
- **Where in ACMP:** `Principle` entities in the Governance module.
- **Distinctions:** A Principle *motivates* Standards, Policies, and Invariants. It does **not** record *why a specific decision was made* — an ADR does that. A Principle is chosen and aspirational; a Constraint is externally imposed.

### Standard

A precise, measurable, agreed-upon rule or specification the organization mandates. More specific than a Principle — it names the technology, format, version, or measurement that must be met.

- **Example:** "All REST APIs must use OpenAPI 3.1 for contract documentation."
- **Lifecycle:** `Draft → Proposed → Approved → (Superseded | Retired | Exempted)`. An exemption from a Standard requires an ADR recording the rationale.
- **Where in ACMP:** `Standard` entities in the Governance module.
- **Distinctions:** A Standard *operationalizes* a Principle into a specific rule. A Standard is technical (what/how to build); a Policy is organizational (who may do what). A Standard is a rule; an Invariant is a property the architecture must *always exhibit*. A Standard violation triggers a review; an Invariant violation is a structural failure.

### Policy

An organizational rule governing *process, behaviour, or authority* — who may do what, under what conditions, and what consequences follow. Typically owned by a function (security, legal, compliance) rather than the architecture committee.

- **Example:** "All production deployments must be approved by two senior engineers."
- **Lifecycle:** `Draft → Proposed → Approved → (Revised | Retired)`. Versioned; older versions archived.
- **Where in ACMP:** ACMP is a *consumer* of organizational Policies (it implements them via RBAC/ABAC and audit controls), not their primary store. ACMP's own process rules (who may vote, who may publish minutes) are modelled as RBAC/ABAC rules. If the committee governs an external Policy, that is recorded as a Decision + ADR and the Policy lives in the org's Policy Register.
- **Distinctions:** A Policy governs *people and process*; a Standard governs *technical artefacts*. A Policy can be changed by the appropriate authority; a Constraint cannot be changed by the committee.

### Constraint

An externally imposed, non-negotiable restriction on what the architecture may or may not do. Not chosen by the committee; received from regulation, law, contractual obligation, or mandated organizational infrastructure.

- **Example:** CON-001 — "ACMP must not depend on the organization's shared runtime infrastructure."
- **Lifecycle:** No lifecycle — persists until the external authority removes or relaxes it. Recorded as `CON-###` in the planning package and `docs/risks/risk-register.md`; a relaxation is recorded in RAID against the original CON reference.
- **Where in ACMP:** `CON-###` identifiers; constraints surface in ADRs as binding context (not weighed options), and are enforced in code (e.g., CON-001 is enforced by not importing org-infra packages).
- **Distinctions:** Non-negotiable, unlike an exemptable Standard or Policy. A Constraint does not record *how* it is satisfied — an ADR records the design that satisfies it. Received and mandatory, unlike a chosen, aspirational Principle.

### Architecture Invariant

A structural property the architecture *must always exhibit*, regardless of feature evolution. Enforced architecturally (in code, tests, or linting) and derived from Principles, Standards, or Constraints. Violating an Invariant is a structural failure, not a review trigger.

- **Example:** "No module may read another module's tables directly — inter-module communication is via in-process public contracts only."
- **Lifecycle (`AIV-` in-app entity):** `Draft → Proposed → Active → (Retired | Superseded)`. Violations are tracked as separate `Violation` records against the Invariant, never as status changes on the Invariant itself.
- **Where in ACMP:** The Governance module stores Invariants as first-class entities (`AIV-YYYY-###`) recording category, scope, enforcement mechanism, exceptions policy, and open violations.
- **Distinctions:** An Invariant *enforces* a Principle or Standard structurally. An ADR may *introduce* one. An Invariant is an ongoing property, not a point-in-time Decision.

### Decision

A point-in-time, committee-ratified choice between evaluated options, with recorded rationale, alternatives considered, and vote/authority record. The *outcome of committee deliberation* on an architecture or governance topic.

- **Example:** "The committee decided (DECN-2026-012) to adopt Keycloak as the OIDC identity provider, conditional on a security review within 60 days."
- **Lifecycle (`DECN-` in-app entity):** `Pending → Ratified → (Implemented | Deferred | Converted)`. A ratified Decision is immutable (ADR-0009); superseding one requires a new Decision.
- **Where in ACMP:** Decisions module. Each Decision links to its triggering Topic, the ratifying Vote, the Chairman's approval, any conditions, and the resulting ADR.
- **Distinctions:** More specific than a Principle (a Decision says "adopt Keycloak"). An ADR *documents* a Decision. A Decision is a discrete choice event; a Standard is a standing rule.

### ADR (Architecture Decision Record)

A structured document capturing the *context, options, trade-offs, chosen option, and consequences* of a significant architectural Decision — written so future readers understand not just *what* was decided but *why*, and *what alternatives were rejected*. The persistent, human-readable memory of a Decision.

- **Example:** ADR-0001 — "Modular Monolith for v1. Context: ≤20 users, single team, time-to-value priority. Chosen: modular monolith. Consequences: modules communicate via in-process contracts; no cross-module DB access invariant."
- **Lifecycle (`ADR-` in-app entity):** `Draft → Proposed → Approved → (Superseded | Deprecated)`. A Superseded ADR is never deleted; the superseding ADR references it. No in-place editing of Approved ADRs (append-only audit requirement).
- **Where in ACMP:** Governance module; template follows MADR (see below), extended with committee-specific fields (vote reference, related topic, affected systems, author, reviewers).
- **Distinctions:** An ADR documents a **Decision** (one-to-one or many-to-one). It may introduce or reference an **Invariant**, and describes alignment with a **Standard** or the rationale for an exemption. It is a specific, time-stamped record, not an aspirational Principle or a standing Standard.

### Summary table

| Concept | What it is | Lifecycle | Mutable? | ACMP entity |
|---|---|---|---|---|
| **Principle** | Enduring belief about how to build systems | Draft→Proposed→Approved→(Revised\|Retired) | Versioned (not silently edited) | `Principle` |
| **Standard** | Precise technical rule / specification | Draft→Proposed→Approved→(Superseded\|Retired\|Exempted) | Versioned; exemptions via ADR | `Standard` |
| **Policy** | Organizational process/authority rule | Draft→Proposed→Approved→(Revised\|Retired) | Versioned | External register (ACMP consumes) |
| **Constraint** | Non-negotiable external restriction | Persistent until relaxed | Not relaxed without authority change | `CON-###` (planning) |
| **Architecture Invariant** | Structural property always exhibited | Draft→Proposed→Active→(Retired\|Superseded) | No; violations tracked separately | `AIV-YYYY-###` |
| **Decision** | Point-in-time committee choice (ratified) | Pending→Ratified→(Implemented\|Deferred\|Converted) | Immutable after ratification | `DECN-YYYY-###` |
| **ADR** | Structured memory of a Decision with context + rationale | Draft→Proposed→Approved→(Superseded\|Deprecated) | No in-place edit after Approved | `ADR-YYYY-###` (in-app) |

## Standards & frameworks

The formal standards and industry frameworks ACMP holds itself to. Non-compliance with a mandatory standard requires an explicit ADR-recorded justification. Full detail lives in `docs/domain/standards-and-best-practices.md`.

| Standard / framework | What it governs | ACMP posture |
|---|---|---|
| **arc42** | Architecture documentation template (12 sections) | `docs/domain/architecture-detail.md` is structured along arc42's 12 sections; each section maps to a planning doc |
| **C4 model** | Layered architecture diagrams (Context, Container, Component, Code) | Diagrams use C4 levels L1–L3; rendered by Tarseem's `architecture/C4` family (L4 not diagrammed) |
| **MADR** | Architecture Decision Record format | Repo ADRs use a MADR-lite template (`adr/template.md`); in-app ADRs extend MADR 3.x with committee fields |
| **OWASP ASVS 5.0 — Level L2** | Application security verification (~350 requirements, 17 chapters) | Target L2 across all chapters (sensitive internal governance data; L3 not required). Mapped in `docs/domain/security-controls.md` |
| **OWASP Top 10 (2021)** | Baseline web-app security checklist | Applied as a design + review checklist (access control, injection, crypto, misconfiguration, vulnerable components) |
| **OWASP LLM Top 10 — LLM01** | Prompt-injection risk for AI features | Phase 3 only: AI-extracted transcript content is untrusted candidate data, human-approved before commit; content isolation + structured output |
| **WCAG 2.2 Level AA** | Web accessibility (W3C Recommendation) | Target for all React UI, both themes, EN + AR/RTL. axe-core in Playwright + manual screen-reader testing before release. AAA optional |
| **Conventional Commits** | Commit message structure | Required on every branch (see [contributing](contributing.md)) |
| **Semantic Versioning (SemVer)** | Version numbering | Applied to releases and API versioning (`/api/v1/`; breaking changes bump the version) |

Standards ACMP mandates internally (e.g., "REST APIs use OpenAPI 3.1", modular-monolith + Clean Architecture + vertical slice per ADR-0001/ADR-0002) are recorded as `Standard` entities and enforced by the module-boundary Architecture Invariants.

## Documentation governance

### Two documentation realms

ACMP keeps a hard boundary between engineering docs and committee knowledge; crossing it causes duplication and version drift.

| Realm | What lives here | Audience |
|---|---|---|
| **Repo docs** (`/docs`, `/adr`, module READMEs, runbooks) | Architecture decisions, planning package, deployment runbooks, API reference, contribution guide, per-module design notes | Engineers, the Claude Code agent, DevSecOps |
| **In-app wiki** (Knowledge module) | Governance standards, principles, topic/ADR/minutes templates, role how-to guides, glossary | Committee members, Secretary, Chairman, Submitter |

Engineering implementation details (repo ADRs, diagrams, migration guides, CI runbooks) belong in the **repo**. Committee process guides, terminology, and reusable templates belong in the **in-app wiki**. Repo ADRs govern how ACMP is built; in-app ADRs are the committee's governance output that ACMP exists to produce — the two are distinct and must not be conflated.

### ADR lifecycle

Repo ADRs are MADR-lite Markdown in [`../adrs/`](../adrs/), numbered sequentially (4-digit, from `0001`). The lifecycle:

| Step | Action |
|---|---|
| **Draft** | Copy `adr/template.md` → `adr/ADR-NNNN-<kebab-title>.md`; author the context and options |
| **Proposed** | Open a PR; link the relevant `FR-`/`NFR-`/`OQ-` from the planning package |
| **Accepted** | PR merged; status → `Accepted`; date updated |
| **Superseded** | A new ADR references the old one (`Superseded by ADR-MMMM`); the old ADR's status header is updated but its **original text is preserved** (immutable after Accepted, per ADR-0009) |
| **Deprecated** | Decision no longer relevant; status → `Deprecated` with a reason |

**Rule:** never delete an ADR file. Superseded and deprecated ADRs remain in the repo as part of the decision history — this mirrors the audit/immutability principle (ADR-0009).

### Review cadence

Documentation is kept fresh by tying each class to an update trigger and an owner, not to a calendar alone.

| Doc class | Review cadence | Owner |
|---|---|---|
| Planning package | Phase gate + major scope/decision change | Lead secretary |
| ADRs | On creation / supersession (always PR-reviewed) | Proposer + lead engineer |
| Architecture doc (arc42) | Phase gate + any ADR acceptance | Lead engineer |
| Per-module READMEs | Sprint boundary + interface change | Module lead |
| API docs (OpenAPI) | Continuous, auto-generated in CI | CI |
| User guides (EN + AR) | Per release | Secretary |
| In-app wiki | Quarterly + committee process change | Secretary |
| CLAUDE.md | Phase gate + module addition | Lead engineer |
| Security exemptions | 90-day expiry | Security owner |

Staleness is caught mechanically where possible: OpenAPI is regenerated in CI (a stale spec is a visible PR diff), EN↔AR i18n key parity is enforced by `scripts/check-i18n.sh`, and cross-doc links are link-checked in CI.
