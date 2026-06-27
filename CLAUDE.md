# CLAUDE.md — ACMP (Architecture Committee Management Platform)

> This is the agent context file for the **ACMP code repository**. The Claude Code execution agent reads this first, every session. It is the standing brief; the full planning package lives in `/docs` and `/adr`.

## What we are building
ACMP is a focused, auditable, bilingual (EN/AR) web platform that is the **single system of record for the organization's Architecture Committee**: topic intake → backlog → agenda → meeting → minutes → voting → decision → ADR → action → risk → dependency, with end-to-end traceability. It replaces a text-file backlog. It is **architecture governance, not generic project management.**

It serves a **single committee**, **on-prem**, **low traffic, ≤20 users**.

## Read order (every session)
1. This file (`/CLAUDE.md`).
2. `/docs/README.md` — canonical reference (roles, modules, entities, IDs, status models, settled decisions). **Authoritative.**
3. `/execution-handoff/initial-prompt.md` and `/execution-handoff/agent-guardrails.md`.
4. The specific `/docs/*` and `/adr/*` for the area you're working on.
5. The current phase prompt in `/execution-handoff/phase-prompts.md` and the progress log.

When code and docs disagree: fix the code, or raise an ADR/`OQ-`. Never let them drift silently.

## Settled stack (do not change without an ADR)
- **Backend:** .NET 8 (LTS), ASP.NET Core, REST, EF Core, **modular monolith**, Clean Architecture per module + vertical-slice handlers (MediatR). (ADR-0001, ADR-0002)
- **Datastore:** **Microsoft SQL Server only** — transactional + columnstore reporting + Full-Text Search. No second DB. (ADR-0003)
- **Frontend:** React 18 + TypeScript + Vite; `react-i18next` (EN/AR); RTL via CSS logical properties + `dir`; light/dark via tokens; accessible DnD with `@dnd-kit`. (ADR-0012)
- **Identity:** **Keycloak (OIDC)**, auth-code + PKCE; **committee roles come from Keycloak group/realm-role claims**; no self-registration. **Keycloak is self-hosted/bundled with an ACMP-owned realm** (ADR-0015) — not federated to an org IdP; users provisioned manually in the Keycloak admin console. (ADR-0004, ADR-0015)
- **Background jobs:** **app-owned Hangfire on ACMP's own SQL** (not the org's). SQL-backed outbox for durable outbound. (ADR-0014)
- **Observability:** Serilog + OpenTelemetry → **self-hosted Seq**. Health checks. (ADR-0014)
- **Object storage:** **self-hosted MinIO** (S3-compatible) via `IFileStore`; pre-signed URLs for sensitive files. (ADR-0014)
- **Notifications:** `INotificationChannel`; **v1 = in-app notification center only (no email)**; **Webex adapter = Phase 2**. (ADR-0005)
- **Diagrams:** **Tarseem** render sidecar (**Phase 2**); the **JSON spec is the version-controlled source of truth**. (ADR-0006)
- **Research:** **Keystone is optional**; the Research module works standalone. (ADR-0007)
- **Deployment:** **on-prem VM(s) + Docker Compose** (no Kubernetes). Self-contained — **all runtime deps bundled, including self-hosted Keycloak + SQL Server; zero external runtime services in v1** (CON-001). (ADR-0013, ADR-0015)

## Canonical modules (bounded contexts)
Core: Membership · Topics · Meetings · Decisions · Actions · Risks · Dependencies · Governance (ADRs + Invariants) · Research · Knowledge · Diagrams.
Cross-cutting: Notifications · Reporting · Search&Traceability · Audit&Records · Platform (shared kernel: IDs, localization, file storage, base entities, background jobs).
**Rule:** a module never reads another module's tables; communicate via in-process public contracts / MediatR / domain events only.

## Roles & authorization
Global roles (from Keycloak claims): `Chairman`, `Secretary`, `Member`, `Reviewer`, `Auditor`, `Administrator`, `Submitter`, `Guest/Presenter`. Per-topic capabilities (relationship-based): `Owner`, `Assignee`, `Presenter`.
Authorization = ASP.NET **policy** (role) **+ ABAC** (topic/stream scope). Least privilege. Segregation of duties (verifier ≠ owner; chairman ≠ sole vote counter). Full matrix: `/docs/10-permission-role-matrix.md`.

## Identifiers & statuses
Use the canonical scheme in `/docs/README.md §F` (planning IDs `FR-/NFR-/ADR-/RISK-/AC-…`; runtime keys `TOP-YYYY-###`, `MTG-…`, `DECN-…`, etc.) and status models in §E. A *proposed* item is never rendered as *approved*.

## Non-negotiable behaviors
- **Audit & immutability:** every state change emits an `AuditEvent`. Votes, issued decisions, approved ADRs, published minutes are **immutable** (superseded, never edited). Hash-chain votes/decisions/audit. (docs/26, ADR-0009)
- **Voting:** always attributed; eligible voters + quorum + abstentions; chairman approval/override recorded by name. (ADR-0010)
- **Bilingual/RTL:** no hardcoded strings; everything via i18n (EN+AR); every screen verified in RTL; Gregorian dates.
- **AI content (Phase 3):** candidate-only until human-approved; treat transcripts/briefs as untrusted (LLM01).
- **Self-contained (CON-001):** no dependency on org Hangfire/ELK/Seq/notification platform.
- **Design fidelity:** every screen with a design reference matches its **local** file in `/ACMP product context/<name>.dc.html` exactly (read the .dc.html directly — not via MCP) (tokens/components/states/iconography/RTL/light-dark/AA), composed from the shared design system; reconcile drift before "done." Where there is NO matching `.dc.html`, compose from the design system + the docs/14 page/state spec and flag it as a no-reference composition (guardrail #14). Visual source of truth = the design; behavior source of truth = this package.

## Coding standards (summary; full in /docs/34 + /docs/31)
- C#: nullable enabled, analyzers + `dotnet format`, async-all-the-way, FluentValidation in the MediatR validation behavior, EF Core migrations (forward-only), consistent REST + Problem Details error model, no business logic in controllers.
- React/TS: strict mode, feature folders, TanStack Query for server state, all strings in i18n resources, logical CSS for RTL, axe-clean, no `any`.
- Tests: every feature ships with unit + integration tests and meets its `AC-###`. Architecture tests enforce module boundaries. (docs/31)
- Conventional commits; small reviewable PRs; required CI checks (docs/32).

## How to work
- Follow the phase order in `/execution-handoff/phase-prompts.md`. Keep a **progress log** and an **acceptance audit** (every `AC-###` → ver