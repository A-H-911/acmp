# Phase-Specific Prompts — ACMP (Deliverable 56)

Copy-pasteable prompts for each build slice. **Every prompt assumes the agent has read `/CLAUDE.md`, `/docs/README.md`, and `/execution-handoff/agent-guardrails.md`.** Each ends by requiring tests + acceptance criteria + audit + EN/AR/RTL, and updates to the progress log and acceptance audit. Work top to bottom; the core loop (Topics → Meetings → Decisions → Actions) is PH-1 priority.

> Standard footer for every prompt below (the agent must satisfy it): *Ship with unit + integration tests; satisfy the relevant `AC-###` (`/docs/40`); enforce authorization (`/docs/10`) and emit `AuditEvent`s; no hardcoded strings (EN+AR) and verify RTL; **for any screen with a design reference, match its **local** reference file in `/ACMP product context/<name>.dc.html` exactly (read the .dc.html DIRECTLY with the file tools — NOT the claude_design MCP) — tokens / components / states / iconography / RTL / AA — composing the shared design-system components, and reconcile any drift before the phase is done; where a screen/state has NO matching `.dc.html`, do NOT invent a pattern — compose from the shared design system + the `docs/14` page/state spec and FLAG it as a no-reference composition (guardrail #14)**; no secrets in source; update `/docs/_progress/progress-log.md` and `acceptance-audit.md`; conventional commits **on a `feat/P{n}-<slug>` branch off `main`** (never commit to `main` directly) — ensure CI is **green locally before pushing**, open a **PR** and **monitor the remote CI until green**, then squash-merge + sync `main` (`/docs/46-git-workflow.md`); raise an ADR for any new architecture decision.*

---

## P1 — Repository initialization
Scaffold the repo exactly per `/docs/34-repository-structure.md`: solution, `Acmp.Api` host, `src/BuildingBlocks` shared kernel, a `tests/` tree, `/deploy` (Dockerfiles + `docker-compose.yml` with api/web/**keycloak**/sqlserver/seq/minio, plus `deploy/keycloak/` realm bootstrap — ACMP self-hosts Keycloak per ADR-0015), `/.github/workflows` skeleton (`/docs/32`), `.editorconfig`, analyzers, `dotnet format`, and architecture-test project that fails on cross-module table access. Add health checks, Serilog→Seq, OpenTelemetry, and externalized config (env + Docker secrets, `.env` git-ignored). Deliver a `docker compose up` that brings up the stack healthy. No business features yet.

## P2 — Backend foundation
Establish the modular-monolith host and the **reference module pattern** end-to-end on one small module (recommend **Membership**): Domain/Application/Infrastructure layers, MediatR pipeline behaviors (validation via FluentValidation, authorization, audit, logging), EF Core DbContext with **schema-per-module**, forward-only migrations, the `ICurrentUser`/`IClock`/`IFileStore`/`INotificationChannel` abstractions, Problem Details error model, REST conventions, and OpenAPI. Prove the vertical slice with one command + one query + tests. This pattern is the template for every later module.

## P3 — Frontend foundation

**Design (visual source of truth):** build the foundation to match the local files — `/ACMP product context/ACMP Design System.dc.html` (extract tokens VERBATIM), `ACMP.dc.html` (app shell/top bar), `ACMP Navigation & IA.dc.html` (left nav + empty/loading/error/permission-denied states) — read directly with your file tools.

React 18 + TS + Vite app shell: routing, OIDC login against Keycloak (auth-code + PKCE) with role claims, TanStack Query API client, design tokens (light/dark), `react-i18next` with EN + AR resource files and a **CI i18n-parity check**, RTL via CSS logical properties + `dir` toggle, base layout + role-based navigation (`/docs/14`), and the empty/loading/error/permission-denied state patterns. Wire `@dnd-kit` with a keyboard-accessible fallback as a shared component. Verify the shell renders correctly in AR/RTL and dark mode.

## P4 — Identity & permissions

**Design (visual source of truth):** the **Users & Membership** screen in `/ACMP product context/ACMP Administration.dc.html` (that screen only).

Implement the **Membership** module fully: map Keycloak group/realm-role claims → canonical roles (`/docs/README §C`; roles come from ACMP's **self-hosted** Keycloak realm per ADR-0015 — users provisioned manually in the Keycloak admin console); committee membership administration; per-topic capabilities (Owner/Assignee/Presenter) as relationships; ASP.NET **policy-based authorization** + **ABAC** handlers (stream scope: all-read, write by role/ownership; SoD rules); delegation/temporary assignment. Build the **permission-matrix test suite** from `/docs/10`. No self-registration. Acceptance: `/docs/40` AC for auth, ABAC, SoD.

## P5 — Topic & backlog management

**Design (visual source of truth):** `/ACMP product context/ACMP Backlog & Topic.dc.html`.

Implement **Topics**: intake/submission + triage (accept/reject/return), the Topic aggregate + lifecycle (`/docs/12`), topic types + urgency/scope/source attributes (`/docs/09`), affected streams/systems, dependencies/risks references, attachments (MinIO via `IFileStore` with validation), and the **Backlog** as views over Topics (list/table/kanban/calendar/timeline, filter/sort/saved-views, prioritization incl. accessible DnD, aging indicators, status). Workflows W1–W5 (`/docs/13`). This is the heart of the core loop — prioritize it.

## P6 — Agenda & meeting management

**Design (visual source of truth):** `/ACMP product context/ACMP Agenda & Meeting.dc.html` (agenda builder + meeting workspace; other Meetings screens are no-reference compositions per guardrail #14).

Implement **Meetings**: agenda builder (select backlog topics, DnD ordering, time-box, presenter assignment, publish, carry-over), meeting creation + scheduling, attendance/apologies, discussion notes. Workflows W6–W9. Notifications (in-app) for agenda publication and meeting scheduling. Track actual time per topic.

## P7 — Minutes & decisions

**Design (visual source of truth):** the **minutes + decision** screens in `/ACMP product context/ACMP Decision, Voting & ADR.dc.html` (decision record, rationale/alternatives, supersede, convert-to-ADR, minutes — NOT the voting or ADR-registry screens).

Implement **Minutes of Meeting** (capture, versioning, review/approve, immutable after publish) and the **Decisions** module (decision record with rationale/alternatives/conditions/authority/effective date; outcomes per `/docs/README §E`; supersede; link to topic/meeting; convert decision → ADR). Workflows W10, W12, W17, W21. Enforce immutability + audit. Acceptance: decision-history/immutability AC.

## P8 — Actions & follow-up

**Design (visual source of truth):** the **Actions** register in `/ACMP product context/ACMP Lists & Registers.dc.html`.

Implement **Actions**: create from topics/meetings/decisions/risks; owner + contributors; due dates; status + progress; evidence/attachments; reminders + overdue escalation (app-owned Hangfire jobs + in-app notifications); completion criteria + verification (verifier ≠ owner). Workflows W13, W14, W22. Overdue is derived. Acceptance: action lifecycle + SoD AC.

## P9 — Voting

**Design (visual source of truth):** the **voting** screens in `/ACMP product context/ACMP Decision, Voting & ADR.dc.html` (ballot, eligible voters, quorum, abstentions, COI recusal, chairman approval/override).

Implement **Voting** (domain may be stubbed in PH-1 for the core loop; full UX here in PH-2): configure eligible voters, open/close, options, abstentions, quorum; **always attributed**; record comments; **immutable after close**; chairman final approval/override recorded by name; conflict-of-interest recusal; voting summaries. Workflow W11. Acceptance: voting-integrity AC (quorum gate, attribution, chairman-not-sole-counter, immutability + hash-chain).

## P10 — Risks & dependencies

**Design (visual source of truth):** `/ACMP product context/ACMP Traceability & Dependencies.dc.html` + the **Risks & Dependencies** registers in `ACMP Lists & Registers.dc.html`.

Implement **Risks** (identify/categorize/assess probability×impact/exposure/owner/mitigation/residual/status/escalation; associate with topics/decisions/actions/ADRs/systems/streams) and **Dependencies** (typed edges between topics/decisions/actions/systems/services/teams/streams/partners/ADRs; blocked-work detection; cross-stream impact; upstream/downstream). Workflows W15. Dashboards for risk exposure + dependencies. Keep `Dependency` (governed, status-bearing) distinct from generic `Relationship` (trace edge).

## P11 — ADRs & invariants

**Design (visual source of truth):** the **ADR** screens in `/ACMP product context/ACMP Decision, Voting & ADR.dc.html` + the **ADR/Invariant** register in `ACMP Lists & Registers.dc.html`.

Implement the **Governance** module: in-app **ADRs** (MADR template, lifecycle, supersede/deprecate, searchable repository, links to topics/decisions/research/diagrams/actions/risks/systems) and **Architecture Invariants** (categories, scope, rationale, exceptions, violations, periodic review, links to ADRs/decisions). Use the concept disambiguation in `/docs/22 §A` (principle/standard/policy/constraint/invariant/decision/ADR) — do not create duplicate concepts. Workflows W18, W21.

## P12 — Dashboards & reports

**Design (visual source of truth):** `/ACMP product context/ACMP Dashboards & Reports.dc.html`.

Implement **Reporting**: read models + columnstore; the dashboards in `/docs/27` (backlog status, aging, meeting readiness, decision status, pending approvals, overdue actions, risk exposure, dependencies, throughput, attendance, executive summary) and the `KPI-##` catalog (`/docs/28`); interactive filtering, drill-down, export (CSV/PDF), scheduled reports (Hangfire → in-app), role-based views. Basic dashboards are PH-1; advanced analytics PH-3. Validate chart-lib RTL (`OQ`).

## P13 — Webex integration (Phase 2)
Implement the **Webex adapter** behind `INotificationChannel` + a meeting-metadata client, per `/docs/18` + ADR-0005: bot + **Adaptive Cards v1.3** notifications (≤80KB), meeting metadata + recording links, webhook for recording-ready, OAuth/bot token (scoped), webhook signature verification, **429 + Retry-After** backoff via Hangfire. **Do not** assume programmatic Webex Assistant transcripts. Keep Webex strictly behind the adapter — v1 must run without it. If the environment is air-gapped, build the adapter but don't deploy a live connection.

## P14 — Tarseem integration (Phase 2)
Integrate **Tarseem** behind `IDiagramRenderer` per `/docs/19` + ADR-0006: run Tarseem as a containerized render sidecar (thin HTTP wrapper around `tarseem generate`) or a Hangfire worker invoking the CLI; **store the JSON spec as the version-controlled source of truth** (`Diagram.Spec`, `SpecHash`); store artifacts (SVG/PNG/PDF/drawio/pptx) in MinIO; surface the capability report; self-repair on the coded error contract; attach diagrams to topics/ADRs/decisions via `Relationship`. **Do not build a diagram engine.** Handle Tarseem-unavailable gracefully.

## P15 — Keystone integration (optional, Phase 3)

**Design (visual source of truth):** `/ACMP product context/ACMP Research & Knowledge.dc.html`.

Implement the optional **Research** import per `/docs/20` + ADR-0007: `IResearchImporter` that ingests a Keystone package's structured artifacts (manifest, requirements, decisions, risks, acceptance criteria, traceability) and maps them to ResearchMission/Finding/Recommendation + links. The Research module must already work **standalone** (manual entry) before this. Keystone is never embedded or a hard dependency. Adopt its ID scheme (already done in this package).

## P16 — Security hardening
Apply `/docs/25` controls to OWASP ASVS 5.0 L2: finalize authz + SoD, session/MFA at Keycloak, input validation/output encoding, file-upload + malware scanning (ClamAV sidecar `OQ`), encryption in transit (TLS everywhere) + at rest (SQL TDE, MinIO SSE), secret management, audit immutability + hash-chain verification, insider-risk controls, notification security, strict CSP, container hardening (non-root, read-only FS), and dependency/secret/image scanning + SBOM. Map each to a threat in `/docs/24`. Run the security test suite.

## P17 — Testing
Bring the full test suite to the targets in `/docs/31`: coverage thresholds, permission-matrix, workflow, audit-trail, voting-integrity, decision-history, localization/RTL, accessibility (axe), migration, backup/restore, and mocked integration contracts. Wire all gates into CI (`/docs/32`). Ensure the **acceptance audit** maps every `AC-###` to a passing test (G-TRACE/G-PROGRESS).

## P18 — Deployment
Finalize per `/docs/33`: multi-stage Dockerfiles, production `docker-compose` (+ overrides), externalized/secret config, EF m