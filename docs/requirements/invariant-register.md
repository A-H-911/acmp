---
status: Approved
version: 1.0.0
updated: 2026-07-06
owner: lead-secretary + Claude Code execution agent
---

# Invariant Register — ACMP

Non-negotiable invariants (`INV-`). Each is a hard rule that **can only be overridden by a new, human-approved ADR** in [`docs/adrs/`](../adrs/). If a task seems to require breaking one, **stop and surface it** — do not work around it silently. These are the register form of the execution guardrails; they are surfaced early in every handoff (see [AGENTS.md](../../AGENTS.md)).

## Conventions

Status is `Approved` for every active invariant. "Enforced by" names the primary mechanism (architecture test, policy, CI gate, code review, or human process). A superseding ADR moves an invariant to `Superseded` and records the successor rule.

| ID | Invariant | Rationale | Enforced by |
|---|---|---|---|
| INV-001 | Do not replace the approved technology stack (.NET 8 / ASP.NET Core / EF Core / SQL Server / React + TypeScript / Vite) without a new ADR. | The stack is settled and load-bearing; drift fragments the build and the ops story. | ADR-0001/0002/0003/0012; code review |
| INV-002 | No distributed architecture — no microservices, message brokers (RabbitMQ/Kafka), service mesh, Kubernetes, or a second database — without a demonstrated, measured need recorded in an ADR. The default is a **modular monolith**. | Right-sized for on-prem, low-traffic, ≤20 users; premature distribution is pure cost. | ADR-0001; architecture tests; code review |
| INV-003 | Do not depend on the organization's runtime infrastructure (CON-001). ACMP self-hosts and **bundles all runtime deps** — incl. self-hosted Keycloak (ACMP-owned realm) + SQL Server — in its own Docker Compose stack; zero external runtime services in v1. Only external dependency = Webex (Phase 2). | Self-contained deployment is a hard constraint from the operator. | ADR-0013/0014/0015; deployment config review |
| INV-004 | Do not bypass authorization. Every endpoint and command goes through policy-based authorization (role + ABAC). Enforce least privilege and segregation of duties (verifier ≠ action owner; chairman is never sole vote counter). | Governance integrity and least privilege. | ASP.NET policies; CapabilityHandler (ABAC); tests |
| INV-005 | Do not create unaudited changes to governance records. Every state change emits an `AuditEvent`. **Votes, issued decisions, approved ADRs, and published minutes are immutable** — superseded, never edited or deleted. Hash-chain votes/decisions/audit. | Auditability and tamper-evidence of the system of record. | ADR-0009; append-only audit; hash chain; tests |
| INV-006 | Do not treat AI-extracted content as authoritative. Any AI-suggested minutes/decisions/actions (Phase 3) are **candidates** until a human reviews and approves them. Treat transcripts/briefs as untrusted input (OWASP LLM01). | Human-reviewed automation; prevents unverified content entering the record. | ADR-0007 principle; human approval gate |
| INV-007 | Do not store secrets in source control. Use externalized config + Docker secrets / `.env` (git-ignored). No connection strings, tokens, or keys in code or committed files. | Secret hygiene. | secret scanning; code review; `.gitignore` |
| INV-008 | Do not implement a feature without tests and acceptance criteria. Each feature ships with unit + integration tests and satisfies its `AC-###`. | No feature is "done" without demonstrable acceptance. | DoD; CI coverage gate; acceptance audit |
| INV-009 | Do not break Arabic or RTL. No hardcoded user-facing strings — everything via i18n (EN + AR). Use CSS logical properties + `dir`. Every screen renders correctly in RTL. Dates are Gregorian (localized formatting). | Bilingual/RTL is first-class, not a retrofit. | i18n parity check; RTL visual regression; review |
| INV-010 | Do not duplicate Tarseem or Keystone functionality. Diagrams are rendered by Tarseem (JSON spec is the source of truth); research packages come from the optional Keystone workflow. No in-app diagram engine or research methodology without a documented ADR. | Avoid reinventing solved, owned capabilities. | ADR-0006/0007; code review |
| INV-011 | Do not mask assumptions or open decisions. Inferences are recorded as `ASM-###`; unresolved decisions as `OQ-###` (use the recommended default but flag it). Never silently decide a flagged open question. | Keeps the unresolved visible and traceable. | assumption/open-question registers; review |
| INV-012 | Do not overengineer. Right-size for an on-prem, low-traffic, ≤20-user internal tool. No premature abstraction, speculative generality, or enterprise patterns the problem doesn't demand. | Simplicity and maintainability at this scale. | code review; architecture tests |
| INV-013 | Do not commit directly to `main`. Every phase/slice ships on a short-lived branch → PR → green CI → review (GO) → squash-merge → delete branch → sync local `main`. `main` stays green and deployable. | Reviewable, revertible, always-green trunk. | branch protection; CI; git workflow |
| INV-014 | Do not let the UI drift from the reference design. Any screen with a matching `.dc.html` in [`ACMP product context/`](../../ACMP%20product%20context/) matches it exactly (read directly, not via MCP) — tokens/anatomy/states/iconography/RTL/light-dark/copy/AA — composed from the shared design system. No-reference screens compose from the design system + the IA spec and are flagged. | Design is the visual source of truth; the package is the behavior source of truth. | design-parity ledger; visual regression; review |

## Coverage

Every invariant is checked in the [traceability matrix](../validation/traceability-matrix.md) backward view (invariant → decision/ADR → work item → test). A violation requires a new ADR (`docs/adrs/adr-NNNN-*.md`, status Proposed) before any work proceeds.
