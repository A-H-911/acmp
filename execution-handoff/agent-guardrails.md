# ACMP — Agent Guardrails (read before and during every build session)

**Purpose:** Hard invariants the Claude Code execution agent must never violate. A guardrail can only be overridden by a new, human-approved ADR in `/adr`. If a task seems to require breaking one, **stop and surface it** as an open question — do not work around it silently.

## The hard "DO NOT" list

1. **Do not replace the approved technology stack** (.NET 8 / ASP.NET Core / EF Core / SQL Server / React 18 + TypeScript / Vite) without a new ADR. (ADR-0002, ADR-0003, ADR-0012)
2. **Do not introduce distributed architecture** — no microservices, message brokers (RabbitMQ/Kafka), service mesh, Kubernetes, or a second database — without a demonstrated, measured need recorded in an ADR. The default is a **modular monolith**. (ADR-0001)
3. **Do not depend on the organization's runtime infrastructure** (CON-001). No org Hangfire, no org ELK/Seq, no org notification platform. ACMP self-hosts its own background jobs (app-owned Hangfire on ACMP's SQL), observability (self-hosted Seq), and notification channels. Allowed external dependencies: **SQL Server** (own instance), **Keycloak** (OIDC federation), and **Webex** (Phase 2 SaaS adapter only). (ADR-0013, ADR-0014)
4. **Do not bypass authorization.** Every endpoint and command goes through policy-based authorization (role + ABAC). No "TODO: add auth later". Enforce least privilege and segregation-of-duties (a verifier ≠ the action owner; the chairman is never the sole vote counter). (docs/10, docs/25)
5. **Do not create unaudited changes to governance records.** Every state change emits an `AuditEvent`. **Votes, issued decisions, approved ADRs, and published minutes are immutable** — they are *superseded*, never edited or deleted. Hash-chain the most sensitive records (votes/decisions/audit). (ADR-0009, docs/26)
6. **Do not treat AI-extracted content as authoritative.** Any AI-suggested minutes/decisions/actions (Phase 3) are **candidates** until a human reviews and approves them. Never auto-commit AI output to a governance record. Treat transcripts/briefs as untrusted input (OWASP LLM01).
7. **Do not store secrets in source control.** Use externalized config + Docker secrets / `.env` (git-ignored). No connection strings, tokens, or keys in code or committed files.
8. **Do not implement a feature without tests and acceptance criteria.** Each feature ships with unit/integration tests and satisfies its `AC-###`. No feature is "done" without them (see Definition of Done, docs/44).
9. **Do not break Arabic or RTL.** No hardcoded user-facing strings — everything goes through i18n (EN + AR). Use CSS logical properties + `dir`. Every screen must render correctly in RTL. Dates are Gregorian (localized formatting).
10. **Do not duplicate Tarseem or Keystone functionality.** Diagrams are rendered by Tarseem (JSON spec is the source of truth); research packages come from the optional Keystone workflow. Don't build a diagram engine or a research methodology in-app without a documented justification (ADR). (ADR-0006, ADR-0007)
11. **Do not mask assumptions or open decisions.** If you infer something not stated, record it as an `ASM-###` (docs/41). If a decision is unresolved, it's an `OQ-###` (docs/42) — use the recommended default but flag it. Never silently decide a flagged open question.
12. **Do not overengineer.** Right-size for an on-prem, low-traffic, ≤20-user internal tool. No premature abstraction, no speculative generality, no enterprise patterns the problem doesn't demand. Prefer explicit domain concepts over generic frameworks.

## The "DO" list (positive invariants)

- **Respect module boundaries.** A module never reads another module's tables. Cross-module communication is via in-process public contracts / MediatR / domain events only. (ADR-0001)
- **Clean Architecture per module:** Domain (no infra deps) → Application (handlers/validation) → Infrastructure (EF, adapters). Cross-cutting concerns run in the MediatR pipeline (validation, authorization, audit, logging).
- **Every artifact has an owner and a status.** Use the canonical status models (README §E) and identifier scheme (README §F). A *proposed* item is never rendered as *approved*.
- **Traceability is a first-class feature.** Wire relationships (topic→decision→action→ADR, etc.) as you build; keep the live traceability matrix (every MVP requirement → ≥1 decision, ≥1 work item, ≥1 test — Keystone gate G-TRACE).
- **Record decisions as ADRs.** Any new architecture decision (or a change to a settled one) is a MADR file in `/adr`, lifecycle `Draft→Proposed→Approved→Superseded/Deprecated`. Superseded ADRs are not edited.
- **Keep the planning package authoritative.** When code and the planning docs disagree, fix the code or raise an ADR/OQ — do not let them drift silently.
- **Validate before claiming.** If a library/service behavior is unverified, mark it and test it; don't assume.

## Self-check before opening a PR
Tests pass · authorization enforced · audit events emitted · EN/AR strings + RTL verified · accessibility checks pass · no secrets committed · no new high/critical vulns · acceptance criteria met · ADR added/updated if a decision changed · assumptions/open-questions recorded. (Full list: docs/44-definition-of-done.md.)

> If any guardrail conflicts with a requested change, **stop and ask** — a guardrail change requires a human-approved ADR.
